# Sunlight Column Optimization Plan

## Goal

Move intra-column indirect sunlight propagation from the single-threaded `SunlightHorizontalSpillJob` into the parallel `SunlightColumnJob`, so the spillover job only handles cross-column light bleeding.

## Background

Each `SunlightColumnJob` exclusively owns all voxels where `chunkX == topChunk.ChunkPos.x && chunkZ == topChunk.ChunkPos.z` across all Y. Horizontal propagation that stays within those XZ bounds is safe to run in parallel. Currently all horizontal propagation is deferred to the single-threaded spillover job, which receives a seed node for every sky-exposed voxel.

---

## Changes

### 1. `PropagateLightNodes` — add column-boundary mode

Add two optional parameters:

```csharp
private void PropagateLightNodes(
    Queue<LightNode> lightNodes,
    int channel,
    HashSet<Vector3Int> visitedNodes,
    HashSet<Vector3Int> visitedChunks,
    bool isSunlight = false,
    Vector3Int? columnChunkXZ = null,       // restrict propagation to this XZ column
    HashSet<Vector3Int> columnSpilloverOut = null  // collects boundary nodes for spillover
)
```

**Behaviour change when `columnChunkXZ` is set:**

Before calling `TryGetNeighboringChunkVoxel` (i.e. when `neighborLocalPos` is out of the current chunk's bounds), check whether the crossing is in X or Z. If so, it's a column-boundary exit — add the current node to `columnSpilloverOut` and skip, without resolving the neighbour chunk at all. Y-crossings (up/down into the next chunk of the same column) are still resolved normally.

```
if (!lightNode.Chunk.LocalVoxelPosIsInChunk(neighborLocalPos))
{
    if (columnChunkXZ != null && (neighborDir.x != 0 || neighborDir.z != 0))
    {
        columnSpilloverOut.Add(lightNode.GlobalPos); // boundary node, already lit
        continue;
    }
    if (!lightNode.Chunk.TryGetNeighboringChunkVoxel(...)) continue;
}
```

The current (in-column) node is already lit at the correct level, so it serves as a valid seed for the subsequent spillover job. No other changes to propagation logic.

---

### 2. `UpdateSunlightColumnVertical` — seed BFS from all straight-down nodes

The straight-down loop is unchanged. All nodes it visits become BFS seeds for the intra-column pass.

**Why not obstacle-nodes only:** every sky-15 node is a valid level-14 source for its horizontal neighbours. Seeding only from the bottom obstacle node would cause shadow-zone voxels adjacent to mid-column sky nodes to receive attenuated values (14 - distance_from_bottom) instead of the correct 14. The BFS from sky-15 nodes into adjacent sky-15 neighbours immediately short-circuits via `neighborLightLevel + 2 > currentLightLevel` (17 > 15), so including all sky nodes as seeds is cheap on open terrain and correct everywhere.

```
// straight-down loop → populates allColumnNodes (same nodes currently go to spilloverOut)
var spilloverSet = new HashSet<Vector3Int>();
PropagateLightNodes(
    new Queue<LightNode>(allColumnNodes),
    Chunk.SunlightChannel,
    new HashSet<Vector3Int>(),
    new HashSet<Vector3Int>(),
    isSunlight: true,
    columnChunkXZ: topChunk.ChunkPos,
    columnSpilloverOut: spilloverSet
);
spilloverOut.AddRange(spilloverSet.Select(...));
```

`spilloverOut` now receives only XZ-boundary nodes instead of every sky-exposed voxel.

---

### 3. No changes required

- `SunlightColumnJob` — job structure, `ExecuteAsync`, `PostExecuteSync` unchanged.
- `PropagateSpilloverNodes` / `SunlightHorizontalSpillJob` — unchanged; they already run a full 6-dir BFS from whatever seeds they receive.
- All other `PropagateLightNodes` call sites — the new parameters are optional, existing calls are unaffected.

---

## Expected Outcome

| | Before | After |
|---|---|---|
| Spillover seeds | Every sky-exposed voxel | Only XZ column-edge voxels |
| Spillover job work | Full indirect BFS for all columns | Only cross-column light bleeding |
| Column job work | Straight-down only | Straight-down + intra-column BFS (parallel) |

The spillover job's BFS shrinks proportionally to how much indirect lighting is contained within single columns (caves, overhangs, interiors all benefit).

---

## Risk / Edge Cases

- **Visited set reuse**: the downward pass populates a `visitedByChunk` array that the horizontal pass must also use, to avoid re-processing already-lit voxels. Pass it through rather than creating a fresh one.
- **`LightNode` reconstruction from `HashSet<Vector3Int>`**: storing full `LightNode` structs in the spillover set (keyed by `GlobalPos`) avoids needing to look up chunk/localPos again at merge time.
- **Correctness**: the spillover job re-propagating from boundary nodes that are already correctly lit is safe — `neighborLightLevel + 2 > currentLightLevel` guards immediately skip them if neighbors are already at the right level.
