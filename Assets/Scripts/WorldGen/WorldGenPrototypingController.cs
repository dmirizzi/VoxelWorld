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

    private ComputeBuffer _gridBuffer;
    private ComputeBuffer _surfaceBuffer;
    private ComputeBuffer _argsBuffer;
    private Material      _material;
    private Mesh          _cubeMesh;

    // OnEnable fires on first start AND after every domain reload (script recompile in Play mode),
    // making it the right place to rebuild native GPU resources.
    void OnEnable()
    {
        if (Application.isPlaying)
            Regenerate();
    }

    void OnDisable() => ReleaseBuffers();

    void Update()
    {
        if (_argsBuffer != null)
            Graphics.DrawMeshInstancedIndirect(
                _cubeMesh, 0, _material,
                new Bounds(Vector3.zero, Vector3.one * 100000f),
                _argsBuffer);

        if (Input.GetKeyDown(KeyCode.R))
            Regenerate();
    }

    [ContextMenu("Regenerate")]
    public void Regenerate()
    {
        ReleaseBuffers();
        var chunks = GenerateAllChunks();
        DispatchCuller(chunks, out Vector3Int gridSize, out _);
        SetupRendering();
        Debug.Log($"[WorldGenPrototyping] Ready. Seed={WorldSeed}  Radius={ChunkGenerationRadius}  Grid={gridSize}");
    }

    private void ReleaseBuffers()
    {
        _gridBuffer?.Release();    _gridBuffer    = null;
        _surfaceBuffer?.Release(); _surfaceBuffer = null;
        _argsBuffer?.Release();    _argsBuffer    = null;
        if (_material != null) { Destroy(_material); _material = null; }
        _cubeMesh = null;
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

        _gridBuffer = new ComputeBuffer(total, sizeof(uint));
        _gridBuffer.SetData(grid);

        // Surface output — generous upper bound
        int maxSurface  = Mathf.Max(total / 6, 1);
        _surfaceBuffer  = new ComputeBuffer(maxSurface, 16, ComputeBufferType.Append);
        _surfaceBuffer.SetCounterValue(0);

        int kernel = VoxelCullerShader.FindKernel("CullSurface");
        VoxelCullerShader.SetBuffer(kernel, "VoxelGrid",     _gridBuffer);
        VoxelCullerShader.SetBuffer(kernel, "SurfaceVoxels", _surfaceBuffer);
        VoxelCullerShader.SetInts("GridSize",   side, side, side);
        VoxelCullerShader.SetInts("GridOffset", gridMin.x, gridMin.y, gridMin.z);

        int groups = Mathf.CeilToInt(side / 8f);
        VoxelCullerShader.Dispatch(kernel, groups, groups, groups);
    }

    private void SetupRendering()
    {
        // Read back instance count from the append buffer counter
        using var countBuf = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
        ComputeBuffer.CopyCount(_surfaceBuffer, countBuf, 0);
        var countArr = new uint[1];
        countBuf.GetData(countArr);
        uint instanceCount = countArr[0];
        Debug.Log($"[WorldGenPrototyping] Surface voxel count: {instanceCount:N0}");

        _cubeMesh = BuildUnitCube();

        _material = new Material(VoxelInstancedShader);
        _material.SetBuffer("_VoxelBuffer", _surfaceBuffer);

        var colors = new Vector4[16];
        for (int i = 0; i < Mathf.Min(BlockColors.Length, 16); ++i)
            colors[i] = BlockColors[i];
        _material.SetVectorArray("_BlockColors", colors);

        _argsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(new uint[]
        {
            (uint)_cubeMesh.GetIndexCount(0),
            instanceCount,
            (uint)_cubeMesh.GetIndexStart(0),
            (uint)_cubeMesh.GetBaseVertex(0),
            0u
        });
    }

    private static Mesh BuildUnitCube()
    {
        // Vertices laid out so normals point outward per face for shading
        var v = new Vector3[]
        {
            // Bottom -Y
            new(0,0,0), new(1,0,0), new(1,0,1), new(0,0,1),
            // Top +Y
            new(0,1,1), new(1,1,1), new(1,1,0), new(0,1,0),
            // Front -Z
            new(0,0,0), new(0,1,0), new(1,1,0), new(1,0,0),
            // Back +Z
            new(1,0,1), new(1,1,1), new(0,1,1), new(0,0,1),
            // Left -X
            new(0,0,1), new(0,1,1), new(0,1,0), new(0,0,0),
            // Right +X
            new(1,0,0), new(1,1,0), new(1,1,1), new(1,0,1),
        };

        var tris = new int[]
        {
            0,2,1,  0,3,2,    // Bottom
            4,6,5,  4,7,6,    // Top
            8,10,9, 8,11,10,  // Front
            12,14,13,12,15,14,// Back
            16,18,17,16,19,18,// Left
            20,22,21,20,23,22,// Right
        };

        var mesh = new Mesh { name = "PrototypeCube" };
        mesh.vertices  = v;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
