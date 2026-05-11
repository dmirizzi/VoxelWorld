using System.Collections.Generic;
using UnityEngine;

public class ChunkMeshBuilder
{
    public Vector3Int ChunkPos { get; private set; }

    public ChunkMeshBuilder(VoxelWorld world, Vector3Int chunkPos, Chunk chunk, Material textureAtlasMaterial, Material textureAtlasTransparentMaterial)
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
                    var voxelType = _chunk.GetVoxelInsideChunk(x, y, z);
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

        meshFilter.SetVertices(chunkMesh.Vertices);
        meshFilter.SetNormals(chunkMesh.Normals);
        meshFilter.SetUVs(0, chunkMesh.UVCoordinates);
        meshFilter.SetTriangles(chunkMesh.Triangles, 0);
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

            var localVoxelPos = VoxelPosHelper.WorldPosToGlobalVoxelPos(vp);

            int r = 0, g = 0, b = 0, sun = 0;
            //TODO: Need to look into why we need to sample only -1 to 0 for smooth lighting to work.
            //TODO: If we go until 1, we get some dark spots on the corners of voxels.
            //TODO: Maybe we need to ignore voxels that are occluded with a light value of 0?
            for(int x = -1; x < 1; ++x)
            {
                for(int z = -1; z < 1; ++z)
                {
                    for(int y = -1; y < 1; ++y)
                    {
                        var neighborPos = localVoxelPos + new Vector3Int(x, y, z);
                        if(_chunk.LocalVoxelPosIsInChunk(neighborPos))
                        {
                            r += (byte)(_chunk.GetLightChannelValue(neighborPos, 0) << 4);
                            g += (byte)(_chunk.GetLightChannelValue(neighborPos, 1) << 4);
                            b += (byte)(_chunk.GetLightChannelValue(neighborPos, 2) << 4);
                            sun += (byte)(_chunk.GetLightChannelValue(neighborPos, 3) << 4);
                        }
                        else if(_chunk.TryGetNeighboringChunkVoxel(neighborPos, out var neighborChunk, out var neighborChunkLocalVoxelPos))
                        {
                            r += (byte)(neighborChunk.GetLightChannelValue(neighborChunkLocalVoxelPos, 0) << 4);
                            g += (byte)(neighborChunk.GetLightChannelValue(neighborChunkLocalVoxelPos, 1) << 4);
                            b += (byte)(neighborChunk.GetLightChannelValue(neighborChunkLocalVoxelPos, 2) << 4);
                            sun += (byte)(neighborChunk.GetLightChannelValue(neighborChunkLocalVoxelPos, 3) << 4);
                        }
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

        /*
            //TODO: Optimize before re-enabling
            var heightOffset = _world.GetVoxel(globalVoxelPos + Vector3Int.up) != voxelType ?
                VoxelInfo.GetVoxelHeightOffset(voxelType)
                : 0.0f;
        */
        var heightOffset = 0.0f;

        bool cornersComputed = false;

        _voxelVertices.Clear();
        for (int i = 0; i < VoxelInfo.VoxelFaceData.Length; ++i)
        {
            var faceData = VoxelInfo.VoxelFaceData[i];
            if (VoxelBuildHelper.IsVoxelSideVisible(_world, _chunk, voxelType, globalVoxelPos, localVoxelPos, faceData.VoxelFace))
            {
                if (!cornersComputed)
                {
                    // Calculate voxel corner vertices only if at least one face is visible
                    float x = localVoxelPos.x, y = localVoxelPos.y, z = localVoxelPos.z;
                    float s = VoxelInfo.VoxelSize, top = s - heightOffset;
                    _voxelCornerVertices[0] = new Vector3(x,     y,       z);
                    _voxelCornerVertices[1] = new Vector3(x + s, y,       z);
                    _voxelCornerVertices[2] = new Vector3(x + s, y,       z + s);
                    _voxelCornerVertices[3] = new Vector3(x,     y,       z + s);
                    _voxelCornerVertices[4] = new Vector3(x,     y + top, z);
                    _voxelCornerVertices[5] = new Vector3(x + s, y + top, z);
                    _voxelCornerVertices[6] = new Vector3(x + s, y + top, z + s);
                    _voxelCornerVertices[7] = new Vector3(x,     y + top, z + s);
                    cornersComputed = true;
                }

                _voxelVertices.Add(_voxelCornerVertices[faceData.VertexIndices[0]]);
                _voxelVertices.Add(_voxelCornerVertices[faceData.VertexIndices[1]]);
                _voxelVertices.Add(_voxelCornerVertices[faceData.VertexIndices[2]]);
                _voxelVertices.Add(_voxelCornerVertices[faceData.VertexIndices[3]]);

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

        for (int i = 0; i < _voxelVertices.Count; i += 4)
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

    // Reused across all voxels during Build() to avoid per-voxel heap allocation.
    private List<Vector3> _voxelVertices = new List<Vector3>(24);
    private Vector3[] _voxelCornerVertices = new Vector3[8];

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
