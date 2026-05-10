# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

VoxelWorld is a Unity 6 (6000.2.6f2) voxel engine built with URP 17.2.0. It implements a chunk-based infinite world with flood-fill lighting (RGB + sunlight), async job scheduling, and a data-driven block type system.

## Build & Run

Open in Unity Hub with Unity 6000.2.6f2. There are no CLI build scripts — all building and testing is done through the Unity Editor. The single scene is `Assets/Scenes/SampleScene.unity`.

There are no automated tests; validation is done by running the scene in Play mode.

## File Structure

```
Assets/
├── Resources/
│   ├── BlockTypes.json              # Block metadata for all 11 block types
│   ├── ItemData.json                # Item pickup/inventory data
│   ├── Models/
│   │   ├── Door_Bottom.obj          # Door mesh (lower half)
│   │   ├── Door_Top.obj             # Door mesh (upper half)
│   │   └── Ladder.obj               # Ladder mesh
│   └── Prefabs/
│       ├── Torch.prefab             # Torch GameObject (light source)
│       └── Blob.prefab
├── Scenes/
│   └── SampleScene.unity
└── Scripts/
    ├── BlockTypes/
    │   ├── BlockData.cs             # Serializable block render config
    │   ├── BlockDataRepository.cs   # Static loader for BlockTypes.json
    │   ├── BlockFaceHelper.cs       # Face ↔ vector conversions and rotations
    │   ├── BlockTypeBase.cs         # Abstract base for all block behaviors
    │   ├── BlockTypeRegistry.cs     # ID → implementation mapping
    │   ├── BlockProperties/
    │   │   ├── IBlockProperty.cs           # Interface: bit length + serializer factory
    │   │   ├── IBlockPropertySerializer.cs # Interface: pack/unpack ushort bits
    │   │   ├── SerializationHelper.cs      # Bit mask/extract utilities
    │   │   ├── PlacementFaceProperty.cs    # 3-bit: which face block was placed on
    │   │   └── DoorStateProperty.cs        # 2-bit: IsTopPart + IsOpen flags
    │   └── Types/
    │       ├── DoorBlockType.cs            # Two-block door with open/close
    │       ├── LadderBlockType.cs          # Climbable; placement-face mesh rotation
    │       ├── TorchBlockType.cs           # Light-emitting GameObject
    │       └── WedgeBlockType.cs           # Half-block wedge geometry
    ├── Diagnostics/
    │   ├── GizmosDispatcher.cs
    │   └── WorldDbg.cs
    ├── Items/
    │   ├── ItemData.cs
    │   ├── ItemDataRepository.cs
    │   └── Torch.cs                 # Holdable torch item
    ├── Misc/
    │   ├── DebugHelper.cs
    │   ├── GitHelper.cs
    │   ├── JsonConverters.cs        # Custom Newtonsoft converters (enums, colors)
    │   ├── PriorityQueue.cs
    │   ├── Profiler.cs              # Named timer wrappers
    │   ├── ReadOnly3DArray.cs
    │   └── SceneLayers.cs
    ├── Player/
    │   ├── IPlayerHoldable.cs
    │   ├── PlayerActionBarController.cs
    │   ├── PlayerController.cs
    │   └── PlayerHoldingController.cs
    ├── Scheduling/
    │   ├── BackloggedVoxelCreationJob.cs
    │   ├── BlockLightUpdateJob.cs
    │   ├── ChunkGenerationJob.cs
    │   ├── ChunkLightFillUpdateJob.cs
    │   ├── ChunkLightMappingUpdateJob.cs
    │   ├── ChunkRebuildJob.cs
    │   ├── ChunkVoxelCreationJob.cs
    │   ├── IWorldUpdateJob.cs
    │   ├── JobPriority.cs
    │   ├── SunlightUpdateJob.cs
    │   └── WorldUpdateScheduler.cs
    ├── Voxels/
    │   ├── Chunk.cs
    │   ├── ChunkBuilder.cs
    │   ├── ChunkMesh.cs
    │   ├── ChunkSerializer.cs
    │   ├── LightMap.cs
    │   ├── VoxelBuildHelper.cs
    │   ├── VoxelCollider.cs
    │   ├── VoxelInfo.cs
    │   ├── VoxelMesh.cs
    │   ├── VoxelPosHelper.cs
    │   └── VoxelWorld.cs
    ├── WorldGen/
    │   ├── ChunkGenerator.cs
    │   ├── ChunkUpdateBuilder.cs
    │   ├── VoxelCreationAction.cs
    │   └── WorldGenerator.cs
    └── DayNightController.cs
```

## Architecture

### Coordinate Systems

Three coordinate spaces are used throughout:
- **World space**: Unity float positions
- **Global voxel**: integer grid, no chunk boundaries (converted via `VoxelPosHelper`)
- **Chunk-local**: 0–15 per axis within a chunk

`VoxelPosHelper` converts between these using bit shifts (`>> 4` for chunk pos, `& 0xF` for local) since chunks are 16³. Negative coordinates require special handling — use `VoxelPosHelper`, never raw division.

### Chunk System (`Assets/Scripts/Voxels/`)

- `Chunk.cs` — 16×16×16 `ushort[,,]` for block IDs; separate `ushort[,,]` for packed light (4 bits × 4 channels: R, G, B, sunlight). Holds a 3×3×3 array of neighbor chunk references for cross-boundary queries.
- `VoxelWorld.cs` — central world manager; owns all `Chunk` instances in a `Dictionary<Vector3Int, Chunk>`, provides public voxel get/set API, tracks topmost chunks per XZ column for sunlight.
- `LightMap.cs` — flood-fill light engine; BFS queues per channel; handles add/remove/sunlight; crosses chunk boundaries via neighbor references.

### Job Pipeline (`Assets/Scripts/Scheduling/`)

`WorldUpdateScheduler` runs a priority queue of jobs across ordered stages:

| Stage | Jobs |
|-------|------|
| 0 | `ChunkGenerationJob` |
| 1 | `ChunkVoxelCreationJob` |
| 2 | `BackloggedVoxelCreationJob` |
| 3 | `SunlightUpdateJob` (coordinator — no-op async, spawns column + spill jobs) |
| 4 | `SunlightColumnJob` (one per XZ column, parallel), `ChunkRebuildJob` |
| 5 | `SunlightHorizontalSpillJob` (single, sequential) |
| 6 | `BlockLightUpdateJob`, `ChunkLightFillUpdateJob` |
| 7 | `ChunkLightMappingUpdateJob` |

Each job has three phases: `PreExecuteSync()` (main thread, reserves chunks), `ExecuteAsync()` (background `Task`), `PostExecuteSync()` (main thread, queues follow-up jobs). Chunk reservations prevent concurrent modification. Within a stage, jobs are prioritized by squared distance to the player.

### World Generation (`Assets/Scripts/WorldGen/`)

`WorldGenerator` manages the load radius around the player (default: 4 chunks = 64 voxels). `ChunkGenerator` produces a `ChunkUpdate` (list of `VoxelCreationAction`) which is applied by `ChunkVoxelCreationJob`. Terrain generation is currently a flat test plate — Perlin noise infrastructure is present but commented out.

### Block Type System (`Assets/Scripts/BlockTypes/`)

**Configuration — `BlockTypes.json`**

`BlockDataRepository` loads `Assets/Resources/BlockTypes.json` once at startup. The JSON array index is the block's `ushort` ID. Each entry produces a `BlockData` object:

| Field | Type | Purpose |
|-------|------|---------|
| `Name` | string | Key for `BlockDataRepository.GetBlockTypeId()` |
| `RenderType` | enum | `Voxel`, `CustomMesh`, or `GameObject` |
| `OpaqueFaces` | `BlockFaceSelector[]` | Which faces occlude neighbors (drives face culling) |
| `Transparent` | bool | Routes to transparent mesh pass |
| `LightColor` | hex string | RGB emitted light color (light-source blocks only) |
| `FaceTextureTileCoords` | map | Face selector → `[x, y]` tile in texture atlas |
| `HeightOffset` | float | Y-offset of top face (e.g. `0.075` for water) |

Current block IDs (array index = ID):

| ID | Name | RenderType | Notes |
|----|------|-----------|-------|
| 0 | Empty | Voxel | Transparent air placeholder |
| 1 | Grass | Voxel | Different top / side / bottom textures |
| 2 | Dirt | Voxel | |
| 3 | Water | Voxel | Transparent, 0.075 height offset |
| 4 | Cobblestone | Voxel | |
| 5 | Torch | GameObject | LightColor FFB077 (warm orange) |
| 6 | CobblestoneWedge | CustomMesh | |
| 7 | Door | CustomMesh | Two-block; top+bottom halves |
| 8 | Ladder | CustomMesh | |
| 9 | Log | Voxel | Different top / side textures |
| 10 | Leaves | Voxel | Transparent |

**`BlockTypeBase` — lifecycle hooks**

Subclass `BlockTypeBase` to give a block ID custom behavior. `BlockTypeRegistry` maps IDs to singleton instances; IDs with no registered instance behave as plain voxels.

| Method | Called when | Return |
|--------|-------------|--------|
| `OnPlace(world, chunk, globalPos, localPos, placementFace, lookDir)` | Player attempts placement — before voxel is written | `false` cancels |
| `OnRemove(world, chunk, globalPos, localPos)` | Player attempts removal — aux data still intact | `false` cancels |
| `OnUse(world, globalPos, lookDir)` | Player interacts with the block | `true` if consumed |
| `OnChunkBuild(world, chunk, globalPos, localPos)` | Each chunk (re)build; for `GameObject` blocks | — |
| `OnChunkVoxelMeshBuild(world, chunk, voxelType, globalPos, localPos, chunkMesh)` | Each chunk (re)build; for `CustomMesh` blocks | — |
| `OnTouchStart/End(world, globalPos, player)` | Player enters / leaves the voxel | — |
| `GetForwardFace(world, globalPos)` *(abstract)* | `ChunkBuilder` face-culling query | `BlockFace` |

`GetForwardFace` is called by `ChunkBuilder` to determine which face of a rotated block points outward, so neighbor occlusion culling remains correct after rotation.

Registered implementations:

| ID | Class | Properties packed into aux `ushort` |
|----|-------|-------------------------------------|
| 5 | `TorchBlockType` | `PlacementFaceProperty` (3 bits) |
| 6 | `WedgeBlockType` | `PlacementFaceProperty` (3 bits) |
| 7 | `DoorBlockType` | `PlacementFaceProperty` (3 bits) + `DoorStateProperty` (2 bits) |
| 8 | `LadderBlockType` | `PlacementFaceProperty` (3 bits) |

**Auxiliary data & `IBlockProperty`**

Each voxel may carry one `ushort` (16 bits) of auxiliary data stored in `Chunk._blockAuxiliaryData` (`Dictionary<Vector3Int, ushort>`), accessed through `VoxelWorld.GetVoxelAuxiliaryData` / `SetVoxelAuxiliaryData`.

Block properties are packed into that `ushort` as bit fields. `IBlockProperty` declares:
- `SerializedLengthInBits` — bits consumed by this property
- `GetSerializer<T>()` — returns an `IBlockPropertySerializer<T>` that packs/unpacks at a caller-supplied bit offset

`BlockTypeBase` computes each property's bit offset cumulatively from the constructor argument order and exposes three protected helpers:

```csharp
protected T   GetProperty<T>(VoxelWorld world, Vector3Int globalPos)
protected void SetProperty<T>(VoxelWorld world, Vector3Int globalPos, T property)
protected void UpdateProperty<T>(VoxelWorld world, Vector3Int globalPos, Func<T, T> updateFunc)
```

`SerializationHelper.ExtractBits` / `OverwriteBits` provide the underlying bit manipulation.

Built-in properties:

| Class | Bits | Stored data |
|-------|------|-------------|
| `PlacementFaceProperty` | 3 | `BlockFace PlacementFace` — which face the block was placed against |
| `DoorStateProperty` | 2 | `bool IsTopPart` (bit 0) + `bool IsOpen` (bit 1) |

Aux data lifecycle:
1. **`OnPlace`** — `SetProperty<T>()` writes bits into the chunk's aux-data dict.
2. **Chunk (re)build** — `OnChunkBuild` / `OnChunkVoxelMeshBuild` call `GetProperty<T>()` to reconstruct state from stored bits and drive mesh or GameObject creation.
3. **`OnRemove`** — runs with aux data still intact (final state readable), then VoxelWorld clears the entry.

### Mesh Building (`Assets/Scripts/Voxels/ChunkBuilder.cs`)

Each chunk produces two meshes: solid and transparent. Smooth lighting samples light values at the 8 corners of each voxel and writes them as vertex colors (RGBA = R, G, B, sunlight channels). `ChunkLightMappingUpdateJob` handles the light-to-vertex-color pass separately from geometry.

## Key Patterns & Conventions

- **Never modify a chunk from `ExecuteAsync()`** — all chunk writes must happen in `PreExecuteSync()` or `PostExecuteSync()`.
- **Chunk reservations are mandatory** before any job that reads or writes voxel/light data; released in `PostExecuteSync()`.
- **Block IDs**: `0` is always air. Block type `ushort` IDs match indices in `BlockTypes.json`.
- **Light packing**: `SSSS RRRR GGGG BBBB` — sunlight in the high nibble of the high byte, RGB in low nibbles.
- **Profiling**: `Assets/Scripts/Misc/Profiler.cs` wraps named timers; active profiling calls are present in scheduler and light code — keep them when modifying those paths.
