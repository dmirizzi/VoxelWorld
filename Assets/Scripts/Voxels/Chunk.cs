using System.Collections.Generic;
using UnityEngine;


public class Chunk
{
    public const int SunlightChannel = 3;

    public Vector3Int ChunkPos { get; private set; }

    public GameObject ChunkGameObject { get; private set; }

    public Chunk(VoxelWorld voxelWorld, Vector3Int chunkPos)
    {
        _voxelWorld = voxelWorld;
        ChunkPos = chunkPos;

        _chunkData = new ushort[VoxelInfo.ChunkSize, VoxelInfo.ChunkSize, VoxelInfo.ChunkSize];
        ResetLightMap();
    }

    public void ConnectNeighbor(Chunk neighborChunk, bool propagate)
    {
        var offset = (neighborChunk.ChunkPos - ChunkPos) + Vector3Int.one;
        _neighboringChunks[offset.x, offset.y, offset.z] = neighborChunk;

        if(propagate == true)
        {
            neighborChunk.ConnectNeighbor(this, false);
        }
    }

    public ushort GetVoxelInsideChunk(Vector3Int localPos)
    {
        return _chunkData[localPos.x, localPos.y, localPos.z];
    }

    public ushort GetVoxelInsideChunk(int localX, int localY, int localZ)
    {
        return _chunkData[localX, localY, localZ];
    }

    public ushort GetVoxel(Vector3Int localPos)
    {
        if(LocalVoxelPosIsInChunk(localPos))
        {
            return _chunkData[localPos.x, localPos.y, localPos.z];
        }
        else if(TryGetNeighboringChunkVoxel(localPos, out var neighborChunk, out var neighborLocalPos))
        {
            return neighborChunk.GetVoxelInsideChunk(neighborLocalPos);
        }
        else
        {
            return 0;
        }
    }

    public ushort GetVoxel(int localX, int localY, int localZ)
    {
        if(LocalVoxelPosIsInChunk(localX, localY, localZ))
        {
            return _chunkData[localX, localY, localZ];
        }
        else if(TryGetNeighboringChunkVoxel(localX, localY, localZ, out var neighborChunk, out var neighborX, out var neighborY, out var neighborZ))
        {
            return neighborChunk.GetVoxelInsideChunk(neighborX, neighborY, neighborZ);
        }
        else
        {
            return 0;
        }
    }    

    public bool SetVoxel(
        Vector3Int localPos,
        ushort type,
        BlockFace? placementFace = null,
        BlockFace? lookDir = null,
        bool useExistingAuxData = false)
    {
        var oldVoxelType = _chunkData[localPos.x, localPos.y, localPos.z];
        var oldBlockType = BlockTypeRegistry.GetBlockType(oldVoxelType);
        var newBlockType = BlockTypeRegistry.GetBlockType(type);

        _chunkData[localPos.x, localPos.y, localPos.z] = type;

        if (oldBlockType == null && newBlockType == null)
        {
            // Shortcut for simple voxel to voxel change
            return true;
        }

        // Remove old aux data and gameobjects if it already exists at this voxel position
        if (_blockGameObjects.ContainsKey(localPos))
        {
            GameObject.Destroy(_blockGameObjects[localPos]);
            _blockGameObjects.Remove(localPos);
        }

        // Remove old block if one already exists at this position
        if (oldBlockType != null)
        {
            var globalPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(localPos, ChunkPos);

            // Remove existing voxel collider at this position if any
            if (_voxelColliderGameObjects.ContainsKey(localPos))
            {
                GameObject.Destroy(_voxelColliderGameObjects[localPos]);
                _voxelColliderGameObjects.Remove(localPos);
            }

            // Execute remove logic on old block if available 
            if (!oldBlockType.OnRemove(_voxelWorld, this, globalPos, localPos))
            {
                // Block cannot be removed
                return false;
            }
        }

        // Clear auxiliary data before setting new voxel unless it should explicitly be kept,
        // e.g. when setting initial aux data for a new voxel
        if (!useExistingAuxData && _blockAuxiliaryData.ContainsKey(localPos))
        {
            _blockAuxiliaryData.Remove(localPos);
        }

        // Execute place logic on old block if available
        if (newBlockType != null)
        {
            var globalPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(localPos, ChunkPos);

            if (!newBlockType.OnPlace(_voxelWorld, this, globalPos, localPos, placementFace, lookDir))
            {
                // Block cannot be placed
                return false;
            }
        }

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

    public void AddChunkMeshGameObjects(params GameObject[] gameObjects)
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

    public IReadOnlyDictionary<Vector3Int, ushort> GetAllAuxiliaryData() => _blockAuxiliaryData;

    public ReadOnly3DArray<ushort> GetAllVoxelData() => new ReadOnly3DArray<ushort>(_chunkData);

    // Populates an empty chunk directly from a generated voxel buffer. Assumes no prior data exists —
    // plain voxels are written directly to _chunkData, special voxels go through SetVoxel so OnPlace fires.
    public void PopulateFromBuffer(ushort[,,] voxelData)
    {
        for (int z = 0; z < VoxelInfo.ChunkSize; ++z)
        {
            for (int y = 0; y < VoxelInfo.ChunkSize; ++y)
            {
                for (int x = 0; x < VoxelInfo.ChunkSize; ++x)
                {
                    var type = voxelData[x, y, z];
                    if (type == 0) continue;

                    if (BlockTypeRegistry.GetBlockType(type) != null)
                    {
                        SetVoxel(new Vector3Int(x, y, z), type);
                    }
                    else
                    {
                        _chunkData[x, y, z] = type;
                    }
                }
            }
        }
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
        if(ChunkGameObject == null)
        {
            ChunkGameObject = new GameObject($"Chunk[{ChunkPos.x}|{ChunkPos.y}|{ChunkPos.z}]");
            var chunkVoxelPos = VoxelPosHelper.ChunkPosToGlobalChunkBaseVoxelPos(ChunkPos);
            ChunkGameObject.transform.position = chunkVoxelPos;
        }
        else
        {
            for(int i = 0; i < ChunkGameObject.transform.childCount; ++i)
            {
                var child = ChunkGameObject.transform.GetChild(i);
                if(!child.name.StartsWith("Gizmo"))
                {
                    GameObject.Destroy(child.gameObject);
                }
            }
        }

        _voxelColliderGameObjects.Clear();
        _blockGameObjects.Clear();
    }

    public void ResetLightMap()
    {
        _lightMap = new ushort[VoxelInfo.ChunkSize, VoxelInfo.ChunkSize, VoxelInfo.ChunkSize];
    }

    public void SetLightChannelValue(Vector3Int pos, int channel, byte intensity)
    {        
        var mask = (ushort)~(0xF << (channel * 4));
        var maskedOldValue = (_lightMap[pos.x, pos.y, pos.z] & mask);
        var newValue = (ushort)((intensity & 0xF) << (channel * 4));
        _lightMap[pos.x, pos.y, pos.z] = (ushort)(maskedOldValue | newValue);
    }

    public byte GetLightChannelValue(Vector3Int pos, int channel)
    {
        return (byte)((_lightMap[pos.x, pos.y, pos.z] >> (channel * 4)) & 0xF);
    }

    public bool LocalVoxelPosIsInChunk(Vector3Int pos)
    {
        int mask = VoxelInfo.ChunkSize - 1;
        return ((pos.x | pos.y | pos.z) & ~mask) == 0;
    }
    
    public bool LocalVoxelPosIsInChunk(int x, int y, int z)
    {
        int mask = VoxelInfo.ChunkSize - 1;
        return ((x | y | z) & ~mask) == 0;
    }

    public bool TryGetNeighboringChunkVoxel(Vector3Int pos, out Chunk neighborChunk, out Vector3Int neighborChunkLocalVoxelPos)
    {
        var neighborChunkIndex = VoxelPosHelper.LocalVoxelPosToRelativeChunkPos(pos) + Vector3Int.one;

        neighborChunk = _neighboringChunks[neighborChunkIndex.x, neighborChunkIndex.y, neighborChunkIndex.z];
        neighborChunkLocalVoxelPos = VoxelPosHelper.LocalVoxelPosToNeighboringLocalVoxelPos(pos);

        return neighborChunk != null;
    }

    public bool TryGetNeighboringChunkVoxel(int x, int y, int z, out Chunk neighborChunk, out int neighborX, out int neighborY, out int neighborZ)
    {
        VoxelPosHelper.LocalVoxelPosToRelativeChunkPos(x, y, z, out var neighborChunkX, out var neighborChunkY, out var neighborChunkZ);
        neighborChunk = _neighboringChunks[neighborChunkX, neighborChunkY, neighborChunkZ];

        VoxelPosHelper.LocalVoxelPosToNeighboringLocalVoxelPos(x, y, z, out neighborX, out neighborY, out neighborZ);

        return neighborChunk != null;
    }
    
    public bool HasAnyBlockLight(Vector3Int pos)
    {
        return (_lightMap[pos.x, pos.y, pos.z] & 0xFFF) > 0;
    }

    public bool HasAnyLight(Vector3Int pos)
    {
        return _lightMap[pos.x, pos.y, pos.z] > 0;
    }

    // SSSS.RRRR.GGGG.BBBB
    // Where S = Sunlight level
    private ushort[,,] _lightMap;

    private ushort[,,] _chunkData;

    private VoxelWorld _voxelWorld;

    private GameObject _chunkGameObject;

    private Queue<(Vector3Int LocalVoxelPos, Bounds Bounds)> _voxelCollidersToBuild = new Queue<(Vector3Int, Bounds)>();

    private Dictionary<Vector3Int, GameObject> _voxelColliderGameObjects = new Dictionary<Vector3Int, GameObject>();

    private Dictionary<Vector3Int, ushort> _blockAuxiliaryData = new Dictionary<Vector3Int, ushort>();

    private Dictionary<Vector3Int, GameObject> _blockGameObjects = new Dictionary<Vector3Int, GameObject>();

    // Offsets are shifted by one, so range is (0-2), so 0 would be -1 etc.
    private Chunk[,,] _neighboringChunks = new Chunk[3, 3, 3];
}
