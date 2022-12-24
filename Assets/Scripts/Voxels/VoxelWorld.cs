using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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
        _queuedChunkRebuilds = new PriorityQueue<Vector3Int, float>();
        _lightMap = new LightMap(this);
        _queuedChunkLightMappingUpdates = new HashSet<Vector3Int>();
        _queuedLightUpdates = new PriorityQueue<LightUpdate, float>();
    }

    void Awake()
    {
        _player = FindObjectOfType<PlayerController>();
    }

    void Update()
    {
        var sw = new Stopwatch();
        sw.Start();

        RebuildChangedChunks();
        HandleChunkBuildJobs();

        sw.Stop();
        //UnityEngine.Debug.Log($"Rebuilt changed chunks in {sw.ElapsedMilliseconds}ms");

        sw.Restart();

        lock(_queuedLightUpdates)
        {
            if(_queuedLightUpdates.TryDequeue(out var update))
            {
                ProcessLightUpdate(update);
            }
        }

        sw.Stop();
        //UnityEngine.Debug.Log($"Processed lightmap updates in {sw.ElapsedMilliseconds}ms");

        sw.Restart();

        lock(_queuedChunkLightMappingUpdates)
        {
            foreach(var chunkPos in _queuedChunkLightMappingUpdates)
            {
                if(_chunkBuilders.ContainsKey(chunkPos) && _chunkBuilders[chunkPos].MeshFinishedBuilding)
                {
                    _chunkBuilders[chunkPos].UpdateLightVertexColors();
                }            
            }
        }        
        _queuedChunkLightMappingUpdates.Clear();        


        sw.Stop();
        //UnityEngine.Debug.Log($"Updated chunk lighting in {sw.ElapsedMilliseconds}ms");
    }

    public void SetVoxel(
        Vector3Int globalPos, 
        ushort type, 
        BlockFace? placementDir = null, 
        BlockFace? lookDir = null, 
        bool useExistingAuxData = false)
    {
        SetVoxel(globalPos.x, globalPos.y, globalPos.z, type, placementDir, lookDir, useExistingAuxData);
    }

    public void SetVoxel(
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
        if(!chunk.SetVoxel(chunkLocalPos, type, placementDir, lookDir, useExistingAuxData))
        {
            // Voxel cant be placed
            return;
        }

        foreach(var affectedChunkPos in GetChunksAdjacentToVoxel(globalPos))
        {
            // Remember affected chunks for rebuilding in batches later
            if(!_chunks.ContainsKey(affectedChunkPos))
            {
                continue;
            }
            _queuedChunkRebuilds.EnqueueUnique(affectedChunkPos, GetChunkDistToPlayer(affectedChunkPos));
        }
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

        // Update lighting on affected chunks that won't be rebuilt anyway due to changed voxel
        QueueChunksForLightMappingUpdate(chunksAffectedByLightUpdate);
    }

    public void SetVoxelSphere(Vector3Int center, int radius, ushort voxelType, bool rebuild)
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
                        SetVoxel(x, y, z, voxelType);
                    }
                }
            }
        }

        if(rebuild)
        {
            RebuildChangedChunks();
        }
    }

    public void InitializeSunlight()
    {
        var affectedChunks = new HashSet<Vector3Int>();
        _lightMap.InitializeSunlight(_topMostChunks.Values, affectedChunks);
        QueueChunksForLightMappingUpdate(affectedChunks);
    }

    public void AddLight(Vector3Int pos, Color32 color)
    {
        lock(_queuedLightUpdates)
        {
            var lightWorldPos = new Vector3(pos.x, pos.y, pos.z);
            var distToPlayer = (_player.transform.position - lightWorldPos).sqrMagnitude;          

            _queuedLightUpdates.EnqueueUnique(
                new LightUpdate{
                    Position = pos,
                    Color = color,
                    Add = true
                },
                distToPlayer
            );    
        }
    }

    public void RemoveLight(Vector3Int pos, bool sunlight)
    {
        lock(_queuedLightUpdates)
        {
            var lightWorldPos = new Vector3(pos.x, pos.y, pos.z);
            var distToPlayer = (_player.transform.position - lightWorldPos).sqrMagnitude;          

            _queuedLightUpdates.EnqueueUnique(
                new LightUpdate{
                    Position = pos,
                    Color = new Color32(0, 0, 0, 255),
                    Add = false,
                    Sunlight = sunlight
                },
                distToPlayer
            );    
        }        
    }

    private void ProcessLightUpdate(LightUpdate lightUpdate)
    {
        var affectedChunks = new HashSet<Vector3Int>();

        if(lightUpdate.Sunlight)
        {
            if(!lightUpdate.Add)
            {
                _lightMap.RemoveLight(lightUpdate.Position, Chunk.SunlightChannel, affectedChunks);                
            }
        }
        else
        {
            var colorChannels = new byte[]{
                (byte)(lightUpdate.Color.r >> 4),
                (byte)(lightUpdate.Color.g >> 4),
                (byte)(lightUpdate.Color.b >> 4)
            };

            for(int channel = 0; channel < 3; ++channel)
            {            
                if(lightUpdate.Add)
                {
                    _lightMap.AddLight(lightUpdate.Position, channel, colorChannels[channel], affectedChunks);
                }
                else
                {
                    _lightMap.RemoveLight(lightUpdate.Position, channel, affectedChunks);                
                }
            }
        }

        QueueChunksForLightMappingUpdate(affectedChunks);
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
            var x = Random.Range(bounds.Item1.x, bounds.Item2.x);
            var z = Random.Range(bounds.Item1.z, bounds.Item2.z);
            var y = GetHighestVoxelPos(x, z);
            if(y.HasValue)
            {
                return new Vector3Int(x, y.Value, z);
            }
        }
    }

    public void QueueVoxelForRebuild(Vector3Int globalVoxelPos)
    {
        var chunkPos = GetChunkFromVoxelPosition(globalVoxelPos, true).ChunkPos;
        _queuedChunkRebuilds.EnqueueUnique(chunkPos, GetChunkDistToPlayer(chunkPos));
    }

    public void RebuildChangedChunks()
    {
        if(_queuedChunkRebuilds.Count == 0)
        {
            return;
        }

        var builderTasks = new List<Task>();     
     
        while(_chunkBuildJobs.Count < MaxNumChunkBuilderTasks && _queuedChunkRebuilds.TryDequeue(out var chunkPos))
        {
            if(!_chunks.ContainsKey(chunkPos)) continue;

            // Queue all builder tasks
            _chunkBuilders[chunkPos] = new ChunkBuilder(this, chunkPos, _chunks[chunkPos], TextureAtlasMaterial, TextureAtlasTransparentMaterial);
            _chunkBuildJobs.Add((_chunkBuilders[chunkPos], _chunkBuilders[chunkPos].Build()));
        }
    }

    private void HandleChunkBuildJobs()
    {
        var jobsToRemove = new HashSet<(ChunkBuilder, Task)>();

        foreach(var buildJob in _chunkBuildJobs)
        {
            if(buildJob.BuilderTask.IsCompleted)
            {
                // Chunk GameObjects must be generated on main thread      
                var builder = buildJob.Builder;
                _chunks[builder.ChunkPos].Reset();
                _chunks[builder.ChunkPos].AddVoxelMeshGameObjects(builder.CreateChunkGameObjects());
                _chunks[builder.ChunkPos].BuildBlockGameObjects();
                _chunks[builder.ChunkPos].BuildVoxelColliderGameObjects();

                jobsToRemove.Add(buildJob);
            }
        }

        _chunkBuildJobs.RemoveAll(x => jobsToRemove.Contains(x));
    }

    public void Clear()
    {
        foreach(var chunk in _chunks.Values)
        {
            chunk.Reset();
        }
        _chunks.Clear();
        _queuedChunkRebuilds.Clear();
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

    private void QueueChunksForLightMappingUpdate(IEnumerable<Vector3Int> chunkPositions)
    {
        lock(_queuedChunkLightMappingUpdates)
        {
            foreach(var chunkPos in chunkPositions)
            {
                // Skip chunk if its already being rebuilt anyways
                if(_queuedChunkRebuilds.Contains(chunkPos)) continue;

                _queuedChunkLightMappingUpdates.Add(chunkPos);
            }
        }
    }

    private List<Vector3Int> GetChunksAdjacentToVoxel(Vector3Int voxelPos)
    {
        var adjacentChunks = new List<Vector3Int>();

        var localPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(voxelPos);
        var chunkPos = VoxelPosHelper.GlobalVoxelPosToChunkPos(voxelPos);

        adjacentChunks.Add(chunkPos);

        if(localPos.x == 0) adjacentChunks.Add(chunkPos + Vector3Int.left);
        if(localPos.x == VoxelInfo.ChunkSize - 1) adjacentChunks.Add(chunkPos + Vector3Int.right);
        if(localPos.y == 0) adjacentChunks.Add(chunkPos + Vector3Int.down);
        if(localPos.y == VoxelInfo.ChunkSize - 1) adjacentChunks.Add(chunkPos + Vector3Int.up);
        if(localPos.z == 0) adjacentChunks.Add(chunkPos + Vector3Int.back);
        if(localPos.z == VoxelInfo.ChunkSize - 1) adjacentChunks.Add(chunkPos + Vector3Int.forward);

        return adjacentChunks;
    }

    private float GetChunkDistToPlayer(Vector3Int chunkPos)
    {
        var chunkVoxelPos = VoxelPosHelper.ChunkPosToGlobalChunkBaseVoxelPos(chunkPos);
        var playerVoxelPos = VoxelPosHelper.WorldPosToGlobalVoxelPos(_player.transform.position);
        return (chunkVoxelPos - playerVoxelPos).magnitude;
    }

    public Chunk GetChunkFromVoxelPosition(Vector3Int globalPos, bool create)
    {
        //TODO: Switch implementation with other overloaded method
        return GetChunkFromVoxelPosition(globalPos.x, globalPos.y, globalPos.z, create);
    }

    public Chunk GetChunkFromVoxelPosition(int x, int y, int z, bool create)
    {
        var voxelPos = new Vector3Int(x, y, z);
        var chunkPos = VoxelPosHelper.GlobalVoxelPosToChunkPos(voxelPos);

        lock(_chunkCreationLock)
        {
            if(!_chunks.ContainsKey(chunkPos))
            {
                if(create)
                {
                    var chunk = new Chunk(this, chunkPos);
                    _chunks.Add(chunkPos, chunk);

                    var chunkXZPos = new Vector2Int(x, z);
                    if(!_topMostChunks.ContainsKey(chunkXZPos) || y > _topMostChunks[chunkXZPos].ChunkPos.y)
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

    private PriorityQueue<LightUpdate, float> _queuedLightUpdates;

    private HashSet<Vector3Int> _queuedChunkLightMappingUpdates;

    private PriorityQueue<Vector3Int, float> _queuedChunkRebuilds;

    private List<(ChunkBuilder Builder, Task BuilderTask)> _chunkBuildJobs = new List<(ChunkBuilder Builder, Task BuilderTask)>();

    private PlayerController _player;
}
