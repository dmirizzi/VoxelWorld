using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

public class ChunkBuilder
{
    private struct VoxelFaceData
    {
        public VoxelFaceData(VoxelFace voxelFace, Vector3Int intDirection, Vector3 floatDirection, int[] vertexIndices, int[] uvIndices)
        {
            VoxelFace = voxelFace;
            Direction = intDirection;
            VertexIndices = vertexIndices;
            UVIndices = uvIndices;
            Normals = Enumerable.Repeat(floatDirection, 4).ToArray();
        }

        public VoxelFace VoxelFace;

        public Vector3Int Direction;

    public Vector3[] Normals;

        public int[] VertexIndices;

        public int[] UVIndices;
    }
    
    private VoxelFaceData[] _voxelFaceData = new VoxelFaceData[]
    {
        new VoxelFaceData( 
            VoxelFace.Bottom, 
            Vector3Int.down, 
            Vector3.down,
            new int[] { 0, 1, 2, 3 },
            new int[] { 0, 1, 3, 2 } 
        ),
        new VoxelFaceData( 
            VoxelFace.Top, 
            Vector3Int.up, 
            Vector3.up,
            new int[] { 5, 4, 7, 6 },
            new int[] { 3, 2, 0, 1 } 
        ),
        new VoxelFaceData( 
            VoxelFace.Front, 
            Vector3Int.back, 
            Vector3.back,
            new int[] { 0, 4, 5, 1 },
            new int[] { 2, 0, 1, 3 } 
        ),
        new VoxelFaceData( 
            VoxelFace.Back, 
            Vector3Int.forward, 
            Vector3.forward,
            new int[] { 3, 2, 6, 7 },
            new int[] { 3, 2, 0, 1 } 
        ),
        new VoxelFaceData( 
            VoxelFace.Left, 
            Vector3Int.left, 
            Vector3.left,
            new int[] { 4, 0, 3, 7 },
            new int[] { 1, 3, 2, 0 } 
        ),
        new VoxelFaceData( 
            VoxelFace.Right, 
            Vector3Int.right, 
            Vector3.right,
            new int[] { 5, 6, 2, 1 },
            new int[] { 0, 1, 3, 2 } 
        )
    };

    private VoxelWorld _world;
    private readonly Material _textureAtlasMaterial;
    private readonly Material _textureAtlasTransparentMaterial;

    public ChunkBuilder(VoxelWorld world, Material textureAtlasMaterial, Material textureAtlasTransparentMaterial)
    {
        _world = world;
        _textureAtlasMaterial = textureAtlasMaterial;
        _textureAtlasTransparentMaterial = textureAtlasTransparentMaterial;
    }

    public GameObject[] Build(Vector3Int chunkPos, byte[,,] chunkData)
    {
        var chunkGameObj = new GameObject($"Chunk[{chunkPos.x}|{chunkPos.y}|{chunkPos.z}]");
        chunkGameObj.AddComponent<MeshRenderer>();
        var mesh = chunkGameObj.AddComponent<MeshFilter>().mesh;

        var chunkVertices = new List<Vector3>();
        var chunkNormals = new List<Vector3>();
        var chunkUVs = new List<Vector2>();
        var chunkTriangles = new List<int>();

        var chunkTpGameObj = new GameObject($"ChunkTP[{chunkPos.x}|{chunkPos.y}|{chunkPos.z}]");
        chunkTpGameObj.AddComponent<MeshRenderer>();
        var meshTp = chunkTpGameObj.AddComponent<MeshFilter>().mesh;

        var chunkVerticesTp = new List<Vector3>();
        var chunkNormalsTp = new List<Vector3>();
        var chunkUVsTp = new List<Vector2>();
        var chunkTrianglesTp = new List<int>();

        var chunkVoxelPos = VoxelPosConverter.ChunkToBaseVoxelPos(chunkPos);
        
        for(int x = 0; x < VoxelInfo.ChunkSize; ++x)
        {
            for(int y = 0; y < VoxelInfo.ChunkSize; ++y)
            {
                for(int z = 0; z < VoxelInfo.ChunkSize; ++z)
                {
                    var voxelType = (VoxelType)chunkData[x, y, z];
                    if(voxelType == VoxelType.Empty) continue;

                    bool isTransparent = !VoxelInfo.IsSolid((VoxelType)chunkData[x, y, z]);

                    // Select which chunk to add mesh to - either solid or transparent
                    var vertices = isTransparent ? chunkVerticesTp : chunkVertices;
                    var normals = isTransparent ? chunkNormalsTp : chunkNormals;
                    var uvs = isTransparent ? chunkUVsTp : chunkUVs;
                    var triangles = isTransparent ? chunkTrianglesTp : chunkTriangles;

                    var voxelPos = chunkVoxelPos + new Vector3Int(x, y, z);

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
                    for(int i = 0; i < _voxelFaceData.Length; ++i)
                    {
                        var faceData = _voxelFaceData[i];
                        if(VoxelSideVisible(voxelType, voxelPos, faceData.Direction))
                        {
                            voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[0]]);
                            voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[1]]);
                            voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[2]]);
                            voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[3]]);

                            normals.AddRange(faceData.Normals);

                            var uv = GetUVsForVoxelType((VoxelType)chunkData[x, y, z], faceData.VoxelFace);
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

        mesh.Clear();
        mesh.vertices = chunkVertices.ToArray();
        mesh.triangles = chunkTriangles.ToArray();
        mesh.normals = chunkNormals.ToArray();
        mesh.uv = chunkUVs.ToArray();
        mesh.Optimize();

        chunkGameObj.transform.position = chunkVoxelPos;
        chunkGameObj.GetComponent<Renderer>().material = _textureAtlasMaterial;

        meshTp.Clear();
        meshTp.vertices = chunkVerticesTp.ToArray();
        meshTp.triangles = chunkTrianglesTp.ToArray();
        meshTp.normals = chunkNormalsTp.ToArray();
        meshTp.uv = chunkUVsTp.ToArray();
        meshTp.Optimize();

        chunkTpGameObj.transform.position = chunkVoxelPos;
        chunkTpGameObj.GetComponent<Renderer>().material = _textureAtlasTransparentMaterial;

        return new GameObject[] {
            chunkGameObj,
            chunkTpGameObj
        };
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
        if(!_tileUVCache.ContainsKey((voxelType, face)))
        {

            var uvOffset = VoxelInfo.GetAtlasUVOffsetForVoxel(voxelType, face);
            var uvTileSize = new Vector2(
                VoxelInfo.TextureTileSize * 1.0f / VoxelInfo.TextureAtlasWidth,
                VoxelInfo.TextureTileSize * 1.0f / VoxelInfo.TextureAtlasHeight
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
