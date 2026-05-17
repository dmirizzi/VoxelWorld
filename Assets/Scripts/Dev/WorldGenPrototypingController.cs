using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

// Drop onto an empty GameObject in the WorldGenPrototyping scene.
// Assign VoxelCullerShader and VoxelInstancedShader in the Inspector.
public class WorldGenPrototypingController : MonoBehaviour
{
    [Header("World Generation")]
    public int  ChunkGenerationRadius = 8;
    public int  WorldSeed             = 123456789;

    [Header("Rendering")]
    public ComputeShader VoxelCullerShader;
    public Shader        VoxelInstancedShader;

    // Colors indexed by block type ID — must match BlockTypes.json order.
    private static readonly Color[] BlockColors =
    {
        Color.clear,                            // 0 Empty
        new(0.30f, 0.60f, 0.20f),              // 1 Grass
        new(0.50f, 0.35f, 0.20f),              // 2 Dirt
        new(0.20f, 0.40f, 0.80f),              // 3 Water
        new(0.50f, 0.50f, 0.50f),              // 4 Cobblestone
        new(1.00f, 0.70f, 0.20f),              // 5 Torch
        new(0.45f, 0.45f, 0.45f),              // 6 CobblestoneWedge
        new(0.40f, 0.25f, 0.10f),              // 7 Door
        new(0.70f, 0.50f, 0.30f),              // 8 Ladder
        new(0.40f, 0.30f, 0.10f),              // 9 Log
        new(0.15f, 0.45f, 0.10f),              // 10 Leaves
    };

    private readonly VoxelPrototypingRenderer _renderer = new();

    // OnEnable fires on first start AND after every domain reload (script recompile in Play mode),
    // making it the right place to rebuild native GPU resources.
    void OnEnable()
    {
        if (Application.isPlaying)
            Regenerate();
    }

    void OnDisable() => _renderer.Release();

    void Update()
    {
        _renderer.Draw();

        if (Input.GetKeyDown(KeyCode.R))
            Regenerate();
    }

    [ContextMenu("Regenerate")]
    public void Regenerate()
    {
        _renderer.Release();
        var chunks = GenerateAllChunks();
        DispatchCuller(chunks, out Vector3Int gridSize, out _);
        uint instances = _renderer.SetupRendering(VoxelInstancedShader, BlockColors);
        Debug.Log($"[WorldGenPrototyping] Ready. Seed={WorldSeed}  Radius={ChunkGenerationRadius}  Grid={gridSize}  Surface={instances:N0}");
    }

    // -------------------------------------------------------------------------

    private IDictionary<Vector3Int, ushort[,,]> GenerateAllChunks()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var generator = new ChunkGenerator(WorldSeed);
        var chunkData = new ConcurrentDictionary<Vector3Int, ushort[,,]>();
        var allUpdates = new ConcurrentBag<ChunkUpdate>();

        int r = ChunkGenerationRadius;

        var columnTasks = new List<Task>();

        for (int z = -r; z <= r; ++z)
        for (int x = -r; x <= r; ++x)
        {
            int lx = x, lz = z; // avoid modified closure in lambda
            columnTasks.Add(Task.Run(() =>
            {
                for (int y = -r; y <= r; ++y)
                {
                    var pos    = new Vector3Int(lx, y, lz);
                    var update = generator.GenerateChunk(pos);
                    chunkData[pos] = update.VoxelData;
                    allUpdates.Add(update);
                }
            }));
        }
        Task.WaitAll(columnTasks.ToArray());

        // Apply cross-chunk backlog (tree tops, etc.)
        foreach (var update in allUpdates)
        {
            foreach (var kvp in update.Backlog)
            {
                if (!chunkData.TryGetValue(kvp.Key, out var target)) continue;
                foreach (var action in kvp.Value)
                {
                    var lp = action.LocalVoxelPos;
                    if ((uint)lp.x < 16 && (uint)lp.y < 16 && (uint)lp.z < 16)
                        target[lp.x, lp.y, lp.z] = action.Type;
                }
            }
        }

        sw.Stop();
        Debug.Log($"[WorldGenPrototyping] Generated {chunkData.Count} chunks in {sw.Elapsed.TotalSeconds:F2} seconds");

        return chunkData;
    }

    private void DispatchCuller(
        IDictionary<Vector3Int, ushort[,,]> chunkData,
        out Vector3Int gridSize,
        out Vector3Int gridMin)
    {
        int r    = ChunkGenerationRadius;
        int side = (2 * r + 1) * VoxelInfo.ChunkSize;
        int off  = -r * VoxelInfo.ChunkSize;

        gridSize = new Vector3Int(side, side, side);
        gridMin  = new Vector3Int(off, off, off);

        // Build flat grid: index = x + z*sideX + y*sideX*sideZ
        int total = side * side * side;
        var grid  = new uint[total];

        foreach (var kvp in chunkData)
        {
            var cp    = kvp.Key;
            var data  = kvp.Value;
            int baseX = cp.x * VoxelInfo.ChunkSize - gridMin.x;
            int baseY = cp.y * VoxelInfo.ChunkSize - gridMin.y;
            int baseZ = cp.z * VoxelInfo.ChunkSize - gridMin.z;

            for (int ly = 0; ly < VoxelInfo.ChunkSize; ++ly)
            for (int lz = 0; lz < VoxelInfo.ChunkSize; ++lz)
            for (int lx = 0; lx < VoxelInfo.ChunkSize; ++lx)
            {
                int idx = (baseX + lx) + (baseZ + lz) * side + (baseY + ly) * side * side;
                grid[idx] = data[lx, ly, lz];
            }
        }

        _renderer.Dispatch(VoxelCullerShader, grid, side, side, side, gridMin);
    }
}
