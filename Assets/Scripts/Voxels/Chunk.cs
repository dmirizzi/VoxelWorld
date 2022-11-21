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
    }

    public ushort GetVoxel(Vector3Int localPos)
    {
        return _chunkData[localPos.x, localPos.y, localPos.z];
    }

    public ushort GetVoxel(int localX, int localY, int localZ)
    {
        return _chunkData[localX, localY, localZ];
    }

    public bool SetVoxel(Vector3Int localPos, ushort type, BlockFace? placementFace = null, BlockFace? lookDir = null, bool useExistingAuxData = false)
    {
        // Remove old aux data and gameobjects if it already exists at this voxel position
        if(_blockGameObjects.ContainsKey(localPos))
        {
            GameObject.Destroy(_blockGameObjects[localPos]);
            _blockGameObjects.Remove(localPos);
        }

        var globalPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(localPos, ChunkPos);

        var oldVoxelType = _chunkData[localPos.x, localPos.y, localPos.z];
        var oldBlockType = BlockTypeRegistry.GetBlockType(oldVoxelType);

        // Delete old voxel if one already exists at this position
        if(oldBlockType != null)
        {
            // Remove existing voxel collider at this position if any
            if(_voxelColliderGameObjects.ContainsKey(localPos))
            {
                GameObject.Destroy(_voxelColliderGameObjects[localPos]);
                _voxelColliderGameObjects.Remove(localPos);
            }            

            // Execute remove logic on old block if available 
            if(!oldBlockType.OnRemove(_voxelWorld, this, globalPos, localPos))
            {
                 // Block cannot be removed
                return false;
            }
        }

        // Clear auxiliary data before setting new voxel unless it should explicitly be kept,
        // e.g. when setting initial aux data for a new voxel
        if(!useExistingAuxData && _blockAuxiliaryData.ContainsKey(localPos))
        {
            _blockAuxiliaryData.Remove(localPos);
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

    // Bounds should already contain chunk-local voxel position in bounds.center
    public void AddVoxelCollider(Vector3Int localPos, Bounds bounds)
    {
        // We add it to the building queue first for the actual game objects to be created later in BuildVoxelColliderGameObjects
        // because we are not on the main thread here
        _voxelCollidersToBuild.Enqueue((localPos, bounds));
    }

    public void AddBlockGameObject(Vector3Int localPos, GameObject obj)
    {
        if(_blockGameObjects.ContainsKey(localPos))
        {
            GameObject.Destroy(_blockGameObjects[localPos]);
        }
        _blockGameObjects[localPos] = obj;
        obj.transform.parent = ChunkGameObject.transform;
        obj.transform.localPosition = VoxelPosHelper.GetVoxelCenterSurfaceWorldPos(localPos);
        obj.SetLayer(SceneLayers.VoxelsLayer);
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

    public void ClearAuxiliaryData(Vector3Int localPos)
    {
        _blockAuxiliaryData.Remove(localPos);
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
                        var localPos = new Vector3Int(x, y, z);
                        var globalPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(localPos, ChunkPos);
                        blockType.OnChunkBuild(_voxelWorld, this, globalPos, localPos);
                    }
                }                
            }
        }
    }

    public void BuildVoxelColliderGameObjects()
    {
        while(_voxelCollidersToBuild.TryDequeue(out var buildCollider))
        {
            var globalVoxelPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(buildCollider.LocalVoxelPos, ChunkPos);

            var gameObj = new GameObject($"VoxelCollider{globalVoxelPos}");
            
            var collider = gameObj.AddComponent<BoxCollider>();
            collider.center = buildCollider.Bounds.center;
            collider.size = buildCollider.Bounds.size;

            var colliderScript = gameObj.AddComponent<VoxelCollider>();
            colliderScript.GlobalVoxelPos = globalVoxelPos;

            gameObj.transform.parent = ChunkGameObject.transform;
            gameObj.transform.localPosition = Vector3.zero; // position is already in bounds.center
            gameObj.SetLayer(SceneLayers.VoxelCollidersLayer);
        
            _voxelColliderGameObjects.Add(buildCollider.LocalVoxelPos, gameObj);
        }
    }

    public void Reset()
    {
        //TODO: Not destroying!?
        if(ChunkGameObject != null)
        {
            GameObject.Destroy(ChunkGameObject);
        }
        CreateNewChunkGameObject();

        _voxelColliderGameObjects.Clear();
        _blockGameObjects.Clear();
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
        var chunkVoxelPos = VoxelPosHelper.ChunkToBaseVoxelPos(ChunkPos);
        _chunkGameObject.transform.position = chunkVoxelPos;
    }

    // 5-bit for each color channel (RGB)
    private float[,,,] _lightMap;

    private ushort[,,] _chunkData;

    private VoxelWorld _voxelWorld;

    private GameObject _chunkGameObject;

    private Queue<(Vector3Int LocalVoxelPos, Bounds Bounds)> _voxelCollidersToBuild = new Queue<(Vector3Int, Bounds)>();

    private Dictionary<Vector3Int, GameObject> _voxelColliderGameObjects = new Dictionary<Vector3Int, GameObject>();

    private Dictionary<Vector3Int, ushort> _blockAuxiliaryData = new Dictionary<Vector3Int, ushort>();

    private Dictionary<Vector3Int, GameObject> _blockGameObjects = new Dictionary<Vector3Int, GameObject>();

}
