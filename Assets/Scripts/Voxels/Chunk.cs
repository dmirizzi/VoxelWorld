using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk
{
    public Vector3Int ChunkPos { get; private set; }

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
        private set
        {
            _chunkGameObject = value;
        } 
    }

    public Chunk(VoxelWorld voxelWorld, Vector3Int chunkPos)
    {
        _voxelWorld = voxelWorld;
        _chunkData = new ushort[VoxelInfo.ChunkSize, VoxelInfo.ChunkSize, VoxelInfo.ChunkSize];
        _lightMap = new float[VoxelInfo.ChunkSize, VoxelInfo.ChunkSize, VoxelInfo.ChunkSize, 3];

        ChunkPos = chunkPos;
        //CreateNewChunkGameObject();
        _blockAuxiliaryData = new Dictionary<Vector3Int, ushort>();
        _blockGameObject = new Dictionary<Vector3Int, GameObject>();
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
        if(_blockGameObject.ContainsKey(localPos))
        {
            GameObject.Destroy(_blockGameObject[localPos]);
            _blockGameObject.Remove(localPos);
        }
        if(_blockAuxiliaryData.ContainsKey(localPos))
        {
            _blockAuxiliaryData.Remove(localPos);
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
        if(_blockGameObject.ContainsKey(localPos))
        {
            GameObject.Destroy(_blockGameObject[localPos]);
        }
        _blockGameObject[localPos] = obj;
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

    public void SetAuxiliaryData(Vector3Int localPos, ushort data)
    {
        _blockAuxiliaryData[localPos] = data;
    }

    public ushort? GetAuxiliaryData(Vector3Int localPos)
    {
        if(_blockAuxiliaryData.ContainsKey(localPos))
        {
            return _blockAuxiliaryData[localPos];
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

    private Dictionary<Vector3Int, ushort> _blockAuxiliaryData;

    private Dictionary<Vector3Int, GameObject> _blockGameObject;

}
