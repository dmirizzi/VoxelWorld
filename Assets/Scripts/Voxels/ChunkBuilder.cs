using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class ChunkBuilder
{
    private VoxelWorld _world;

    private Chunk _chunk;

    private readonly Material _textureAtlasMaterial;

    private readonly Material _textureAtlasTransparentMaterial;

    private ChunkMesh _solidMesh;

    private ChunkMesh _transparentMesh;

    private Vector3Int _chunkVoxelPos;

    private GameObject _solidChunk;

    public Vector3Int ChunkPos { get; private set; }

    public ChunkBuilder(VoxelWorld world, Vector3Int chunkPos, Chunk chunk, Material textureAtlasMaterial, Material textureAtlasTransparentMaterial)
    {
        _world = world;
        ChunkPos = chunkPos;
        _chunkVoxelPos = VoxelPosConverter.ChunkToBaseVoxelPos(chunkPos);
        _chunk = chunk;
        _textureAtlasMaterial = textureAtlasMaterial;
        _textureAtlasTransparentMaterial = textureAtlasTransparentMaterial;
        _solidMesh = new ChunkMesh();
        _transparentMesh = new ChunkMesh();
    }

    public GameObject[] GetChunkGameObjects()
    {
        _solidChunk = new GameObject($"SolidVoxelMesh");
        _solidChunk.AddComponent<MeshRenderer>();
        var solidMeshFilter = _solidChunk.AddComponent<MeshFilter>().mesh;

        solidMeshFilter.Clear();
        solidMeshFilter.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        solidMeshFilter.vertices = _solidMesh.Vertices.ToArray();
        solidMeshFilter.colors32 = GetLightVertexColors();
        solidMeshFilter.triangles = _solidMesh.Triangles.ToArray();
        solidMeshFilter.normals = _solidMesh.Normals.ToArray();
        solidMeshFilter.uv = _solidMesh.UVCoordinates.ToArray();
        solidMeshFilter.Optimize();

        _solidChunk.GetComponent<Renderer>().material = _textureAtlasMaterial;
        GenerateMeshCollider(_solidChunk);
        _solidChunk.layer = LayerMask.NameToLayer("Voxels");

        var transparentChunk = new GameObject($"TransparentVoxelMesh");
        transparentChunk.AddComponent<MeshRenderer>();
        var transparentMeshFilter = transparentChunk.AddComponent<MeshFilter>().mesh;

        transparentMeshFilter.Clear();
        transparentMeshFilter.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        transparentMeshFilter.vertices = _transparentMesh.Vertices.ToArray();
        transparentMeshFilter.triangles = _transparentMesh.Triangles.ToArray();
        transparentMeshFilter.normals = _transparentMesh.Normals.ToArray();
        transparentMeshFilter.uv = _transparentMesh.UVCoordinates.ToArray();
        transparentMeshFilter.Optimize();

        transparentChunk.GetComponent<Renderer>().material = _textureAtlasTransparentMaterial;
        GenerateMeshCollider(transparentChunk);
        transparentChunk.layer = LayerMask.NameToLayer("Voxels");

        return new GameObject[] {
            _solidChunk,
            transparentChunk
        };
    }


    public Task Build()
    {
        return Task.Run( () => 
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
        });
    }

    public void UpdateLightVertexColors()
    {
        _solidChunk.GetComponent<MeshFilter>().mesh.colors32 = GetLightVertexColors();
    }

    private Color32[] GetLightVertexColors()
    {
        // Add vertex colors based on light map
        var colors = new Color32[_solidMesh.Vertices.Count];
        for(int vi = 0; vi < _solidMesh.Vertices.Count; ++vi)
        {
            var localVoxelPos = Vector3Int.FloorToInt(_solidMesh.Vertices[vi]);
            var globalVoxelPos = VoxelPosConverter.ChunkLocalVoxelPosToGlobal(localVoxelPos, _chunk.ChunkPos);

            //TODO: Would be faster to get light value direct from chunk and surrounding chunks
            colors[vi] = _world.GetLightValue(globalVoxelPos);
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

        var voxelVertices = new List<Vector3>();
        for(int i = 0; i < VoxelInfo.VoxelFaceData.Length; ++i)
        {
            var faceData = VoxelInfo.VoxelFaceData[i];
            if(VoxelBuildHelper.IsVoxelSideVisible(_world, voxelType, globalVoxelPos, faceData.Direction))
            {
                voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[0]]);
                voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[1]]);
                voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[2]]);
                voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[3]]);

                chunkMesh.Normals.AddRange(faceData.Normals);

                var uv = VoxelBuildHelper.GetUVsForVoxelType(voxelType, faceData.VoxelFace);
                chunkMesh.UVCoordinates.Add(uv[faceData.UVIndices[0]]);
                chunkMesh.UVCoordinates.Add(uv[faceData.UVIndices[1]]);
                chunkMesh.UVCoordinates.Add(uv[faceData.UVIndices[2]]);
                chunkMesh.UVCoordinates.Add(uv[faceData.UVIndices[3]]);
            }
        }

        int vertexBaseIdx = chunkMesh.Vertices.Count;
        chunkMesh.Vertices.AddRange(voxelVertices);

        for(int i = 0; i < voxelVertices.Count; i += 4)
        {
            chunkMesh.Triangles.Add(i + vertexBaseIdx);
            chunkMesh.Triangles.Add(i + 1 + vertexBaseIdx);
            chunkMesh.Triangles.Add(i + 2 + vertexBaseIdx);
            chunkMesh.Triangles.Add(i + 2 + vertexBaseIdx);
            chunkMesh.Triangles.Add(i + 3 + vertexBaseIdx);
            chunkMesh.Triangles.Add(i + vertexBaseIdx);
        }
    }

    private void GenerateMeshCollider(GameObject chunkObject)
    {
        var collider = chunkObject.AddComponent<MeshCollider>();
    }
}
