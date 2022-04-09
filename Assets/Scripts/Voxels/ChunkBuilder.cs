using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

public class ChunkBuilder
{
    private VoxelWorld _world;
    private readonly Material _textureAtlasMaterial;
    private readonly Material _textureAtlasTransparentMaterial;
    private List<Vector3> _chunkVertices = new List<Vector3>();
    private List<Vector3> _chunkNormals = new List<Vector3>();
    private List<Vector2> _chunkUVs = new List<Vector2>();
    private List<int> _chunkTriangles = new List<int>();
    private List<Vector3> _chunkVerticesTp = new List<Vector3>();
    private List<Vector3> _chunkNormalsTp = new List<Vector3>();
    private List<Vector2> _chunkUVsTp = new List<Vector2>();
    private List<int> _chunkTrianglesTp = new List<int>();
    private Vector3Int _chunkVoxelPos;

    public Vector3Int ChunkPos { get; private set; }

    public ChunkBuilder(VoxelWorld world, Material textureAtlasMaterial, Material textureAtlasTransparentMaterial)
    {
        _world = world;
        _textureAtlasMaterial = textureAtlasMaterial;
        _textureAtlasTransparentMaterial = textureAtlasTransparentMaterial;
    }

    public GameObject[] GetChunkGameObjects()
    {
        var chunkGameObj = new GameObject($"SolidVoxelMesh");
        chunkGameObj.AddComponent<MeshRenderer>();
        var mesh = chunkGameObj.AddComponent<MeshFilter>().mesh;

        mesh.Clear();
        mesh.vertices = _chunkVertices.ToArray();
        mesh.triangles = _chunkTriangles.ToArray();
        mesh.normals = _chunkNormals.ToArray();
        mesh.uv = _chunkUVs.ToArray();
        mesh.Optimize();

        chunkGameObj.GetComponent<Renderer>().material = _textureAtlasMaterial;
        GenerateMeshCollider(chunkGameObj);
        chunkGameObj.layer = LayerMask.NameToLayer("Voxels");

        var chunkTpGameObj = new GameObject($"TransparentVoxelMesh");
        chunkTpGameObj.AddComponent<MeshRenderer>();
        var meshTp = chunkTpGameObj.AddComponent<MeshFilter>().mesh;

        meshTp.Clear();
        meshTp.vertices = _chunkVerticesTp.ToArray();
        meshTp.triangles = _chunkTrianglesTp.ToArray();
        meshTp.normals = _chunkNormalsTp.ToArray();
        meshTp.uv = _chunkUVsTp.ToArray();
        meshTp.Optimize();

        chunkTpGameObj.GetComponent<Renderer>().material = _textureAtlasTransparentMaterial;
        GenerateMeshCollider(chunkTpGameObj);
        chunkTpGameObj.layer = LayerMask.NameToLayer("Voxels");

        return new GameObject[] {
            chunkGameObj,
            chunkTpGameObj
        };
    }

    public Task Build(Vector3Int chunkPos, Chunk chunk)
    {
        ChunkPos = chunkPos;
        _chunkVoxelPos = VoxelPosConverter.ChunkToBaseVoxelPos(chunkPos);

        return Task.Run( () => 
        {         
            for(int x = 0; x < VoxelInfo.ChunkSize; ++x)
            {
                for(int y = 0; y < VoxelInfo.ChunkSize; ++y)
                {
                    for(int z = 0; z < VoxelInfo.ChunkSize; ++z)
                    {
                        var voxelType = (VoxelType)chunk.GetVoxel(x, y, z);
                        if(voxelType == VoxelType.Empty) continue;
                        if(VoxelInfo.IsGameObjectBlock(voxelType)) continue;

                        bool isTransparent = !VoxelInfo.IsSolid(voxelType);

                        // Select which chunk to add mesh to - either solid or transparent
                        var vertices = isTransparent ? _chunkVerticesTp : _chunkVertices;
                        var normals = isTransparent ? _chunkNormalsTp : _chunkNormals;
                        var uvs = isTransparent ? _chunkUVsTp : _chunkUVs;
                        var triangles = isTransparent ? _chunkTrianglesTp : _chunkTriangles;

                        var voxelPos = _chunkVoxelPos + new Vector3Int(x, y, z);

                        var heightOffset = (VoxelType)_world.GetVoxel(voxelPos + Vector3Int.up) != voxelType ?
                            VoxelInfo.GetVoxelHeightOffset(voxelType)
                            : 0.0f;

                        var voxelCornerVertices = new Vector3[]
                        {
                                new Vector3(x, y, z),
                                new Vector3(x + VoxelInfo.VoxelSize, y, z),
                                new Vector3(x + VoxelInfo.VoxelSize, y, z + VoxelInfo.VoxelSize),
                                new Vector3(x, y, z + VoxelInfo.VoxelSize),

                                new Vector3(x, y + VoxelInfo.VoxelSize - heightOffset, z),
                                new Vector3(x + VoxelInfo.VoxelSize, y + VoxelInfo.VoxelSize - heightOffset, z),
                                new Vector3(x + VoxelInfo.VoxelSize, y + VoxelInfo.VoxelSize - heightOffset, z + VoxelInfo.VoxelSize),
                                new Vector3(x, y + VoxelInfo.VoxelSize - heightOffset, z + VoxelInfo.VoxelSize)
                        };

                        var voxelVertices = new List<Vector3>();
                        for(int i = 0; i < VoxelInfo.VoxelFaceData.Length; ++i)
                        {
                            var faceData = VoxelInfo.VoxelFaceData[i];
                            if(VoxelSideVisible(voxelType, voxelPos, faceData.Direction))
                            {
                                voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[0]]);
                                voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[1]]);
                                voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[2]]);
                                voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[3]]);

                                normals.AddRange(faceData.Normals);

                                var uv = GetUVsForVoxelType(voxelType, faceData.VoxelFace);
                                uvs.Add(uv[faceData.UVIndices[0]]);
                                uvs.Add(uv[faceData.UVIndices[1]]);
                                uvs.Add(uv[faceData.UVIndices[2]]);
                                uvs.Add(uv[faceData.UVIndices[3]]);
                            }
                        }

                        int vertexBaseIdx = vertices.Count;
                        vertices.AddRange(voxelVertices);

                        for(int i = 0; i < voxelVertices.Count; i += 4)
                        {
                            triangles.Add(i + vertexBaseIdx);
                            triangles.Add(i + 1 + vertexBaseIdx);
                            triangles.Add(i + 2 + vertexBaseIdx);
                            triangles.Add(i + 2 + vertexBaseIdx);
                            triangles.Add(i + 3 + vertexBaseIdx);
                            triangles.Add(i + vertexBaseIdx);
                        }
                    }
                }
            }
        });
    }

    private void GenerateMeshCollider(GameObject chunkObject)
    {
        var collider = chunkObject.AddComponent<MeshCollider>();
    }

    private bool VoxelSideVisible(VoxelType voxelType, Vector3Int voxelPos, Vector3Int direction)
    {
        var neighbor = _world.GetVoxel(voxelPos + direction);

        if(VoxelInfo.IsSolid(voxelType))
        {
            // Solid neighboring voxels hide their shared face
            return !VoxelInfo.IsSolid(neighbor);
        }
        else
        {
            // Transparent neighboring voxels only hide their shared face if they are of the same type
            return voxelType != neighbor;
        }
    }

    private Dictionary<(VoxelType, VoxelFace), Vector2[]> _tileUVCache = new Dictionary<(VoxelType, VoxelFace), Vector2[]>();

    private Vector2[] GetUVsForVoxelType(VoxelType voxelType, VoxelFace face)
    {     
        // Shift the UV coordinates by a half texel to probe the texture pixel colors at the center
        // of the pixel rather than the border to avoid interpolation between atlas tiles
        Vector2 halfTexel = new Vector2(
          .5f / VoxelInfo.TextureAtlasWidth,
          -.5f / VoxelInfo.TextureAtlasHeight
        );

        if(!_tileUVCache.ContainsKey((voxelType, face)))
        {
            var uvOffset = VoxelInfo.GetAtlasUVOffsetForVoxel(voxelType, face) + halfTexel;
            var uvTileSize = new Vector2(
                VoxelInfo.TextureTileSize * 1.0f / VoxelInfo.TextureAtlasWidth - halfTexel.x * 2,
                VoxelInfo.TextureTileSize * 1.0f / VoxelInfo.TextureAtlasHeight + halfTexel.y * 2
            );

            var uvs = new Vector2[]
            {
                uvOffset + new Vector2(0f, 0f),
                uvOffset + new Vector2(uvTileSize.x, 0f),
                uvOffset + new Vector2(0f, -uvTileSize.y),
                uvOffset + new Vector2(uvTileSize.x, -uvTileSize.y)
            };

            _tileUVCache[(voxelType, face)] = uvs;
        }

        return _tileUVCache[(voxelType, face)];
    }
}
