using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class WorldGenTests
{
    // Unity's Perlin noise returns 0 at integer lattice points, so terrain height at global (0,0)
    // works out to roughly -261 (chunk y=-17). Placing the player at y=-260 ensures chunk y=-17
    // is within a generation radius of 3, so terrain is actually generated.
    private const float PlayerStartY = -260f;

    [UnityTest]
    public IEnumerator GeneratedWorld_SurfaceVoxels_HaveMaxSunlightAbove()
    {
        SetUpScene(generationRadius: 4, out _, out var worldGo, out var generator);

        yield return WaitForGeneration(generator, timeoutSeconds: 120f);

        AssertSurfaceSunlight(worldGo.GetComponent<VoxelWorld>());
    }

    [UnityTest]
    public IEnumerator GeneratedWorld_AfterPlayerMove_NewSurfaceVoxels_HaveMaxSunlightAbove()
    {
        const int generationRadius = 4;

        SetUpScene(generationRadius, out var playerGo, out var worldGo, out var generator);

        yield return WaitForGeneration(generator, timeoutSeconds: 120f);

        // WorldGenerator.PlacePlayer (called inside BatchFinished) sets gravity active on the
        // player. Disable it immediately so the player doesn't fall through terrain — chunk mesh
        // colliders won't be built correctly in the test scene because TextureAtlasMaterial is
        // null, so nothing will stop the player from falling indefinitely and triggering a
        // continuous stream of chunk update batches.
        playerGo.GetComponent<PlayerController>().SetGravityActive(false);

        // --- Move player to trigger secondary generation ---

        var world = worldGo.GetComponent<VoxelWorld>();

        // 6 chunks (96 voxels) in X: far enough to produce new chunks on the leading edge of
        // the second generation sphere, while still being inside the initial radius-10 sphere
        // so we can resolve the surface height before moving.
        const int newGlobalX = generationRadius * VoxelInfo.ChunkSize;
        const int newGlobalZ = 0;

        int? newSurfaceY = world.GetHighestVoxelPos(newGlobalX, newGlobalZ);
        Assert.IsTrue(newSurfaceY.HasValue,
            $"Could not resolve surface at ({newGlobalX},{newGlobalZ}) — " +
            "position may be outside the initial generation sphere");

        // Place the player just above the surface so WorldGenerator sees a chunk-position change
        // of 6 (> 1 threshold) and fires the secondary batch. The 1.5-unit offset keeps the
        // CharacterController out of the surface voxel itself.
        playerGo.transform.position = new Vector3(
            newGlobalX + 0.5f,
            newSurfaceY.Value + 1.5f,
            newGlobalZ + 0.5f);

        var scheduler = worldGo.GetComponent<WorldUpdateScheduler>();
        yield return WaitForBatch(scheduler, timeoutSeconds: 120f);

        AssertSurfaceSunlight(world);
    }

    // -------------------------------------------------------------------------
    // Scene setup
    // -------------------------------------------------------------------------

    private static void SetUpScene(
        int generationRadius,
        out GameObject playerGo,
        out GameObject worldGo,
        out WorldGenerator generator)
    {
        // WorldGenerator.Awake fetches Camera.main, disables it during load, and re-enables it
        // once the batch finishes.
        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        cameraGo.AddComponent<Camera>();

        // Player must exist before any world-component Awake fires so FindObjectOfType<PlayerController>
        // succeeds. PlayerController.Start also does GameObject.Find("Main Camera"), so the name
        // must match exactly.
        playerGo = new GameObject("Player");
        playerGo.AddComponent<CharacterController>();
        playerGo.AddComponent<PlayerController>();
        playerGo.transform.position = new Vector3(0f, PlayerStartY, 0f);

        // Create the world GO inactive so all three MonoBehaviours can be added before any Awake
        // fires. Each calls FindObjectOfType on the other two in Awake, so all three must be
        // present in the scene at the moment any single Awake runs.
        worldGo = new GameObject("World");
        worldGo.SetActive(false);
        worldGo.AddComponent<VoxelWorld>();
        worldGo.AddComponent<WorldUpdateScheduler>();
        generator = worldGo.AddComponent<WorldGenerator>();
        generator.ChunkGenerationRadius = generationRadius;
        worldGo.SetActive(true); // triggers Awake on all three in component order
    }

    // -------------------------------------------------------------------------
    // Wait helpers
    // -------------------------------------------------------------------------

    // WorldGenerator.WorldGenerated is set inside the BatchFinished callback, after the full
    // generation + sunlight pipeline completes (ChunkGen → VoxelCreation → Sunlight → LightMapping).
    private static IEnumerator WaitForGeneration(WorldGenerator generator, float timeoutSeconds)
    {
        float elapsed = 0f;
        while (!generator.WorldGenerated && elapsed < timeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        Assert.IsTrue(generator.WorldGenerated,
            $"World generation did not complete within {timeoutSeconds}s");
    }

    // Subscribes to the next BatchFinished event on the scheduler and waits for it to fire.
    // Subscribe before yielding so WorldGenerator.Update() cannot start and finish a batch
    // in the gap between yielding and subscribing.
    private static IEnumerator WaitForBatch(WorldUpdateScheduler scheduler, float timeoutSeconds)
    {
        bool done = false;
        void OnBatchFinished() => done = true;
        scheduler.BatchFinished += OnBatchFinished;

        float elapsed = 0f;
        while (!done && elapsed < timeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        scheduler.BatchFinished -= OnBatchFinished;
        Assert.IsTrue(done, $"Secondary batch did not complete within {timeoutSeconds}s");
    }

    // -------------------------------------------------------------------------
    // Assertion
    // -------------------------------------------------------------------------

    private static void AssertSurfaceSunlight(VoxelWorld world)
    {
        var (minBound, maxBound) = world.GetWorldBoundaries();

        int surfaceCount = 0;
        int failureCount = 0;

        for (int gx = minBound.x; gx < maxBound.x; gx++)
        {
            for (int gz = minBound.z; gz < maxBound.z; gz++)
            {
                int? surfaceY = world.GetHighestVoxelPos(gx, gz);
                if (!surfaceY.HasValue) continue;

                // SunlightUpdateJob creates an empty chunk above each topmost chunk as a sky seed,
                // so the chunk above the surface should always be present after generation.
                var abovePos = new Vector3Int(gx, surfaceY.Value + 1, gz);
                var aboveChunk = world.GetChunkFromVoxelPosition(abovePos);
                if (aboveChunk == null) continue;

                surfaceCount++;

                var localPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(abovePos);
                var sunlight = aboveChunk.GetLightChannelValue(localPos, Chunk.SunlightChannel);

                if (sunlight != 15)
                {
                    failureCount++;
                    if (failureCount <= 3)
                        Assert.AreEqual(15, sunlight,
                            $"Sunlight above surface at ({gx},{surfaceY},{gz})");
                }
            }
        }
        Debug.Log("Checked sunlight above " + surfaceCount + " surface voxels");

        Assert.Greater(surfaceCount, 0,
            "No surface voxels with a generated chunk above were found — " +
            "verify that PlayerStartY places the player within the terrain's chunk y range");
        Assert.AreEqual(0, failureCount,
            $"{failureCount}/{surfaceCount} surface columns lacked full sunlight above them");
    }
}
