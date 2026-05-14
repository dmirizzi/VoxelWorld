# Sunlight Parallelization Plan

## Current Architecture

`SunlightUpdateJob.ExecuteAsync` dispatches a single background `Task` that calls `LightMap.UpdateSunlight`. That method:

1. Seeds every voxel in the top row (`y = ChunkSize - 1`) of each topmost chunk at sunlight level 15.
2. Calls `PropagateLightNodes` — a BFS that freely crosses chunk (and XZ column) boundaries, writing light values via `Chunk.SetLightChannelValue` as it goes.

All of this runs on one thread. The goal is to parallelise step 2 by giving each XZ chunk column its own concurrent task.

A **chunk column** is the set of all chunks sharing the same `(ChunkPos.x, ChunkPos.z)`. Sunlight enters from the top of each column and propagates downward; it only escapes horizontally when it meets a transparent opening (e.g. a cave mouth, water surface).

---

## Race Conditions When Running Columns in Parallel

### 1. Non-Atomic Read-Modify-Write on `_lightMap`

`Chunk.SetLightChannelValue` is a three-instruction RMW:

```csharp
var mask            = (ushort)~(0xF << (channel * 4));
var maskedOldValue  = (_lightMap[pos.x, pos.y, pos.z] & mask);   // (1) read
var newValue        = (ushort)((intensity & 0xF) << (channel * 4));
_lightMap[pos.x, pos.y, pos.z] = (ushort)(maskedOldValue | newValue); // (2) write
```

`ushort[,,]` element writes are not guaranteed to be atomic on all architectures. Two threads writing to the **same cell** — which happens whenever sunlight from two adjacent columns races to illuminate the same border voxel — can produce a torn value.

### 2. TOCTOU: Check-Then-Set on Light Level

In `PropagateLightNodes`:

```csharp
var neighborLightLevel = neighborChunk.GetLightChannelValue(...); // read
if (neighborLightLevel + 2 > currentLightLevel) continue;         // check
// ... (time passes — another thread can write here) ...
neighborChunk.SetLightChannelValue(..., newLightLevel);            // write
```

Two column threads can both pass the guard for the same border voxel, both compute the new light level independently, and both write — the second write silently discards the first.

### 3. Shared `visitedChunks` HashSet

`PropagateLightNodes` receives a `HashSet<Vector3Int> visitedChunks` that it adds to whenever light crosses into a new chunk. `HashSet<T>` is **not thread-safe**. Concurrent `Add` calls corrupt the internal state and can throw or silently drop entries.

In the current single-threaded setup this is fine; under parallelism each column task would need its own instance.

### 4. `visitedByChunk` State Is Per-Call and Invisible Across Tasks

Each `PropagateLightNodes` call maintains a local `Dictionary<Chunk, bool[,,]> visitedByChunk` to avoid re-processing nodes. Two tasks processing adjacent columns each have their own copy of this state, so they can both mark the same border voxel as "to be processed" and both set it — with neither being able to see the other has already handled it. The result is redundant work at best and a corrupted light value at worst.

### 5. Border-Chunk Write-Write Conflicts

The fundamental geometric problem: a transparent voxel at the edge of column A's topmost chunk is a direct horizontal neighbor of column B's topmost chunk. When both column tasks propagate their top-row seeds simultaneously, both tasks try to write to that shared border voxel at the same time. There is no synchronisation.

### What Is Safe (Not a Race)

- **`_chunkData` reads** (`GetVoxelInsideChunk`): Voxel block IDs are written only in `PreExecuteSync` and never touched during async execution. Concurrent reads are safe.
- **`_neighboringChunks` reads**: The neighbor-reference array is populated during `ConnectNeighbor` at chunk-creation time, before any jobs run. Concurrent reads are safe.
- **Intra-column writes**: Propagating straight down within a single column's chunks touches only that column's `_lightMap` cells. No other column task ever writes to those cells. These writes are safe to run concurrently with other columns.

---

## How Far Can Horizontal Light Travel?

`GetNewLightLevel` attenuates by 1 for every non-downward step. Starting at the maximum level 15, light can travel at most **14 voxels horizontally** (level drops to 1, then the node is discarded at ≤ 1). A chunk is 16 voxels wide, so horizontal spill is bounded to **at most one adjacent chunk column** per propagation event. This bound is critical for designing a safe parallel scheme.

---

## Proposed Solution: Two-Phase Sunlight Propagation

### Core Idea

Split sunlight propagation into two phases with a clean boundary:

| Phase | Direction | Scope | Thread safety |
|-------|-----------|-------|---------------|
| 1 – Vertical | Down only (at level 15) | Own column's chunks only | Fully parallel, no locks |
| 2 – Horizontal Spill | All 6 directions | All chunks | Single-threaded |

Phase 1 handles the bulk of the work (the straight-down sunlight shaft under open sky, the dominant case). Phase 2 handles cave mouths, overhangs, and any other horizontal or level-attenuated spread — geometrically complex but a small fraction of total voxels.

### Phase 1 — Parallel Vertical-Only BFS (`SunlightColumnJob`)

One job per topmost chunk (one per XZ column with loaded chunks).

**`PreExecuteSync`**
- Receive the topmost chunk for this column from the coordinator.
- Declare `AffectedChunks` as every chunk in this XZ column. No two column jobs share a column, so their `AffectedChunks` sets are disjoint — the existing scheduler reservation system will let all of them run concurrently.

**`ExecuteAsync`**
- Seed the BFS from the top row (`y = ChunkSize − 1`) of the topmost chunk, setting all 256 voxels to sunlight level 15.
- Run a modified BFS that **only follows the `Vector3Int.down` direction** and only while the current light level is 15 (the no-attenuation sunlight rule).
- When a BFS node would propagate in any other direction, or when the downward neighbor is already at the same or higher level, do **not** enqueue it — instead record the current node as a **spillover seed** (a `LightNode` with its global position and light level).
- Never read from or write to chunks outside this column. The down direction never leaves the column's XZ footprint, so this is automatically satisfied.

**`PostExecuteSync`**
- Append this column's local spillover list into `VoxelWorld`'s accumulated spillover buffer (a plain `List<LightNode>` — no lock needed because `PostExecuteSync` always runs on the main thread).
- Call `world.QueueChunksForLightMappingUpdate` for this column's own chunks (the straight-down-lit chunks can begin light-mapping independently of Phase 2).

### Phase 2 — Sequential Horizontal Spill (`SunlightHorizontalSpillJob`)

A single job scheduled by the last column job to complete.

**`PreExecuteSync`**
- Read the spillover buffer from `VoxelWorld` and clear it (main thread, no concurrency concerns).
- Declare `AffectedChunks` as the union of all currently loaded chunk positions. This is conservative but guarantees the scheduler will not start any concurrent stage-2 work while horizontal spill is running. (In practice the reservation is not strictly necessary because stage-1 column jobs will all have finished before this job starts, but it is correct and safe.)

**`ExecuteAsync`**
- Run the existing `PropagateLightNodes` in full multi-directional mode, seeded from the spillover buffer.
- The `neighborLightLevel + 2 > currentLightLevel` guard already correctly skips voxels that Phase 1 has illuminated to the optimal level.
- Track `visitedChunks` normally — single-threaded, no concurrency concern.

**`PostExecuteSync`**
- Call `world.QueueChunksForLightMappingUpdate` for all chunks visited during horizontal spill.

### Coordinator (`SunlightUpdateJob` refactored or replaced)

`SunlightUpdateJob.PreExecuteSync` already collects the list of topmost chunks. After refactoring it becomes a **pure coordinator** that, in its `PostExecuteSync`:

1. Clears `VoxelWorld`'s spillover buffer.
2. Enqueues one `SunlightColumnJob` (stage N) per topmost chunk.
3. Enqueues the single `SunlightHorizontalSpillJob` (stage N+1).

Both the column jobs and the spill job are in the queue from the start; the scheduler's own stage ordering handles the rest.

---

## Job Coordination — Scheduling the Spill Job

No explicit counter or signalling mechanism is needed. `WorldUpdateScheduler.NextJobIsNotHigherStageThanActiveJobs` already guarantees that no stage N+1 job starts until every stage-N job has completed its `PostExecuteSync`. Placing `SunlightColumnJob` at stage N and `SunlightHorizontalSpillJob` at stage N+1 is sufficient — the spill job simply sits in the queue and the scheduler will not dispatch it until all column jobs are fully done.

Because the coordinator enqueues the spill job upfront (before any column job even starts), there is no race between "last column job finishing" and "spill job being scheduled".

The spill job can be assigned to **the same scheduler stage** as the column jobs (currently stage 1 in the intent, stage 3 in the live code — whichever is used). Because it is enqueued only after all column jobs complete, and the scheduler does not advance stages until all active jobs finish, the spill job will naturally execute after the column jobs and before any stage-2 (light mapping) or stage-3 (rebuild) work begins.

---

## Required Code Changes

### New classes

| Class | Replaces / Augments |
|-------|---------------------|
| `SunlightColumnJob` | New; spawned N times by coordinator |
| `SunlightHorizontalSpillJob` | New; scheduled dynamically |

### Modified classes

| Class | Change |
|-------|--------|
| `SunlightUpdateJob` | Becomes a coordinator; no longer calls `LightMap.UpdateSunlight` directly |
| `LightMap` | Add `UpdateSunlightColumnVertical(Chunk topChunk, List<LightNode> spillover)` — down-only BFS; add `PropagateSpilloverNodes(IEnumerable<LightNode> seeds, HashSet<Vector3Int> visitedChunks)` — existing multi-direction BFS reused; **remove `UpdateSunlight`** (no longer called by anything) |
| `VoxelWorld` | Add `List<LightNode> SunlightSpilloverBuffer` (populated by column jobs in `PostExecuteSync`, consumed and cleared by the coordinator before column jobs are enqueued) |
| `WorldUpdateScheduler` | Add `AddSunlightColumnJob(...)` and `AddSunlightHorizontalSpillJob(...)` helpers |

### No changes needed to

- `Chunk` — `SetLightChannelValue`, `GetLightChannelValue`, `GetVoxelInsideChunk`, `TryGetNeighboringChunkVoxel` are all untouched. Phase 1 exclusively writes to its own column, so no synchronisation is added to `Chunk`.
- All other job types.

---

## Alternative Approaches Considered

### Lock per chunk

Add a `lock` object to each `Chunk`. Any thread writing to a border chunk acquires the lock first. Allows full multi-directional BFS across columns but introduces contention on border chunks and risks priority inversion under Unity's `Task` scheduler. Rejected: the two-phase approach eliminates shared writes entirely, which is strictly better.

### Interlocked updates on the light map

Pack the 16-bit light value into an `int` and use `Interlocked.CompareExchange` spin-loops for atomic RMW. Correct, but requires replacing `ushort[,,]` with `int[,,]` (2× memory) and adds CAS retry loops throughout `PropagateLightNodes`. Rejected: invasive change for marginal gain over the two-phase split.

### 4-colour checkerboard scheduling

Assign columns to four groups by `(ChunkPos.x % 2, ChunkPos.z % 2)`. Process groups sequentially; within each group all columns are non-adjacent (≥ 2 chunks apart in both axes) so their ≤14-voxel horizontal spill cannot reach another same-group column. This gives 4 parallel passes with full multi-directional BFS in each — no phase split required. 

Rejected for now because: (a) it requires four sequential rounds even when the world has only a handful of columns, (b) correctness relies on the empirical bound of ≤14 horizontal voxels, which would silently break if `maxLightLevel` or `ChunkSize` changed, and (c) the two-phase approach is more explicit and easier to reason about.

---

## Summary

The two-phase approach is the safest and most parallelism-friendly design for this codebase:

- **Phase 1** (N parallel tasks, one per column): down-only BFS → no cross-column writes → no locks → full parallelism.
- **Phase 2** (1 sequential task): multi-directional BFS from all phase-1 spillover nodes → identical to the current single-threaded implementation → proven correctness.

The net effect: the dominant cost (vertical shaft illumination) runs in parallel across all columns; the minor cost (cave-mouth horizontal spread) remains single-threaded but is a small fraction of total work.
