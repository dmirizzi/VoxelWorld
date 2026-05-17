using UnityEngine;

// Shared GPU voxel renderer for prototyping controllers.
// Handles compute-shader surface culling and GPU-instanced cube drawing.
public class VoxelPrototypingRenderer
{
    private ComputeBuffer _gridBuffer;
    private ComputeBuffer _surfaceBuffer;
    private ComputeBuffer _argsBuffer;
    private Material      _material;
    private Mesh          _cubeMesh;

    // Uploads the flat voxel grid and dispatches the surface-culling compute shader.
    // grid layout: index = x + z*sideX + y*sideX*sideZ; value 0 = air, nonzero = display colour index.
    public void Dispatch(
        ComputeShader shader,
        uint[] grid, int sideX, int sideY, int sideZ,
        Vector3Int gridMin)
    {
        int total = sideX * sideY * sideZ;

        _gridBuffer = new ComputeBuffer(total, sizeof(uint));
        _gridBuffer.SetData(grid);

        int maxSurface = Mathf.Max(total / 4, 1);
        _surfaceBuffer = new ComputeBuffer(maxSurface, 16, ComputeBufferType.Append);
        _surfaceBuffer.SetCounterValue(0);

        int kernel = shader.FindKernel("CullSurface");
        shader.SetBuffer(kernel, "VoxelGrid",     _gridBuffer);
        shader.SetBuffer(kernel, "SurfaceVoxels", _surfaceBuffer);
        shader.SetInts("GridSize",   sideX, sideY, sideZ);
        shader.SetInts("GridOffset", gridMin.x, gridMin.y, gridMin.z);

        shader.Dispatch(kernel,
            Mathf.CeilToInt(sideX / 8f),
            Mathf.CeilToInt(sideY / 8f),
            Mathf.CeilToInt(sideZ / 8f));
    }

    // Creates the material, mesh, and args buffer. Call after Dispatch(). Returns instance count.
    public uint SetupRendering(Shader shader, Color[] colors)
    {
        using var countBuf = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
        ComputeBuffer.CopyCount(_surfaceBuffer, countBuf, 0);
        var countArr = new uint[1];
        countBuf.GetData(countArr);
        uint instanceCount = countArr[0];

        _cubeMesh = BuildUnitCube();

        _material = new Material(shader);
        _material.SetBuffer("_VoxelBuffer", _surfaceBuffer);

        var vec4Colors = new Vector4[16];
        for (int i = 0; i < Mathf.Min(colors.Length, 16); ++i)
            vec4Colors[i] = colors[i];
        _material.SetVectorArray("_BlockColors", vec4Colors);

        _argsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(new uint[]
        {
            (uint)_cubeMesh.GetIndexCount(0),
            instanceCount,
            (uint)_cubeMesh.GetIndexStart(0),
            (uint)_cubeMesh.GetBaseVertex(0),
            0u
        });

        return instanceCount;
    }

    public void Draw()
    {
        if (_argsBuffer == null) return;
        Graphics.DrawMeshInstancedIndirect(
            _cubeMesh, 0, _material,
            new Bounds(Vector3.zero, Vector3.one * 100000f),
            _argsBuffer);
    }

    public void Release()
    {
        _gridBuffer?.Release();    _gridBuffer    = null;
        _surfaceBuffer?.Release(); _surfaceBuffer = null;
        _argsBuffer?.Release();    _argsBuffer    = null;
        if (_material != null) { Object.Destroy(_material); _material = null; }
        _cubeMesh = null;
    }

    public static Mesh BuildUnitCube()
    {
        var v = new Vector3[]
        {
            new(0,0,0), new(1,0,0), new(1,0,1), new(0,0,1),
            new(0,1,1), new(1,1,1), new(1,1,0), new(0,1,0),
            new(0,0,0), new(0,1,0), new(1,1,0), new(1,0,0),
            new(1,0,1), new(1,1,1), new(0,1,1), new(0,0,1),
            new(0,0,1), new(0,1,1), new(0,1,0), new(0,0,0),
            new(1,0,0), new(1,1,0), new(1,1,1), new(1,0,1),
        };
        var tris = new int[]
        {
            0,2,1,  0,3,2,
            4,6,5,  4,7,6,
            8,10,9, 8,11,10,
            12,14,13,12,15,14,
            16,18,17,16,19,18,
            20,22,21,20,23,22,
        };
        var mesh = new Mesh { name = "PrototypingCube" };
        mesh.vertices  = v;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
