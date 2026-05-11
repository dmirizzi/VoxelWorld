# Job Inter-Dependency Analysis

## 1. The Sunlight Pipeline — `SunlightSpilloverBuffer`

### How it works today

Three jobs collaborate to compute sunlight, coordinated across two update stages:

| Stage | Job | Role |
|-------|-----|------|
| 3 | `SunlightUpdateJob` | Clears `VoxelWorld.SunlightSpilloverBuffer`, spawns one `SunlightColumnJob` per XZ column and one `SunlightHorizontalSpillJob` |
| 4 | `SunlightColumnJob` (N, parallel) | Propagates sunlight vertically down its column; in `PostExecuteSync` appends its `_localSpillover` list to `world.SunlightSpilloverBuffer` |
| 5 | `SunlightHorizontalSpillJob` (1) | In `PreExecuteSync` reads the full `world.SunlightSpilloverBuffer` as its seed set; propagates light horizontally across column boundaries |

### The coupling

`VoxelWorld` acts as a shared mutable buffer between two job types that have no direct knowledge of each other. The buffer is a **pipeline artifact**: it carries meaning only within this particular sunlight update sequence, yet it lives on the world's core data class as a public property.

```
SunlightColumnJob (×N)
    PostExecuteSync → world.SunlightSpilloverBuffer.AddRange(...)
                              ↓ (accumulated by all N jobs)
SunlightHorizontalSpillJob
    PreExecuteSync  ← world.SunlightSpilloverBuffer (read + snapshot)
```

### Thread safety

The current design is **sound** but the safety guarantee is **implicit and scattered**:

- `PostExecuteSync` runs on the main thread → concurrent writes to the buffer cannot happen
- Stage ordering (`NextJobIsNotHigherStageThanActiveJobs`) ensures all Stage 4 jobs complete before Stage 5 starts → no read/write race

The invariant holds, but nothing in the code at the point of reading/writing makes it obvious. Someone moving the buffer access into `ExecuteAsync` would introduce a real race with no immediate compiler/runtime warning.

---

## 2. Other VoxelWorld-Mediated Job Dependencies

### a. Scheduler round-trips via VoxelWorld

Several `PostExecuteSync` implementations schedule follow-up work by calling methods on `VoxelWorld` instead of calling `worldUpdateScheduler` directly:

| Call site | VoxelWorld method | Ultimately calls |
|-----------|------------------|-----------------|
| `ChunkMeshRebuildJob.PostExecuteSync` | `world.QueueLightFillOnNewChunk(chunkPos)` | `_updateScheduler.AddChunkLightFillUpdateJob(...)` |
| `ChunkLightFillUpdateJob.PostExecuteSync` | `world.QueueChunkForLightMappingUpdate(chunkPos)` | `_updateScheduler.AddChunkLightMappingUpdateJob(...)` |
| `BlockLightUpdateJob.PostExecuteSync` | `world.QueueChunksForLightMappingUpdate(...)` | `_updateScheduler.AddChunkLightMappingUpdateJob(...)` × N |
| `SunlightColumnJob.PostExecuteSync` | `world.QueueChunksForLightMappingUpdate(...)` | same |

Since `PostExecuteSync` already receives `worldUpdateScheduler` as a parameter, these calls create a circular dependency (`Job → VoxelWorld → WorldUpdateScheduler`) for something that could be a direct call (`Job → WorldUpdateScheduler`). This is a cohesion issue: scheduling decisions bleed out of the scheduling layer.

### b. `GetTopMostChunksAndClear()` — stateful world side-effect

`SunlightUpdateJob.PreExecuteSync` calls `world.GetTopMostChunksAndClear()` twice — once to create empty sentinel chunks above new tops, once to collect the actual targets. `_topMostChunks` is maintained continuously by `VoxelWorld.CreateChunk()` and consumed-and-cleared by the pipeline.

This is not a job-to-job data channel, but it is pipeline-specific mutable state on VoxelWorld. The double-clear semantics are fragile: if the job runs again before any new chunks are created, the second call returns an empty list and the job silently no-ops.

### c. `world.GetLightMap()` — shared resource (not a data channel)

All light jobs call `world.GetLightMap()` in `PreExecuteSync` to get the single `LightMap` instance. This is legitimate shared-resource access, not inter-job communication. Thread safety is upheld because each call operates on its own BFS queue, and chunk reservations prevent concurrent writes to the same voxel region.

---

## 3. Proposed Solutions

### Option A — Pass shared reference from the spawning job *(targeted, zero new abstraction)*

`SunlightUpdateJob` already controls the lifetime of both column jobs and the spill job. It can allocate a shared `List<LightNode>` once and inject it into all of them, removing VoxelWorld from the equation entirely.

```csharp
// SunlightUpdateJob.PostExecuteSync
var sharedSpillover = new List<LightNode>();
foreach (var chunk in _topMostChunks)
    worldUpdateScheduler.AddSunlightColumnJob(chunk, sharedSpillover);
worldUpdateScheduler.AddSunlightHorizontalSpillJob(sharedSpillover);

// SunlightColumnJob — constructor receives List<LightNode> sharedSpillover
// PostExecuteSync (main thread — no lock needed):
_sharedSpillover.AddRange(_localSpillover);

// SunlightHorizontalSpillJob — constructor receives List<LightNode> sharedSpillover
// PreExecuteSync (main thread):
_spilloverSeeds = new List<LightNode>(_sharedSpillover);
return _spilloverSeeds.Count > 0;
```

**Thread safety:** `PostExecuteSync` and `PreExecuteSync` both run on the main thread and stage ordering ensures writes precede the read — identical guarantee to today, but now explicit at the call site.

**What changes on VoxelWorld:** `SunlightSpilloverBuffer` is removed entirely.

**Tradeoff:** Requires changing the `AddSunlightColumnJob` / `AddSunlightHorizontalSpillJob` signatures on `WorldUpdateScheduler`. All call sites are internal, so the impact is small.

---

### Option B — Typed output slots on `WorldUpdateScheduler` *(generalized mechanism)*

Add a lightweight typed "output bag" to the scheduler. Jobs write outputs in `PostExecuteSync` and read them in `PreExecuteSync`. Stage ordering already guarantees that writes at stage N precede reads at stage N+1.

```csharp
// WorldUpdateScheduler additions (main-thread only — no locking needed)
private readonly Dictionary<string, object> _jobOutputs = new();

public void SetJobOutput<T>(string key, T value) => _jobOutputs[key] = value;
public void AppendJobOutput<T>(string key, IEnumerable<T> items)
{
    if (!_jobOutputs.TryGetValue(key, out var existing))
        _jobOutputs[key] = new List<T>(items);
    else
        ((List<T>)existing).AddRange(items);
}
public bool TryGetJobOutput<T>(string key, out T value)
{
    if (_jobOutputs.TryGetValue(key, out var obj)) { value = (T)obj; return true; }
    value = default; return false;
}
public void ClearJobOutput(string key) => _jobOutputs.Remove(key);
```

The sunlight pipeline would then use a named slot `"SunlightSpillover"`:

```csharp
// SunlightUpdateJob.PostExecuteSync
worldUpdateScheduler.ClearJobOutput("SunlightSpillover");
...

// SunlightColumnJob.PostExecuteSync
worldUpdateScheduler.AppendJobOutput("SunlightSpillover", _localSpillover);

// SunlightHorizontalSpillJob.PreExecuteSync
worldUpdateScheduler.TryGetJobOutput("SunlightSpillover", out List<LightNode> seeds);
_spilloverSeeds = seeds ?? new List<LightNode>();
```

**Thread safety:** Same guarantee as Option A — all access is on the main thread and stage ordering is enforced by the scheduler.

**Advantage over A:** Generalizes to any future job-to-job data transfer without touching `VoxelWorld`. The slot is owned by the scheduler, which already enforces stage ordering — the invariant and the data live in the same place.

**Tradeoff:** A stringly-typed key is easy to mistype. This can be mitigated with `static readonly` key constants (or `nameof` on a dedicated type) at no runtime cost.

---

### Fix for the scheduler round-trip (Options A and B, complementary)

Regardless of which option is chosen for the spillover buffer, the VoxelWorld scheduling round-trips (section 2a) should be cleaned up. In `PostExecuteSync`, jobs already receive `worldUpdateScheduler` — they can call it directly:

```csharp
// ChunkMeshRebuildJob.PostExecuteSync — before
world.QueueLightFillOnNewChunk(ChunkPos);
// after
worldUpdateScheduler.AddChunkLightFillUpdateJob(ChunkPos);

// ChunkLightFillUpdateJob.PostExecuteSync — before
world.QueueChunkForLightMappingUpdate(ChunkPos);
// after
worldUpdateScheduler.AddChunkLightMappingUpdateJob(ChunkPos);

// BlockLightUpdateJob.PostExecuteSync — before
world.QueueChunksForLightMappingUpdate(_affectedChunks);
// after
foreach (var pos in _affectedChunks) worldUpdateScheduler.AddChunkLightMappingUpdateJob(pos);
```

The corresponding `QueueLightFillOnNewChunk`, `QueueChunkForLightMappingUpdate`, and `QueueChunksForLightMappingUpdate` wrapper methods can then be removed from `VoxelWorld`. This also removes the `_updateScheduler` back-reference from `VoxelWorld`, reducing the circular dependency.

---

## 4. Recommendation

| Step | Action | Removes from VoxelWorld |
|------|--------|------------------------|
| 1 | Apply **Option A** for the spillover buffer | `SunlightSpilloverBuffer` |
| 2 | Apply the scheduler round-trip fix | `QueueLightFillOnNewChunk`, `QueueChunkForLightMappingUpdate`, `QueueChunksForLightMappingUpdate`, `_updateScheduler` field |
| 3 | If more job-to-job transfers appear in the future, promote to **Option B** as a generalization | — |

Steps 1 and 2 together shrink `VoxelWorld`'s responsibilities toward pure world state and eliminate all scheduling-pipeline artifacts from it. Option B is worth adopting only once a second real use case emerges — applying it now would be premature generalization.
