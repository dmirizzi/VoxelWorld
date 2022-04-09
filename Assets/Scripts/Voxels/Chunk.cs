using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk
{
    public Vector3Int ChunkPos { get; set; }

    public GameObject ChunkGameObject { get; set; }

    public Dictionary<Vector3Int, byte> BlockAuxiliaryData { get; set; }

    public Dictionary<Vector3Int, GameObject> BlockGameObject { get; set; }

    public Chunk(Vector3Int chunkPos)
    {
        ChunkPos = chunkPos;
        CreateNewChunkGameObject();
        _chunkData = new byte[VoxelInfo.ChunkSize, VoxelInfo.ChunkSize, VoxelInfo.ChunkSize];
        BlockAuxiliaryData = new Dictionary<Vector3Int, byte>();
        BlockGameObject = new Dictionary<Vector3Int, GameObject>();
    }

    public VoxelType GetVoxel(Vector3Int localPos)
    {
        return (VoxelType)_chunkData[localPos.x, localPos.y, localPos.z];
    }

    public VoxelType GetVoxel(int localX, int localY, int localZ)
    {
        return (VoxelType)_chunkData[localX, localY, localZ];
    }

    public void SetVoxel(Vector3Int localPos, VoxelType type)
    {
        if(BlockGameObject.ContainsKey(localPos))
        {
            GameObject.Destroy(BlockGameObject[localPos]);
            BlockGameObject.Remove(localPos);
        }
        if(BlockAuxiliaryData.ContainsKey(localPos))
        {
            BlockAuxiliaryData.Remove(localPos);
        }
        _chunkData[localPos.x, localPos.y, localPos.z] = (byte)type;
    }

    public void AddBlockGameObject(Vector3Int localPos, GameObject obj)
    {
        if(BlockGameObject.ContainsKey(localPos))
        {
            GameObject.Destroy(BlockGameObject[localPos]);
        }
        BlockGameObject[localPos] = obj;
        obj.transform.parent = ChunkGameObject.transform;
        obj.transform.localPosition = VoxelPosConverter.GetVoxelCenterSurfaceWorldPos(localPos);
    }

    public void AddVoxelMeshGameObjects(params GameObject[] gameObjects)
    {
        foreach(var go in gameObjects)
        {
            go.transform.parent = ChunkGameObject.transform;
            go.transform.localPosition = Vector3.zero;
        }
    }

    public void SetAuxiliaryData(Vector3Int localPos, byte data)
    {
        BlockAuxiliaryData[localPos] = data;
    }

    public byte? GetAuxiliaryData(Vector3Int localPos)
    {
        if(BlockAuxiliaryData.ContainsKey(localPos))
        {
            return BlockAuxiliaryData[localPos];
        }
        return null;
    }

    public void BuildBlockGameObjects()
    {
        for(int z = 0; z < VoxelInfo.ChunkSize; ++z)
        {
            for(int y = 0; y < VoxelInfo.ChunkSize; ++y)
            {
                for(int x = 0; x < VoxelInfo.ChunkSize; ++x)
                {
                    var blockType = BlockTypes.GetBlockType((VoxelType)_chunkData[x, y, z]);
                    if(blockType != null)
                    {
                        blockType.OnChunkBuild(this, new Vector3Int(x, y, z));
                    }
                }                
            }
        }
    }

    public void DestroyGameObject()
    {
        if(ChunkGameObject != null)
        {
            GameObject.Destroy(ChunkGameObject);
        }
        CreateNewChunkGameObject();
    }

    private void CreateNewChunkGameObject()
    {
        ChunkGameObject = new GameObject($"Chunk[{ChunkPos.x}|{ChunkPos.y}|{ChunkPos.z}]");
        var chunkVoxelPos = VoxelPosConverter.ChunkToBaseVoxelPos(ChunkPos);
        ChunkGameObject.transform.position = chunkVoxelPos;
    }

    private byte[,,] _chunkData;
}
