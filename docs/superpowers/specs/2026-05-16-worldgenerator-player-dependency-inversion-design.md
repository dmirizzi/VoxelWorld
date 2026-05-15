# Design: WorldGenerator → Player Dependency Inversion

**Date:** 2026-05-16  
**Status:** Approved

## Problem

`WorldGenerator` (Core layer) currently holds a direct reference to `PlayerController` (Game layer), violating the Core/Game boundary defined in CLAUDE.md. It uses that reference for two distinct purposes:

1. **Placement** — after the initial world batch finishes, it calls `PlacePlayer()` which repositions the player and enables gravity.
2. **Tracking** — during `Update()` and chunk generation, it reads `_player.transform.position` to determine which chunks to load.

## Goal

Invert both dependencies so `WorldGenerator` knows nothing about `PlayerController`. The Game layer registers itself with Core, not the other way around.

## Design

### WorldGenerator (Core/WorldGen/WorldGenerator.cs)

**Remove:**
- `_player: PlayerController` field
- `FindObjectOfType<PlayerController>()` call in `Awake()`
- `PlacePlayer()` method

**Add to public interface:**
```csharp
public event Action OnWorldReady;
public void RegisterTrackedObject(Transform trackedObject)
```

`RegisterTrackedObject` stores the provided `Transform` as `_trackedObject`. All reads of `_player.transform.position` are replaced with `_trackedObject.position`.

In the `BatchFinished` callback, after restoring camera/vsync/framerate/job settings, fire `OnWorldReady?.Invoke()` instead of calling `PlacePlayer()`.

**`_trackedObject` usage sites:**
- `Update()` — computing `currentPlayerChunkPos`
- `PopAllBackloggedChunksWithinGenerationRadius()` — computing `sqrDistToPlayer`
- `GenerateChunksAroundCenter()` — computing `sqrDistToPlayer` per candidate chunk

### PlayerController (Game/Player/PlayerController.cs)

**In `Start()`**, find the `WorldGenerator`, register itself, and subscribe to the event:
```csharp
var worldGenerator = FindObjectOfType<WorldGenerator>();
worldGenerator.RegisterTrackedObject(transform);
worldGenerator.OnWorldReady += OnWorldReady;
```

**Add `OnWorldReady()` handler** with the spawn logic previously in `WorldGenerator.PlacePlayer()`:
1. Get spawn position via `_voxelWorld.GetHighestVoxelPos(0, 0)` (same origin as before)
2. Convert to world position via `VoxelPosHelper.GetVoxelTopCenterSurfaceWorldPos(pos) + Vector3.up * 2`
3. Disable `_controller`, set `transform.position`, re-enable `_controller`
4. Call `SetGravityActive(true)`

`PlayerController` already holds `_voxelWorld` and `_controller` — no new fields needed.

## Dependency Direction After Change

```
Before: WorldGenerator (Core) → PlayerController (Game)  [wrong direction]
After:  PlayerController (Game) → WorldGenerator (Core)  [correct direction]
```

## Behavior Preserved

- Gravity remains inactive until `OnWorldReady` fires (same as today — `_gravityActive` defaults to `false`)
- Player spawns at the highest solid surface at XZ origin
- Camera and performance settings are restored before the event fires, so the player sees the world on spawn
