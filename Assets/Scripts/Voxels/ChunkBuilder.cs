using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class ChunkBuilder
{
    public Vector3Int ChunkPos { get; private set; }

    public ChunkBuilder(VoxelWorld world, Vector3Int chunkPos, Chunk chunk, Material textureAtlasMaterial, Material textureAtlasTransparentMaterial)
    {
        _world = world;
        ChunkPos = chunkPos;
        _chunkVoxelPos = VoxelPosHelper.ChunkPosToGlobalChunkBaseVoxelPos(chunkPos);
        _chunk = chunk;
        _textureAtlasMaterial = textureAtlasMaterial;
        _textureAtlasTransparentMaterial = textureAtlasTransparentMaterial;
        _solidMesh = new ChunkMesh();
        _transparentMesh = new ChunkMesh();
    }

    public GameObject[] CreateMeshGameObjects()
    {
        _solidChunk = new GameObject($"SolidVoxelMesh");

        AddChunkMeshToGameObject(_solidMesh, _solidChunk);
        _solidChunk.GetComponent<Renderer>().material = _textureAtlasMaterial;
        _solidChunk.AddComponent<MeshCollider>();
        _solidChunk.layer = LayerMask.NameToLayer("Voxels");

        _transparentChunk = new GameObject($"TransparentVoxelMesh");
        AddChunkMeshToGameObject(_transparentMesh, _transparentChunk);
        _transparentChunk.GetComponent<Renderer>().material = _textureAtlasTransparentMaterial;
        _transparentChunk.AddComponent<MeshCollider>();
        _transparentChunk.layer = LayerMask.NameToLayer("Voxels");

        return new GameObject[] {
            _solidChunk,
            _transparentChunk
        };
    }

    public void Build()
    {
        for(int y = 0; y < VoxelInfo.ChunkSize; ++y)
        {
            for(int x = 0; x < VoxelInfo.ChunkSize; ++x)
            {
                for(int z = 0; z < VoxelInfo.ChunkSize; ++z)
                {
                    var voxelType = _chunk.GetVoxel(x, y, z);
                    if(voxelType == 0) continue;

                    var localVoxelPos =  new Vector3Int(x, y, z);
                    var globalVoxelPos = _chunkVoxelPos + localVoxelPos;
                    var renderType = BlockDataRepository.GetBlockData(voxelType).RenderType;

                    if(renderType == BlockRenderType.CustomMesh)
                    {
                        var blockType = BlockTypeRegistry.GetBlockType(voxelType);
                        blockType.OnChunkVoxelMeshBuild(
                            _world,
                            _chunk,
                            voxelType,
                            globalVoxelPos,
                            localVoxelPos,
                            VoxelInfo.IsTransparent(voxelType) ? _transparentMesh : _solidMesh
                        );
                    }
                    else if(renderType == BlockRenderType.Voxel)
                    {
                        AddVoxelVertices(
                            voxelType,
                            globalVoxelPos,
                            localVoxelPos,
                            VoxelInfo.IsTransparent(voxelType) ? _transparentMesh : _solidMesh
                        );
                    }
                }
            }
        }
    }

    public ChunkLightColorMapping CreateChunkLightColorMapping()
    {   
        return new ChunkLightColorMapping   
        {
            ChunkPos = ChunkPos,
            SolidMeshLightMapping = GetSmoothLightVertexColorMapping(_solidMesh),
            TransparentMeshLightMapping = GetSmoothLightVertexColorMapping(_transparentMesh)
        };
    }

    public void UpdateLightVertexColors(ChunkLightColorMapping mapping)
    {
        _solidChunk.GetComponent<MeshFilter>().mesh.colors32 = mapping.SolidMeshLightMapping;
        _transparentChunk.GetComponent<MeshFilter>().mesh.colors32 = mapping.TransparentMeshLightMapping;
    }

    private void AddChunkMeshToGameObject(ChunkMesh chunkMesh, GameObject gameObject)
    {
        gameObject.AddComponent<MeshRenderer>();
        var meshFilter = gameObject.AddComponent<MeshFilter>().mesh;

        meshFilter.Clear();
        meshFilter.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        meshFilter.vertices = chunkMesh.GetVerticesArray();
        meshFilter.triangles = chunkMesh.GetTrianglesArray();
        meshFilter.normals = chunkMesh.GetNormalsArray();
        meshFilter.uv = chunkMesh.GetUVArray();
        meshFilter.Optimize();
    }

    private Color32[] GetBlockyLightVertexColorMapping(ChunkMesh mesh)
    {
        var colors = new Color32[mesh.Vertices.Count];

        for(int ti = 0; ti < mesh.Triangles.Count; ti += 3)
        {
            var vi1 = mesh.Triangles[ti];
            var vi2 = mesh.Triangles[ti + 1];
            var vi3 = mesh.Triangles[ti + 2];

            var v1 = mesh.Vertices[vi1];
            var v2 = mesh.Vertices[vi2];
            var v3 = mesh.Vertices[vi3];

            var normal = mesh.Normals[vi1];

            var lightSamplingPoint = (v1 + v2 + v3) / 3f + normal * 0.5f;

            var localVoxelPos = VoxelPosHelper.WorldPosToGlobalVoxelPos(lightSamplingPoint);
            var globalVoxelPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(localVoxelPos, _chunk.ChunkPos);

            var lightVal = _world.GetVoxelLightColor(globalVoxelPos);
            colors[vi1] = lightVal;
            colors[vi2] = lightVal;
            colors[vi3] = lightVal;
        }

        return colors;
    }

    private Color32[] GetSmoothLightVertexColorMapping(ChunkMesh mesh)
    {
        var colors = new Color32[mesh.Vertices.Count];

        for(int vi = 0; vi < mesh.Vertices.Count; ++vi)
        {
            var vp = mesh.Vertices[vi];

            var globalVoxelPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(VoxelPosHelper.WorldPosToGlobalVoxelPos(vp), _chunk.ChunkPos);

            int r = 0, g = 0, b = 0, sun = 0;
            int numVoxels = 0;
            for(int x = -1; x < 1; ++x)
            {
                for(int z = -1; z < 1; ++z)
                {   
                    for(int y = -1; y < 1; ++y)
                    {   
                        var neighborPos = globalVoxelPos + new Vector3Int(x, y, z);
                        var voxelColor = _world.GetVoxelLightColor(neighborPos);
                        r += voxelColor.r;
                        g += voxelColor.g;
                        b += voxelColor.b;
                        sun += voxelColor.a;
                        numVoxels++;
                    }
                }
            }

            // Calc average of surrounding voxel light colors
            r >>= 3;
            g >>= 3;
            b >>= 3;
            sun >>= 3;

            colors[vi] = new Color32((byte)r, (byte)g, (byte)b, (byte)sun);
        }

        return colors;
    }

    private void AddVoxelVertices(
        ushort voxelType, 
        Vector3Int globalVoxelPos,
        Vector3Int localVoxelPos,
        ChunkMesh chunkMesh)
    {
        var heightOffset = _world.GetVoxel(globalVoxelPos + Vector3Int.up) != voxelType ?
            VoxelInfo.GetVoxelHeightOffset(voxelType)
            : 0.0f;

        var voxelCornerVertices = new Vector3[]
        {
            localVoxelPos,
            localVoxelPos + new Vector3(VoxelInfo.VoxelSize, 0, 0),
            localVoxelPos + new Vector3(VoxelInfo.VoxelSize, 0, VoxelInfo.VoxelSize),
            localVoxelPos + new Vector3(0, 0, VoxelInfo.VoxelSize),
            localVoxelPos + new Vector3(0, VoxelInfo.VoxelSize - heightOffset, 0),
            localVoxelPos + new Vector3(VoxelInfo.VoxelSize, VoxelInfo.VoxelSize - heightOffset, 0),
            localVoxelPos + new Vector3(VoxelInfo.VoxelSize, VoxelInfo.VoxelSize - heightOffset, VoxelInfo.VoxelSize),
            localVoxelPos + new Vector3(0, VoxelInfo.VoxelSize - heightOffset, VoxelInfo.VoxelSize)
        };

        _voxelVertices.Clear();
        for(int i = 0; i < VoxelInfo.VoxelFaceData.Length; ++i)
        {
            var faceData = VoxelInfo.VoxelFaceData[i];
            if(VoxelBuildHelper.IsVoxelSideVisible(_world, voxelType, globalVoxelPos, faceData.VoxelFace))
            {
                _voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[0]]);
                _voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[1]]);
                _voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[2]]);
                _voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[3]]);

                chunkMesh.AddNormals(faceData.Normals);

                var uv = VoxelBuildHelper.GetUVsForVoxelType(voxelType, faceData.VoxelFace);
                chunkMesh.AddUVCoordinate(uv[faceData.UVIndices[0]]);
                chunkMesh.AddUVCoordinate(uv[faceData.UVIndices[1]]);
                chunkMesh.AddUVCoordinate(uv[faceData.UVIndices[2]]);
                chunkMesh.AddUVCoordinate(uv[faceData.UVIndices[3]]);
            }
        }

        int vertexBaseIdx = chunkMesh.Vertices.Count;
        chunkMesh.AddVertices(_voxelVertices);

        for(int i = 0; i < _voxelVertices.Count; i += 4)
        {
            chunkMesh.AddTriangleIndex(i + vertexBaseIdx);
            chunkMesh.AddTriangleIndex(i + 1 + vertexBaseIdx);
            chunkMesh.AddTriangleIndex(i + 2 + vertexBaseIdx);
            chunkMesh.AddTriangleIndex(i + 2 + vertexBaseIdx);
            chunkMesh.AddTriangleIndex(i + 3 + vertexBaseIdx);
            chunkMesh.AddTriangleIndex(i + vertexBaseIdx);
        }
    }

    public struct ChunkLightColorMapping
    {
        public Vector3Int ChunkPos;

        public Color32[] SolidMeshLightMapping;

        public Color32[] TransparentMeshLightMapping;
    }

    // List of potential vertices for a voxel during building. We only instantiate it once to improve performance.
    private List<Vector3> _voxelVertices = new List<Vector3>(24);

    private VoxelWorld _world;

    private Chunk _chunk;

    private readonly Material _textureAtlasMaterial;

    private readonly Material _textureAtlasTransparentMaterial;

    private ChunkMesh _solidMesh;

    private ChunkMesh _transparentMesh;

    private Vector3Int _chunkVoxelPos;

    private GameObject _solidChunk;

    private GameObject _transparentChunk;    
}
