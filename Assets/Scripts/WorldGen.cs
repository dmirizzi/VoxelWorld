using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum VoxelType
{
    Empty = 0,
    Grass = 1,
    Dirt = 2,
    Water = 3
}

public static class VoxelInfo
{
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
}


public class WorldGen : MonoBehaviour
{
    public Material GrassSideMaterial;
    public Material GrassTopMaterial;

    public Material WaterMaterial;

    public Material DirtMaterial;

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
        if(x < 0) x += ChunkSize;
        var y = voxelPos.y % ChunkSize;
        if(y < 0) y += ChunkSize;
        var z = voxelPos.z % ChunkSize;
        if(z < 0) z += ChunkSize;

        return new Vector3Int(x, y, z);
    }

    private Vector3Int VoxelToChunkPos(Vector3Int voxelPos)
    {
        var x = (int)voxelPos.x / ChunkSize;
        if(voxelPos.x < 0) x -= 1;
        var y = (int)voxelPos.y / ChunkSize;
        if(voxelPos.y < 0) y -= 1;
        var z = (int)voxelPos.z / ChunkSize;
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

    public void Build()
    {
        foreach(var chunkPos in _chunks.Keys)
        {
            var chunkData = _chunks[chunkPos];
            var chunkGameObj = new GameObject($"Chunk[{chunkPos.x}|{chunkPos.y}|{chunkPos.x}]");

            chunkGameObj.AddComponent<MeshRenderer>();
            var mesh = chunkGameObj.AddComponent<MeshFilter>().mesh;

            var chunkVertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            var chunkVoxelPos = ChunkToBaseVoxelPos(chunkPos);
            for(int x = 0; x < ChunkSize; ++x)
            {
                for(int y = 0; y < ChunkSize; ++y)
                {
                    for(int z = 0; z < ChunkSize; ++z)
                    {
                        if(chunkData[x, y, z] == (byte)VoxelType.Empty) continue;
                        
                        var v = new Vector3[]
                        {
                             new Vector3(x, y, z),
                             new Vector3(x + VoxelSize, y, z),
                             new Vector3(x + VoxelSize, y, z + VoxelSize),
                             new Vector3(x, y, z + VoxelSize),
                             new Vector3(x, y + VoxelSize, z),
                             new Vector3(x + VoxelSize, y + VoxelSize, z),
                             new Vector3(x + VoxelSize, y + VoxelSize, z + VoxelSize),
                             new Vector3(x, y + VoxelSize, z + VoxelSize)
                        };

                        var uv = new Vector2[]
                        {
                            new Vector2(0f, 0f),
                            new Vector2(1f, 0f),
                            new Vector2(0f, -1f),
                            new Vector2(1f, -1f)
                        };

                        var vertices = new List<Vector3>();
                        var voxelPos = chunkVoxelPos + new Vector3Int(x, y, z);
                        if(!VoxelInfo.IsSolid(GetVoxel(voxelPos + Vector3Int.down)))
                        {
                            vertices.AddRange(new Vector3[]{ v[0], v[1], v[2], v[3] });
                            normals.AddRange(Enumerable.Repeat(Vector3.down, 4));
                            uvs.AddRange(new Vector2[]{ uv[0], uv[1], uv[3], uv[2] });
                        }        
                        if(!VoxelInfo.IsSolid(GetVoxel(voxelPos + Vector3Int.up)))          
                        {
                            vertices.AddRange(new Vector3[]{ v[5], v[4], v[7], v[6] });                        
                            normals.AddRange(Enumerable.Repeat(Vector3.up, 4));
                            uvs.AddRange(new Vector2[]{ uv[3], uv[2], uv[0], uv[1] });
                        }
                        if(!VoxelInfo.IsSolid(GetVoxel(voxelPos + Vector3Int.back)))        
                        {
                            vertices.AddRange(new Vector3[]{ v[0], v[4], v[5], v[1] });
                            normals.AddRange(Enumerable.Repeat(Vector3.back, 4));
                            uvs.AddRange(new Vector2[]{ uv[2], uv[0], uv[1], uv[3] });
                        }
                        if(!VoxelInfo.IsSolid(GetVoxel(voxelPos + Vector3Int.forward)))     
                        {
                            vertices.AddRange(new Vector3[]{ v[3], v[2], v[6], v[7] });
                            normals.AddRange(Enumerable.Repeat(Vector3.forward, 4));
                            uvs.AddRange(new Vector2[]{ uv[3], uv[2], uv[0], uv[1] });
                        }
                        if(!VoxelInfo.IsSolid(GetVoxel(voxelPos + Vector3Int.left)))        
                        {
                            vertices.AddRange(new Vector3[]{ v[4], v[0], v[3], v[7] });
                            normals.AddRange(Enumerable.Repeat(Vector3.left, 4));
                            uvs.AddRange(new Vector2[]{ uv[1], uv[3], uv[2], uv[0] });
                        }
                        if(!VoxelInfo.IsSolid(GetVoxel(voxelPos + Vector3Int.right)))       
                        {
                            vertices.AddRange(new Vector3[]{ v[5], v[6], v[2], v[1] });
                            normals.AddRange(Enumerable.Repeat(Vector3.right, 4));
                            uvs.AddRange(new Vector2[]{ uv[0], uv[1], uv[3], uv[2] });
                        }

                        int vertexBaseIdx = chunkVertices.Count;
                        chunkVertices.AddRange(vertices);

                        for(int i = 0; i < vertices.Count; i += 4)
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
            mesh.triangles = triangles.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.Optimize();

            chunkGameObj.transform.position = chunkVoxelPos;
            chunkGameObj.GetComponent<Renderer>().material = GrassSideMaterial;
        }

    }


    void Start()
    {
        for(int x = 0; x < 32; ++x)
        {
            for(int z = 0; z < 32; ++z)
            {
                SetVoxel(x, 0, z, VoxelType.Grass);
            }
        }
        Build();

        /*
        for(int x = 0; x < 30; ++x)
        {
            for(int z = 0; z < 30; ++z)
            {
                var waterTile = Random.Range(0f, 1f) <= 0.2f;

                if(waterTile)
                {
                    CreateCube(new Vector3Int(x, 0, z), WaterMaterial, WaterMaterial, 0.075f);
                    CreateCube(new Vector3Int(x, -1, z), DirtMaterial, DirtMaterial);
                }
                else
                {
                    CreateCube(new Vector3Int(x, 0, z), GrassSideMaterial, GrassTopMaterial);
                }
            }
        }*/
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
