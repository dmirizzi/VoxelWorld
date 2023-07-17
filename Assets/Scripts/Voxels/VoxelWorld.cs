using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class VoxelWorld : MonoBehaviour
{
    public int MaxNumChunkBuilderTasks = 8;

    public Material TextureAtlasMaterial;

    public Material TextureAtlasTransparentMaterial;

    public VoxelWorld()
    {
        _chunks = new Dictionary<Vector3Int, Chunk>();
        _topMostChunks = new Dictionary<Vector2Int, Chunk>();
        _chunkBuilders = new Dictionary<Vector3Int, ChunkBuilder>();
        _lightMap = new LightMap(this);
    }

    void Awake()
    {
        _player = FindObjectOfType<PlayerController>();
        _updateScheduler = FindObjectOfType<WorldUpdateScheduler>();
    }

    void Update()
    {
    }

    public ChunkBuilder CreateNewChunkBuilder(Vector3Int chunkPos)
    {
        _chunkBuilders[chunkPos] = new ChunkBuilder(this, chunkPos, _chunks[chunkPos], TextureAtlasMaterial, TextureAtlasTransparentMaterial);
        return _chunkBuilders[chunkPos];
    }

    public LightMap GetLightMap() => _lightMap;

    public ChunkBuilder GetChunkBuilder(Vector3Int chunkPos) => _chunkBuilders[chunkPos];

    public Chunk GetChunk(Vector3Int chunkPos) => _chunks[chunkPos];

    public bool TryGetChunk(Vector3Int chunkPos, out Chunk chunk)
    {
        if(_chunks.ContainsKey(chunkPos))
        {
            chunk = _chunks[chunkPos];
            return true;
        }

        chunk = null;
        return false;
    }

    public bool ChunkExists(Vector3Int chunkPos) => _chunks.ContainsKey(chunkPos);

    public bool ChunkBuilderExists(Vector3Int chunkPos) => _chunkBuilders.ContainsKey(chunkPos);


    public void SetVoxel(
        Vector3Int globalPos, 
        ushort type, 
        BlockFace? placementDir = null, 
        BlockFace? lookDir = null, 
        bool useExistingAuxData = false)
    {
        var chunk = GetChunkFromVoxelPosition(globalPos, true);
        var chunkLocalPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(globalPos);

        var oldVoxelType = chunk.GetVoxel(chunkLocalPos);

        if(!chunk.SetVoxel(chunkLocalPos, type, placementDir, lookDir, useExistingAuxData))
        {
            // Voxel cant be placed
            return;
        }

        QueueAffectedChunksForRebuild(globalPos);
    }

    public void SetVoxel(
        int x, int y, int z, 
        ushort type, 
        BlockFace? placementDir = null, 
        BlockFace? lookDir = null, 
        bool useExistingAuxData = false)
    {
        SetVoxel(new Vector3Int(x, y, z), type, placementDir, lookDir, useExistingAuxData);
    }

    public void SetVoxelAndUpdateLightMap(
        Vector3Int globalVoxelPos, 
        ushort type, 
        BlockFace? placementDir = null, 
        BlockFace? lookDir = null, 
        bool useExistingAuxData = false)
    {
        SetVoxelAndUpdateLightMap(
            globalVoxelPos.x, 
            globalVoxelPos.y, 
            globalVoxelPos.z,
            type, 
            placementDir, 
            lookDir,
            useExistingAuxData);
    }

    public void SetVoxelAndUpdateLightMap(
        int x, int y, int z, 
        ushort type, 
        BlockFace? placementDir = null, 
        BlockFace? lookDir = null, 
        bool useExistingAuxData = false)
    {
        var globalPos = new Vector3Int(x, y, z);
        var chunk = GetChunkFromVoxelPosition(x, y, z, true);
        var chunkLocalPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(globalPos);

        var oldVoxelType = chunk.GetVoxel(chunkLocalPos);

        var chunksAffectedByLightUpdate = new HashSet<Vector3Int>();

        // If a transparent voxel was replaced by a non-transparent voxel, remove the light value at this location
        if(VoxelInfo.IsTransparent(oldVoxelType) && !VoxelInfo.IsTransparent(type))
        { 
            RemoveLight(globalPos, false);
            RemoveLight(globalPos, true);
        }

        SetVoxel(x, y, z, type, placementDir, lookDir, useExistingAuxData);        

        // If non-transparent voxel was removed or replaced by a transparent voxel, update light map to propagate the light
        // through this new gap
        if(!VoxelInfo.IsTransparent(oldVoxelType) && VoxelInfo.IsTransparent(type))
        { 
            _lightMap.UpdateOnRemovedSolidVoxel(globalPos, chunksAffectedByLightUpdate);
        }

        QueueChunksForLightMappingUpdate(chunksAffectedByLightUpdate);
    }

    public void SetVoxelSphere(Vector3Int center, int radius, ushort voxelType)
    {
        var sqrRadius = radius * radius;

        for(int z = center.z - radius; z < center.z + radius; ++z)
        {
            for(int y = center.y - radius; y < center.y + radius; ++y)
            {
                for(int x = center.x - radius; x < center.x + radius; ++x)
                {
                    var dx = (x - center.x) * (x - center.x);
                    var dy = (y - center.y) * (y - center.y);
                    var dz = (z - center.z) * (z - center.z);

                    if(dx + dy + dz < sqrRadius)
                    {
                        SetVoxelAndUpdateLightMap(x, y, z, voxelType);
                    }
                }
            }
        }
    }

    public List<Chunk> GetTopMostChunksAndClear()
    {
        var chunks = new List<Chunk>(_topMostChunks.Values);
        _topMostChunks.Clear();
        return chunks;
    }

    public void AddLight(Vector3Int globalLightPos, Color32 lightColor)
    {
        _updateScheduler.AddBlockLightUpdateJob(
            VoxelPosHelper.GlobalVoxelPosToChunkPos(globalLightPos),
            globalLightPos,
            lightColor,
            true,
            false
        );
    }

    public void QueueLightFillOnNewChunk(Vector3Int chunkPos)
    {
        _updateScheduler.AddChunkLightFillUpdateJob(chunkPos);
    }

    public void RemoveLight(Vector3Int globalLightPos, bool sunlight)
    {
        _updateScheduler.AddBlockLightUpdateJob(
            VoxelPosHelper.GlobalVoxelPosToChunkPos(globalLightPos),
            globalLightPos,
            new Color32(0, 0, 0, 0),
            false,
            sunlight
        );     
    }

    public Color32 GetVoxelLightColor(Vector3Int pos)
    {
        var chunk = GetChunkFromVoxelPosition(pos.x, pos.y, pos.z, false);
        if(chunk == null)
        {
            return new Color32(0, 0, 0, 0);
        }
        var chunkLocalPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(pos);
        return new Color32
        (
            (byte)(chunk.GetLightChannelValue(chunkLocalPos, 0) << 4),
            (byte)(chunk.GetLightChannelValue(chunkLocalPos, 1) << 4),
            (byte)(chunk.GetLightChannelValue(chunkLocalPos, 2) << 4),
            (byte)(chunk.GetLightChannelValue(chunkLocalPos, 3) << 4)
        );
    }

    public void SetVoxelAuxiliaryData(Vector3Int pos, ushort auxData)
    {
        var chunk = GetChunkFromVoxelPosition(pos.x, pos.y, pos.z, true);
        var chunkLocalPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(pos);
        chunk.SetAuxiliaryData(chunkLocalPos, auxData);
    }

    public void ClearVoxelAuxiliaryData(Vector3Int pos)
    {
        var chunk = GetChunkFromVoxelPosition(pos.x, pos.y, pos.z, true);
        var chunkLocalPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(pos);
        chunk.ClearAuxiliaryData(chunkLocalPos);
    }

    public ushort? GetVoxelAuxiliaryData(Vector3Int pos)
    {
        return GetVoxelAuxiliaryData(pos.x, pos.y, pos.z);
    }

    public ushort? GetVoxelAuxiliaryData(int x, int y, int z)
    {
        var chunk = GetChunkFromVoxelPosition(x, y, z, false);
        if(chunk == null)
        {
            return null;
        }
        var chunkLocalPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(new Vector3Int(x, y, z));
        return chunk.GetAuxiliaryData(chunkLocalPos);
    }

    public ushort GetVoxel(int x, int y, int z)
    {
        var chunk = GetChunkFromVoxelPosition(x, y, z, false);
        if(chunk == null)
        {
            return 0;
        }
        var chunkLocalPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(new Vector3Int(x, y, z));
        return chunk.GetVoxel(chunkLocalPos);
    }

    public ushort GetVoxel(Vector3Int voxelPos)
    {
        return GetVoxel(voxelPos.x, voxelPos.y, voxelPos.z);
    }

    public (Vector3Int, Vector3Int) GetWorldBoundaries()
    {
        Vector3Int minBound = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
        Vector3Int maxBound = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

        foreach(var chunkPos in _chunks.Keys)
        {
            minBound.x = Mathf.Min(minBound.x, chunkPos.x);
            minBound.y = Mathf.Min(minBound.y, chunkPos.y);
            minBound.z = Mathf.Min(minBound.z, chunkPos.z);
            maxBound.x = Mathf.Max(maxBound.x, chunkPos.x);
            maxBound.y = Mathf.Max(maxBound.y, chunkPos.y);
            maxBound.z = Mathf.Max(maxBound.z, chunkPos.z);
        }

        minBound = VoxelPosHelper.ChunkPosToGlobalChunkBaseVoxelPos(minBound);
        maxBound = VoxelPosHelper.ChunkPosToGlobalChunkBaseVoxelPos(maxBound) 
                    + new Vector3Int(VoxelInfo.ChunkSize, VoxelInfo.ChunkSize, VoxelInfo.ChunkSize);

        return (minBound, maxBound);
    }

    public Vector3Int GetRandomSolidSurfaceVoxel()
    {
        var bounds = GetWorldBoundaries();

        while(true)
        {
            var x = UnityEngine.Random.Range(bounds.Item1.x, bounds.Item2.x);
            var z = UnityEngine.Random.Range(bounds.Item1.z, bounds.Item2.z);
            var y = GetHighestVoxelPos(x, z);
            if(y.HasValue)
            {
                return new Vector3Int(x, y.Value, z);
            }
        }
    }

    public void QueueVoxelForRebuild(Vector3Int globalVoxelPos)
    {
        //TODO: JUST CONVERT THE VOXELPOS TO CHUNK POS DUMBASS!!
        var chunkPos = GetChunkFromVoxelPosition(globalVoxelPos, true).ChunkPos;
        _updateScheduler.AddChunkRebuildJob(chunkPos);
    }

    public void Clear()
    {
        foreach(var chunk in _chunks.Values)
        {
            chunk.Reset();
        }
        _chunks.Clear();
    }

    public int? GetHighestVoxelPos(int x, int z)
    {
        var voxelXZPos = new Vector3Int(x, 0, z);
        var chunkXZPos = VoxelPosHelper.GlobalVoxelPosToChunkPos(voxelXZPos);
        var chunkPositions = _chunks.Keys
            .Where(c => c.x == chunkXZPos.x && c.z == chunkXZPos.z)
            .OrderByDescending(c => c.y);

        var localVoxelPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(voxelXZPos);
        foreach(var chunkPos in chunkPositions)
        {
            var chunk = _chunks[chunkPos];
            for(int y = VoxelInfo.ChunkSize - 1; y >= 0; --y)
            {
                if(chunk.GetVoxel(localVoxelPos.x, y, localVoxelPos.z) != 0)
                {
                    return VoxelPosHelper.ChunkLocalVoxelPosToGlobal(
                        new Vector3Int(localVoxelPos.x, y, localVoxelPos.z),
                        chunkPos
                    ).y;
                }
            }
        }
        return null;
    }

    public void QueueChunksForLightMappingUpdate(IEnumerable<Vector3Int> chunkPositions)
    {
        foreach(var chunkPos in chunkPositions)
        {
            _updateScheduler.AddChunkLightMappingUpdateJob(chunkPos);
        }
    }

    public void QueueChunkForLightMappingUpdate(Vector3Int chunkPos)
    {
        _updateScheduler.AddChunkLightMappingUpdateJob(chunkPos);
    }

    private void QueueAffectedChunksForRebuild(Vector3Int voxelPos)
    {        
        var localPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(voxelPos);
        var chunkPos = VoxelPosHelper.GlobalVoxelPosToChunkPos(voxelPos);

        _updateScheduler.AddChunkRebuildJob(chunkPos);
        if(localPos.x == 0)                         _updateScheduler.AddChunkRebuildJob(chunkPos + Vector3Int.left);
        if(localPos.x == VoxelInfo.ChunkSize - 1)   _updateScheduler.AddChunkRebuildJob(chunkPos + Vector3Int.right);
        if(localPos.y == 0)                         _updateScheduler.AddChunkRebuildJob(chunkPos + Vector3Int.down);
        if(localPos.y == VoxelInfo.ChunkSize - 1)   _updateScheduler.AddChunkRebuildJob(chunkPos + Vector3Int.up);
        if(localPos.z == 0)                         _updateScheduler.AddChunkRebuildJob(chunkPos + Vector3Int.back);
        if(localPos.z == VoxelInfo.ChunkSize - 1)   _updateScheduler.AddChunkRebuildJob(chunkPos + Vector3Int.forward);
    }

    public Chunk GetChunkFromVoxelPosition(Vector3Int globalVoxelPos, bool create)
    {
        var chunkPos = VoxelPosHelper.GlobalVoxelPosToChunkPos(globalVoxelPos);

        if(!_chunks.ContainsKey(chunkPos))
        {
            if(create)
            {
                var chunk = new Chunk(this, chunkPos);
                _chunks.Add(chunkPos, chunk);

                var chunkXZPos = new Vector2Int(globalVoxelPos.x, globalVoxelPos.z);
                if(!_topMostChunks.ContainsKey(chunkXZPos) || globalVoxelPos.y > _topMostChunks[chunkXZPos].ChunkPos.y)
                {
                    _topMostChunks[chunkXZPos] = chunk;
                }
            }
            else
            {
                return null;
            }
        }
        return _chunks[chunkPos];
    }

    public Chunk GetChunkFromVoxelPosition(int x, int y, int z, bool create)
    {
        return GetChunkFromVoxelPosition(new Vector3Int(x, y, z), create);
    }

    private struct LightUpdate
    {
        public Vector3Int Position { get; set; }

        public Color32 Color { get; set; }

        public bool Add { get; set; }

        public bool Sunlight { get; set; }
    }

    private object _chunkCreationLock = new object();

    private Dictionary<Vector3Int, Chunk> _chunks;

    private Dictionary<Vector2Int, Chunk> _topMostChunks;

    private Dictionary<Vector3Int, ChunkBuilder> _chunkBuilders;

    public LightMap _lightMap;

    private PlayerController _player;

    private WorldUpdateScheduler _updateScheduler;
}
