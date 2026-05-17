using System.Collections.Generic;
using UnityEngine;

// Drop onto an empty GameObject in a prototyping scene.
// Assign VoxelCullerShader and VoxelInstancedShader in the Inspector (same assets as WorldGenPrototypingController).
// Press R in Play mode or use right-click → Regenerate to rebuild.
public class DungeonGenPrototypingController : PrototypingControllerBase
{
    [Header("Dungeon Entry Point")]
    public int Seed = 42;
    [Tooltip("X/Z position of the dungeon entry (world space voxel coordinates).")]
    public int EntryX;
    public int EntryZ;

    [Header("Grid")]
    [Tooltip("Half-size of the voxel grid per axis. Increase if the dungeon gets clipped.")]
    public int MaxGridHalfSize = 256;

    [Header("Dungeon Parameters")]
    public DungeonParams Params = DungeonParams.Default;

    [Header("Rendering")]
    public ComputeShader VoxelCullerShader;
    public Shader        VoxelInstancedShader;

    // 0 = air (skipped), 1 = cobblestone, 2 = torch, 3 = door, 4 = ladder, 5 = wedge, 6 = grass, 7 = dirt
    private static readonly Color[] DisplayColors =
    {
        Color.clear,
        new(0.50f, 0.50f, 0.50f), // 1 cobblestone
        new(1.00f, 0.70f, 0.20f), // 2 torch
        new(0.40f, 0.25f, 0.10f), // 3 door
        new(0.70f, 0.50f, 0.30f), // 4 ladder
        new(0.60f, 0.60f, 0.60f), // 5 wedge
        new(0.30f, 0.60f, 0.20f), // 6 grass
        new(0.50f, 0.35f, 0.20f), // 7 dirt
    };

    private readonly VoxelPrototypingRenderer _renderer = new();

    private float _regenCountdown = -1f;
    private const float RegenDelay = 0.4f;

    void OnEnable()
    {
        if (Application.isPlaying)
            Regenerate();
    }

    void OnDisable() => _renderer.Release();

    void OnValidate()
    {
        if (!Application.isPlaying) return;
        _regenCountdown = RegenDelay;
    }

    void Update()
    {
        _renderer.Draw();

        if (Input.GetKeyDown(KeyCode.R))
            Regenerate();

        if (_regenCountdown >= 0f)
        {
            _regenCountdown -= Time.deltaTime;
            if (_regenCountdown < 0f)
                Regenerate();
        }
    }

    [ContextMenu("Regenerate")]
    public void Regenerate()
    {
        _renderer.Release();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var heightSampler = new TerrainHeightSampler(Seed);
        int surfaceY      = heightSampler.GetHeight(EntryX, EntryZ);
        var globalEntry   = new Vector3Int(EntryX, surfaceY, EntryZ);
        var rng           = new System.Random(Seed);

        var chunkPos = VoxelPosHelper.GlobalVoxelPosToChunkPos(globalEntry);
        var builder  = new ChunkUpdateBuilder(chunkPos);

        new DungeonGenerator(heightSampler, Params).Generate(builder, globalEntry, rng);

        sw.Stop();
        Debug.Log($"[DungeonPrototyping] Generation: {sw.Elapsed.TotalMilliseconds:F0} ms  entry=({globalEntry.x},{globalEntry.y},{globalEntry.z})  Seed={Seed}");

        var voxels = CollectVoxels(builder);

        if (voxels.Count == 0)
        {
            Debug.LogWarning("[DungeonPrototyping] No voxels generated.");
            return;
        }

        sw.Restart();
        var bounds = BuildAndDispatch(voxels);
        sw.Stop();
        Debug.Log($"[DungeonPrototyping] Grid + GPU dispatch: {sw.Elapsed.TotalMilliseconds:F0} ms");

        FrameCamera(bounds);
    }

    private Dictionary<Vector3Int, ushort> CollectVoxels(ChunkUpdateBuilder builder)
    {
        var update = builder.GetChunkUpdate();
        var result = new Dictionary<Vector3Int, ushort>();

        var chunkBase = VoxelPosHelper.ChunkPosToGlobalChunkBaseVoxelPos(update.ChunkPos);
        for (int y = 0; y < VoxelInfo.ChunkSize; y++)
        for (int z = 0; z < VoxelInfo.ChunkSize; z++)
        for (int x = 0; x < VoxelInfo.ChunkSize; x++)
            result[chunkBase + new Vector3Int(x, y, z)] = update.VoxelData[x, y, z];

        foreach (var (backlogChunkPos, actions) in update.Backlog)
        {
            var backlogBase = VoxelPosHelper.ChunkPosToGlobalChunkBaseVoxelPos(backlogChunkPos);
            foreach (var action in actions)
                result[backlogBase + action.LocalVoxelPos] = action.Type;
        }

        return result;
    }

    private Bounds BuildAndDispatch(Dictionary<Vector3Int, ushort> voxels)
    {
        ushort cobblestoneId = BlockDataRepository.GetBlockTypeId("Cobblestone");
        ushort torchId       = BlockDataRepository.GetBlockTypeId("Torch");
        ushort doorId        = BlockDataRepository.GetBlockTypeId("Door");
        ushort ladderId      = BlockDataRepository.GetBlockTypeId("Ladder");
        ushort wedgeId       = BlockDataRepository.GetBlockTypeId("CobblestoneWedge");
        ushort grassId       = BlockDataRepository.GetBlockTypeId("Grass");
        ushort dirtId        = BlockDataRepository.GetBlockTypeId("Dirt");

        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
        int minZ = int.MaxValue, maxZ = int.MinValue;

        foreach (var (pos, type) in voxels)
        {
            if (type == 0) continue;
            if (pos.x < minX) minX = pos.x; if (pos.x > maxX) maxX = pos.x;
            if (pos.y < minY) minY = pos.y; if (pos.y > maxY) maxY = pos.y;
            if (pos.z < minZ) minZ = pos.z; if (pos.z > maxZ) maxZ = pos.z;
        }

        const int margin = 2;
        int rawSideX = maxX - minX + 2 * margin + 1;
        int rawSideY = maxY - minY + 2 * margin + 1;
        int rawSideZ = maxZ - minZ + 2 * margin + 1;

        int cap   = MaxGridHalfSize * 2;
        int sideX = Mathf.Min(rawSideX, cap);
        int sideY = Mathf.Min(rawSideY, cap);
        int sideZ = Mathf.Min(rawSideZ, cap);

        if (rawSideX > cap || rawSideY > cap || rawSideZ > cap)
            Debug.LogWarning($"[DungeonPrototyping] Dungeon exceeds grid ({rawSideX}×{rawSideY}×{rawSideZ}). Increase MaxGridHalfSize. Clipping applied.");

        var gridMin = new Vector3Int(minX - margin, minY - margin, minZ - margin);

        int total = sideX * sideY * sideZ;
        var grid  = new uint[total];

        foreach (var (pos, type) in voxels)
        {
            if (type == 0) continue;
            var lp = pos - gridMin;
            if ((uint)lp.x >= (uint)sideX || (uint)lp.y >= (uint)sideY || (uint)lp.z >= (uint)sideZ) continue;
            int idx = lp.x + lp.z * sideX + lp.y * sideX * sideZ;
            grid[idx] = BlockToDisplayIndex(
                type, cobblestoneId, torchId, doorId, ladderId, wedgeId, grassId, dirtId);
        }

        _renderer.Dispatch(VoxelCullerShader, grid, sideX, sideY, sideZ, gridMin);
        uint instances = _renderer.SetupRendering(VoxelInstancedShader, DisplayColors);
        Debug.Log($"[DungeonPrototyping] Grid {sideX}×{sideY}×{sideZ}  WorldMin=({gridMin.x},{gridMin.y},{gridMin.z})  Surface={instances:N0}");

        var worldCenter = new Vector3(gridMin.x + sideX * 0.5f, gridMin.y + sideY * 0.5f, gridMin.z + sideZ * 0.5f);
        return new Bounds(worldCenter, new Vector3(sideX, sideY, sideZ));
    }

    private static uint BlockToDisplayIndex(
        ushort type,
        ushort cobblestoneId, ushort torchId, ushort doorId,
        ushort ladderId, ushort wedgeId, ushort grassId, ushort dirtId)
    {
        if (type == cobblestoneId) return 1u;
        if (type == torchId)       return 2u;
        if (type == doorId)        return 3u;
        if (type == ladderId)      return 4u;
        if (type == wedgeId)       return 5u;
        if (type == grassId)       return 6u;
        if (type == dirtId)        return 7u;
        return 1u;
    }

    private void FrameCamera(Bounds bounds)
    {
        var cam = Camera.main;
        if (cam == null) return;

        float distance = bounds.size.magnitude * 0.75f;

        var orbit = cam.GetComponent<OrbitCamera>();
        if (orbit != null)
            orbit.Frame(bounds.center, distance);
        else
        {
            cam.transform.position = bounds.center + new Vector3(0f, bounds.size.y * 0.15f, -distance);
            cam.transform.LookAt(bounds.center);
        }
    }

    [ContextMenu("Reset Params to Default")]
    public void ResetParamsToDefault() => Params = DungeonParams.Default;
}
