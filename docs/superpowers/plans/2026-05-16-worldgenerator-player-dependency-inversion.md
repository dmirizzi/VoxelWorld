# WorldGenerator → Player Dependency Inversion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove `WorldGenerator`'s direct reference to `PlayerController` by exposing a C# event and a tracked-object registration method, so Player (Game layer) wires itself to WorldGenerator (Core layer), not the other way around.

**Architecture:** `WorldGenerator` gains a `public event Action OnWorldReady` and `public void RegisterTrackedObject(Transform)`. `PlayerController.Start()` calls both, and a new `PlaceInWorld()` handler on `PlayerController` handles spawn positioning — logic moved verbatim from the deleted `WorldGenerator.PlacePlayer()`.

**Tech Stack:** Unity 6 (6000.2.6f2), C#, no external test framework — verification is done via Unity Editor play mode.

---

## Files

| Action | Path |
|--------|------|
| Modify | `Assets/Scripts/Core/WorldGen/WorldGenerator.cs` |
| Modify | `Assets/Scripts/Game/Player/PlayerController.cs` |

---

### Task 1: Strip the Player reference from WorldGenerator

**Files:**
- Modify: `Assets/Scripts/Core/WorldGen/WorldGenerator.cs`

- [ ] **Step 1: Add the public event and registration method**

  Open `WorldGenerator.cs`. After the existing public properties (`WorldGenerated`, `ChunkGenerator`), add:

  ```csharp
  public event Action OnWorldReady;

  public void RegisterTrackedObject(Transform trackedObject) => _trackedObject = trackedObject;
  ```

- [ ] **Step 2: Add the private `_trackedObject` field; remove `_player`**

  In the private fields section at the bottom of the class, replace:

  ```csharp
  private PlayerController _player;
  ```

  with:

  ```csharp
  private Transform _trackedObject;
  ```

- [ ] **Step 3: Remove the `FindObjectOfType<PlayerController>()` call from `Awake()`**

  In `Awake()`, delete this line:

  ```csharp
  _player = FindObjectOfType<PlayerController>();
  ```

- [ ] **Step 4: Replace `PlacePlayer()` call with `OnWorldReady` fire in the `BatchFinished` callback**

  Inside `Awake()`, the `BatchFinished` lambda currently ends with:

  ```csharp
  //Profiler.WriteProfilingResultsToCSV();
  PlacePlayer(Vector3Int.zero);
  WorldGenerated = true;
  ```

  Change it to:

  ```csharp
  //Profiler.WriteProfilingResultsToCSV();
  WorldGenerated = true;
  OnWorldReady?.Invoke();
  ```

- [ ] **Step 5: Replace the three `_player.transform.position` reads with `_trackedObject.position`**

  There are three callsites — replace each:

  **In `PopAllBackloggedChunksWithinGenerationRadius()` (line ~37):**
  ```csharp
  // before
  var sqrDistToPlayer = VoxelPosHelper.GetChunkSqrDistanceToWorldPos(_player.transform.position, chunkPos);
  // after
  var sqrDistToPlayer = VoxelPosHelper.GetChunkSqrDistanceToWorldPos(_trackedObject.position, chunkPos);
  ```

  **In `Update()` (line ~102):**
  ```csharp
  // before
  var currentPlayerChunkPos = VoxelPosHelper.WorldPosToChunkPos(_player.transform.position);
  // after
  var currentPlayerChunkPos = VoxelPosHelper.WorldPosToChunkPos(_trackedObject.position);
  ```

  **In `GenerateChunksAroundCenter()` (line ~145):**
  ```csharp
  // before
  var sqrDistToPlayer = VoxelPosHelper.GetChunkSqrDistanceToWorldPos(_player.transform.position, chunkPos);
  // after
  var sqrDistToPlayer = VoxelPosHelper.GetChunkSqrDistanceToWorldPos(_trackedObject.position, chunkPos);
  ```

- [ ] **Step 6: Delete the `PlacePlayer()` method**

  Remove the entire private method (lines ~158–181):

  ```csharp
  private void PlacePlayer(Vector3Int? startPos = null)
  {
      ...
  }
  ```

- [ ] **Step 7: Commit**

  ```bash
  git add Assets/Scripts/Core/WorldGen/WorldGenerator.cs
  git commit -m "refactor: remove PlayerController dependency from WorldGenerator"
  ```

---

### Task 2: Wire PlayerController to WorldGenerator

**Files:**
- Modify: `Assets/Scripts/Game/Player/PlayerController.cs`

- [ ] **Step 1: Register and subscribe in `Start()`**

  In `PlayerController.Start()`, after the existing initialization lines, add:

  ```csharp
  var worldGenerator = FindObjectOfType<WorldGenerator>();
  worldGenerator.RegisterTrackedObject(transform);
  worldGenerator.OnWorldReady += PlaceInWorld;
  ```

- [ ] **Step 2: Add the `PlaceInWorld()` handler**

  Add this private method to the "External access to player control" section (or a new "World events" private section before private attributes):

  ```csharp
  private void PlaceInWorld()
  {
      var highestY = _voxelWorld.GetHighestVoxelPos(0, 0).Value;
      var spawnVoxelPos = new Vector3Int(0, highestY, 0);
      var worldPos = VoxelPosHelper.GetVoxelTopCenterSurfaceWorldPos(spawnVoxelPos) + Vector3.up * 2;
      _controller.enabled = false;
      transform.position = worldPos;
      _controller.enabled = true;
      SetGravityActive(true);
      UnityEngine.Debug.Log($"Placing player @ {transform.position}");
  }
  ```

  Note: `_controller` and `_voxelWorld` are already assigned in `Start()` before this runs. The event fires after the world batch completes, well after `Start()` has finished.

- [ ] **Step 3: Commit**

  ```bash
  git add Assets/Scripts/Game/Player/PlayerController.cs
  git commit -m "feat: PlayerController registers itself with WorldGenerator and handles own spawn"
  ```

---

### Task 3: Verify in Unity Editor

- [ ] **Step 1: Check for compile errors**

  Open Unity Editor. Wait for script compilation to complete. Check the Console window — there should be zero errors. If `CS0103` or similar errors appear referencing `PlayerController` or `_player` in `WorldGenerator`, a find-replace in Task 1 was missed.

- [ ] **Step 2: Enter Play mode**

  Press Play. Observe:
  - The camera should start disabled (black screen) while chunks load — same as before.
  - Once the initial batch finishes, the camera enables and the player should appear standing on the surface at roughly XZ (0, 0).
  - The Console should print: `Placing player @ (x, y, z)` confirming `PlaceInWorld()` ran.

- [ ] **Step 3: Verify chunk streaming**

  Walk away from the spawn point. New chunks should continue generating around the player — confirms `_trackedObject.position` is tracking the player correctly in `Update()` and `GenerateChunksAroundCenter()`.

- [ ] **Step 4: Verify backlog**

  If any world features (torches, trees) were generated in chunks outside the load radius, walk toward them — they should load in correctly, confirming `PopAllBackloggedChunksWithinGenerationRadius()` still filters by tracked position.
