using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk
{
    public Vector3Int ChunkPos { get; set; }

    public GameObject ChunkGameObject 
    { 
        get
        {
            if(_chunkGameObject == null)
            {
                CreateNewChunkGameObject();
            }
            return _chunkGameObject;
        } 
        set
        {
            _chunkGameObject = value;
        } 
    }

    public Dictionary<Vector3Int, byte> BlockAuxiliaryData { get; set; }

    public Dictionary<Vector3Int, GameObject> BlockGameObject { get; set; }

    public Chunk(VoxelWorld voxelWorld, Vector3Int chunkPos)
    {
        _voxelWorld = voxelWorld;
        _chunkData = new ushort[VoxelInfo.ChunkSize, VoxelInfo.ChunkSize, VoxelInfo.ChunkSize];
        _lightMap = new float[VoxelInfo.ChunkSize, VoxelInfo.ChunkSize, VoxelInfo.ChunkSize, 3];

        ChunkPos = chunkPos;
        BlockAuxiliaryData = new Dictionary<Vector3Int, byte>();
        BlockGameObject = new Dictionary<Vector3Int, GameObject>();
    }

    public ushort GetVoxel(Vector3Int localPos)
    {
        return _chunkData[localPos.x, localPos.y, localPos.z];
    }

    public ushort GetVoxel(int localX, int localY, int localZ)
    {
        return _chunkData[localX, localY, localZ];
    }

    public bool SetVoxel(Vector3Int localPos, ushort type, BlockFace? placementFace = null, BlockFace? lookDir = null)
    {
        // Remove old aux data and gameobjects if it already exists at this voxel position
        if(BlockGameObject.ContainsKey(localPos))
        {
            GameObject.Destroy(BlockGameObject[localPos]);
            BlockGameObject.Remove(localPos);
        }
        if(BlockAuxiliaryData.ContainsKey(localPos))
        {
            BlockAuxiliaryData.Remove(localPos);
        }

        var globalPos = VoxelPosConverter.ChunkLocalVoxelPosToGlobal(localPos, ChunkPos);

        // Execute remove logic on old block if available
        var oldVoxelType = _chunkData[localPos.x, localPos.y, localPos.z];
        var oldBlockType = BlockTypeRegistry.GetBlockType(oldVoxelType);
        if(oldBlockType != null)
        {
            if(!oldBlockType.OnRemove(_voxelWorld, this, globalPos, localPos))
            {
                 // Block cannot be removed
                return false;
            }
        }

        // Execute place logic on old block if available
        var newBlockType = BlockTypeRegistry.GetBlockType(type);
        if(newBlockType != null)
        {
            if(!newBlockType.OnPlace(_voxelWorld, this, globalPos, localPos, placementFace, lookDir))
            {
                // Block cannot be placed
                return false;
            }
        }

        _chunkData[localPos.x, localPos.y, localPos.z] = type;

        return true;
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
                    var blockType = BlockTypeRegistry.GetBlockType(_chunkData[x, y, z]);
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
        //TODO: Not destroying!?
        if(ChunkGameObject != null)
        {
            GameObject.Destroy(ChunkGameObject);
        }
        CreateNewChunkGameObject();
    }

    public void SetLightChannelValue(Vector3Int pos, int channel, float intensity)
    {
        _lightMap[pos.x, pos.y, pos.z, channel] = intensity;
    }

    public float GetLightChannelValue(Vector3Int pos, int channel)
    {
        return _lightMap[pos.x, pos.y, pos.z, channel];
    }

    // 0BBB.BBGG.GGGR.RRRR
    /*
    public void SetRedLightValue(Vector3Int pos, byte intensity)
    {
        var newVal = _lightMap[pos.x, pos.y, pos.z] & 0x7FE0 | (intensity & 0x1F);
        _lightMap[pos.x, pos.y, pos.z] = (ushort)newVal;
    }

    public void SetGreenLightValue(Vector3Int pos, byte intensity)
    {
        var newVal = _lightMap[pos.x, pos.y, pos.z] & 0x7C1F | (intensity & 0x1F);
        _lightMap[pos.x, pos.y, pos.z] = (ushort)newVal;
    }

    public void SetBlueLightValue(Vector3Int pos, byte intensity)
    {
        var newVal = _lightMap[pos.x, pos.y, pos.z] & 0x3FF | (intensity & 0x1F);
        _lightMap[pos.x, pos.y, pos.z] = (ushort)newVal;
    }

    public byte GetRedLightValue(Vector3Int pos)
    {
        return (byte)(_lightMap[pos.x, pos.y, pos.z] & 0x1F);
    }

    public byte GetGreenLightValue(Vector3Int pos)
    {
        return (byte)((_lightMap[pos.x, pos.y, pos.z] & 0x3E0) >> 5);
    }

    public byte GetBlueLightValue(Vector3Int pos)
    {
        return (byte)((_lightMap[pos.x, pos.y, pos.z] & 0x7C00) >> 10);
    }
*/

    private void CreateNewChunkGameObject()
    {
        _chunkGameObject = new GameObject($"Chunk[{ChunkPos.x}|{ChunkPos.y}|{ChunkPos.z}]");
        var chunkVoxelPos = VoxelPosConverter.ChunkToBaseVoxelPos(ChunkPos);
        _chunkGameObject.transform.position = chunkVoxelPos;
    }

    // 5-bit for each color channel (RGB)
    private float[,,,] _lightMap;

    private ushort[,,] _chunkData;

    private VoxelWorld _voxelWorld;

    private GameObject _chunkGameObject;
}
