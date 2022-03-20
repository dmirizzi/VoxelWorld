using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum VoxelFace
{
    Top,
    Bottom,
    Front,
    Back,
    Left,
    Right
}

public enum VoxelType
{
    Empty = 0,
    Grass = 1,
    Dirt = 2,
    Water = 3
}

public static class VoxelInfo
{
    public const int TextureTileSize = 16;

    public const int TextureAtlasWidth = 64;

    public const int TextureAtlasHeight = 16;

    public static bool IsSolid(VoxelType voxelType)
    {
        switch(voxelType)
        {
            case VoxelType.Empty:       return false;
            case VoxelType.Grass:       return true;
            case VoxelType.Dirt:        return true;
            case VoxelType.Water:       return false;

            default: 
                throw new System.ArgumentException($"Invalid voxel type {voxelType}");
        }
    }

    public static float GetVoxelHeightOffset(VoxelType voxelType)
    {
        switch(voxelType)
        {
            case VoxelType.Water:       return 0.075f;
            default:                    return 0.0f;
        }
    }

    public static Vector2 GetAtlasUVOffsetForVoxel(VoxelType voxelType, VoxelFace face)
    {
        var tilePosX = 0;
        var tilePosY = 0;

        switch(voxelType)
        {
            case VoxelType.Empty: throw new System.ArgumentException("No texture for empty voxel!");
            case VoxelType.Grass:
                if(face == VoxelFace.Top)
                {
                    tilePosX = 1;
                    tilePosY = 0;
                }
                else if(face == VoxelFace.Bottom)
                {
                    tilePosX = 2;
                    tilePosY = 0;
                }
                else 
                {
                    tilePosX = 0;
                    tilePosY = 0;
                }
            break;
            case VoxelType.Dirt:
                tilePosX = 2;
                tilePosY = 0;
            break;
            case VoxelType.Water:
                tilePosX = 3;
                tilePosY = 0;
            break;
        }

        return new Vector2(
            (float)TextureTileSize / TextureAtlasWidth * tilePosX,
            (float)TextureTileSize / TextureAtlasHeight * tilePosY
        );
    }
}


public class WorldGen : MonoBehaviour
{
    public Material TextureAtlasMaterial;

    public Material TextureAtlasTransparentMaterial;

    public const int ChunkSize = 32;
    public const float VoxelSize = 1f;

    private Dictionary<Vector3Int, byte[,,]> _chunks;

    public WorldGen()
    {
        _chunks = new Dictionary<Vector3Int, byte[,,]>();
    }

    public void SetVoxel(int x, int y, int z, VoxelType type)
    {
        var chunk = GetChunkFromVoxelPosition(x, y, z, true);
        var chunkLocalPos = GlobalToChunkLocalVoxelPos(new Vector3Int(x, y, z));
        chunk[chunkLocalPos.x, chunkLocalPos.y, chunkLocalPos.z] = (byte)type;
    }

    public VoxelType GetVoxel(int x, int y, int z)
    {
        var chunkLocalPos = GlobalToChunkLocalVoxelPos(new Vector3Int(x, y, z));
        var chunkData = GetChunkFromVoxelPosition(x, y, z, false);
        if(chunkData == null)
        {
            return VoxelType.Empty;
        }
        return (VoxelType)chunkData[chunkLocalPos.x, chunkLocalPos.y, chunkLocalPos.z];
    }

    public VoxelType GetVoxel(Vector3Int voxelPos)
    {
        return GetVoxel(voxelPos.x, voxelPos.y, voxelPos.z);
    }

    private byte[,,] GetChunkFromVoxelPosition(int x, int y, int z, bool create)
    {

        var voxelPos = new Vector3Int(x, y, z);
        var chunkPos = VoxelToChunkPos(voxelPos);

        if(!_chunks.ContainsKey(chunkPos))
        {
            if(create)
            {
                _chunks.Add(chunkPos, new byte[ChunkSize, ChunkSize, ChunkSize]);
            }
            else
            {
                return null;
            }

        }
        return _chunks[chunkPos];        
    }

    private Vector3Int GlobalToChunkLocalVoxelPos(Vector3Int voxelPos)
    {
        var x = voxelPos.x % ChunkSize;
        if(x != 0 && voxelPos.x < 0) x += ChunkSize;
        var y = voxelPos.y % ChunkSize;
        if(y != 0 && voxelPos.y < 0) y += ChunkSize;
        var z = voxelPos.z % ChunkSize;
        if(z != 0 && voxelPos.z < 0) z += ChunkSize;

        return new Vector3Int(x, y, z);
    }

    private Vector3Int VoxelToChunkPos(Vector3Int voxelPos)
    {
        var x = (int)voxelPos.x;
        if(voxelPos.x < 0) x += 1;
        x /= ChunkSize;
        if(voxelPos.x < 0) x -= 1;

        var y = (int)voxelPos.y;
        if(voxelPos.y < 0) y += 1;
        y /= ChunkSize;
        if(voxelPos.y < 0) y -= 1;

        var z = (int)voxelPos.z;
        if(voxelPos.z < 0) z += 1;
        z /= ChunkSize;
        if(voxelPos.z < 0) z -= 1;

        return new Vector3Int(x, y, z);
    }

    private Vector3Int ChunkToBaseVoxelPos(Vector3Int chunkPos)
    {
        return new Vector3Int(
            chunkPos.x * ChunkSize,
            chunkPos.y * ChunkSize,
            chunkPos.z * ChunkSize
        );
    }

    private bool VoxelSideVisible(VoxelType voxelType, Vector3Int voxelPos, Vector3Int direction)
    {
        var neighbor = GetVoxel(voxelPos + direction);

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

    public void Build()
    {
        foreach(var chunkPos in _chunks.Keys)
        {
            var chunkData = _chunks[chunkPos];

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

            var chunkVoxelPos = ChunkToBaseVoxelPos(chunkPos);

            for(int x = 0; x < ChunkSize; ++x)
            {
                for(int y = 0; y < ChunkSize; ++y)
                {
                    for(int z = 0; z < ChunkSize; ++z)
                    {
                        var voxelType = (VoxelType)chunkData[x, y, z];

                        if(voxelType == VoxelType.Empty) continue;
                        bool isTransparent = !VoxelInfo.IsSolid((VoxelType)chunkData[x, y, z]);

                        var vertices = isTransparent ? chunkVerticesTp : chunkVertices;
                        var normals = isTransparent ? chunkNormalsTp : chunkNormals;
                        var uvs = isTransparent ? chunkUVsTp : chunkUVs;
                        var triangles = isTransparent ? chunkTrianglesTp : chunkTriangles;

                        var heightOffset = VoxelInfo.GetVoxelHeightOffset(voxelType);

                        var v = new Vector3[]
                        {
                             new Vector3(x, y, z),
                             new Vector3(x + VoxelSize, y, z),
                             new Vector3(x + VoxelSize, y, z + VoxelSize),
                             new Vector3(x, y, z + VoxelSize),

                             new Vector3(x, y + VoxelSize - heightOffset, z),
                             new Vector3(x + VoxelSize, y + VoxelSize - heightOffset, z),
                             new Vector3(x + VoxelSize, y + VoxelSize - heightOffset, z + VoxelSize),
                             new Vector3(x, y + VoxelSize - heightOffset, z + VoxelSize)
                        };

                        var voxelVertices = new List<Vector3>();
                        var voxelPos = chunkVoxelPos + new Vector3Int(x, y, z);

                        if(VoxelSideVisible(voxelType, voxelPos, Vector3Int.down))
                        {
                            var uv = GetUVsForTile((VoxelType)chunkData[x, y, z], VoxelFace.Bottom);
                            voxelVertices.AddRange(new Vector3[]{ v[0], v[1], v[2], v[3] });
                            normals.AddRange(Enumerable.Repeat(Vector3.down, 4));
                            uvs.AddRange(new Vector2[]{ uv[0], uv[1], uv[3], uv[2] });
                        }        
                        if(VoxelSideVisible(voxelType, voxelPos, Vector3Int.up))          
                        {
                            var uv = GetUVsForTile((VoxelType)chunkData[x, y, z], VoxelFace.Top);
                            voxelVertices.AddRange(new Vector3[]{ v[5], v[4], v[7], v[6] });                        
                            normals.AddRange(Enumerable.Repeat(Vector3.up, 4));
                            uvs.AddRange(new Vector2[]{ uv[3], uv[2], uv[0], uv[1] });
                        }
                        if(VoxelSideVisible(voxelType, voxelPos, Vector3Int.back))        
                        {
                            var uv = GetUVsForTile((VoxelType)chunkData[x, y, z], VoxelFace.Front);
                            voxelVertices.AddRange(new Vector3[]{ v[0], v[4], v[5], v[1] });
                            normals.AddRange(Enumerable.Repeat(Vector3.back, 4));
                            uvs.AddRange(new Vector2[]{ uv[2], uv[0], uv[1], uv[3] });
                        }
                        if(VoxelSideVisible(voxelType, voxelPos, Vector3Int.forward))     
                        {
                            var uv = GetUVsForTile((VoxelType)chunkData[x, y, z], VoxelFace.Back);
                            voxelVertices.AddRange(new Vector3[]{ v[3], v[2], v[6], v[7] });
                            normals.AddRange(Enumerable.Repeat(Vector3.forward, 4));
                            uvs.AddRange(new Vector2[]{ uv[3], uv[2], uv[0], uv[1] });
                        }
                        if(VoxelSideVisible(voxelType, voxelPos, Vector3Int.left))        
                        {
                            var uv = GetUVsForTile((VoxelType)chunkData[x, y, z], VoxelFace.Left);
                            voxelVertices.AddRange(new Vector3[]{ v[4], v[0], v[3], v[7] });
                            normals.AddRange(Enumerable.Repeat(Vector3.left, 4));
                            uvs.AddRange(new Vector2[]{ uv[1], uv[3], uv[2], uv[0] });
                        }
                        if(VoxelSideVisible(voxelType, voxelPos, Vector3Int.right))       
                        {
                            var uv = GetUVsForTile((VoxelType)chunkData[x, y, z], VoxelFace.Right);
                            voxelVertices.AddRange(new Vector3[]{ v[5], v[6], v[2], v[1] });
                            normals.AddRange(Enumerable.Repeat(Vector3.right, 4));
                            uvs.AddRange(new Vector2[]{ uv[0], uv[1], uv[3], uv[2] });
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
            chunkGameObj.GetComponent<Renderer>().material = TextureAtlasMaterial;

            meshTp.Clear();
            meshTp.vertices = chunkVerticesTp.ToArray();
            meshTp.triangles = chunkTrianglesTp.ToArray();
            meshTp.normals = chunkNormalsTp.ToArray();
            meshTp.uv = chunkUVsTp.ToArray();
            meshTp.Optimize();

            chunkTpGameObj.transform.position = chunkVoxelPos;
            chunkTpGameObj.GetComponent<Renderer>().material = TextureAtlasTransparentMaterial;
        }
    }

    private Vector2[] GetUVsForTile(VoxelType voxelType, VoxelFace face)
    {
        var uvOffset = VoxelInfo.GetAtlasUVOffsetForVoxel(voxelType, face);
        var uvTileSize = new Vector2(
            VoxelInfo.TextureTileSize * 1.0f / VoxelInfo.TextureAtlasWidth,
            VoxelInfo.TextureTileSize * 1.0f / VoxelInfo.TextureAtlasHeight
        );
        return new Vector2[]
        {
            uvOffset + new Vector2(0f, 0f),
            uvOffset + new Vector2(uvTileSize.x, 0f),
            uvOffset + new Vector2(0f, -uvTileSize.y),
            uvOffset + new Vector2(uvTileSize.x, -uvTileSize.y)
        };
    }

    void Start()
    {
        var seed = Random.Range(0, 1000);

        for(int x = 0; x < 64; ++x)
        {
            for(int z = 0; z < 64; ++z)
            {
                var height = Mathf.Min(0, (int)(Mathf.PerlinNoise(seed + x / 20.0f, seed + z / 20.0f) * 16) - 3);

                bool isWater = height < 0;
                if(isWater) height = 0;

                for(int y = -16; y <= height; ++y)
                {
                    if(isWater)
                    {
                        if(y == -16)
                        {
                            SetVoxel(x, y, z, VoxelType.Dirt);
                        }
                        else
                        {
                            SetVoxel(x, y, z, VoxelType.Water);
                        }
                    }
                    else
                    {
                        if(y < height)
                        {
                            SetVoxel(x, y, z, VoxelType.Dirt);
                        }
                        else
                        {
                            SetVoxel(x, y, z, VoxelType.Grass);
                        }
                    }                    
                }
            }
        }
        Build();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
