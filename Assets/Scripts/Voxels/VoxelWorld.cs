using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class VoxelWorld
{
    public VoxelWorld(Material textureAtlasMaterial, Material textureAtlasTransparentMaterial, PlayerController playerController)
    {
        _chunks = new Dictionary<Vector3Int, Chunk>();
        _chunkBuilders = new Dictionary<Vector3Int, ChunkBuilder>();
        _changedChunks = new HashSet<Vector3Int>();
        _lightMap = new LightMap(this);
        _textureAtlasMaterial = textureAtlasMaterial;
        _textureAtlasTransparentMaterial = textureAtlasTransparentMaterial;
        _queuedChunkLightMapUpdates = new HashSet<Vector3Int>();
        _queuedLightUpdates = new PriorityQueue<LightUpdate, float>();
        _playerController = playerController;
    }

    public void Update()
    {
        BuildChangedChunks();

        lock(_queuedChunkLightMapUpdates)
        {
            foreach(var chunkPos in _queuedChunkLightMapUpdates)
            {
                if(_chunkBuilders.ContainsKey(chunkPos))
                {
                    _chunkBuilders[chunkPos].UpdateLightVertexColors();
                }            
            }
        }        
        _queuedChunkLightMapUpdates.Clear();

        lock(_queuedLightUpdates)
        {
            if(_queuedLightUpdates.TryDequeue(out var update))
            {
                ProcessLightUpdate(update);
            }
        }
    }

    public void SetVoxel(Vector3Int globalPos, ushort type, BlockFace? placementDir = null, BlockFace? lookDir = null, bool useExistingAuxData = false)
    {
        SetVoxel(globalPos.x, globalPos.y, globalPos.z, type, placementDir, lookDir, useExistingAuxData);
    }

    public void SetVoxel(int x, int y, int z, ushort type, BlockFace? placementDir = null, BlockFace? lookDir = null, bool useExistingAuxData = false)
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

        // If non-transparent voxel was removed or replaced by a transparent voxel, update light map
        var chunksAffectedByLightUpdate = new HashSet<Vector3Int>();
        if(!VoxelInfo.IsTransparent(oldVoxelType) && VoxelInfo.IsTransparent(type))
        { 
            //TODO: Optimize for large batches of SetVoxel?
            _lightMap.UpdateOnRemovedSolidVoxel(new Vector3Int(x, y, z), chunksAffectedByLightUpdate);
        }

        foreach(var affectedChunkPos in GetChunksAdjacentToVoxel(globalPos))
        {
            // Remember affected chunks for rebuilding in batches later
            if(!_chunks.ContainsKey(affectedChunkPos))
            {
                continue;
            }
            _changedChunks.Add(affectedChunkPos);

            // Only update light on chunks that are not being rebuilt anyways
            if(chunksAffectedByLightUpdate.Contains(affectedChunkPos))
            {
                chunksAffectedByLightUpdate.Remove(affectedChunkPos);
            }
        }

        // Update lighting on affected chunks that won't be rebuilt anyway due to changed voxel
        QueueChunksForLightmapUpdate(chunksAffectedByLightUpdate);
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
            BuildChangedChunks();
        }
    }

    public void SetLight(Vector3Int pos, Color32 color, bool add)
    {
        lock(_queuedLightUpdates)
        {
            var lightWorldPos = new Vector3(pos.x, pos.y, pos.z);
            var distToPlayer = (_playerController.transform.position - lightWorldPos).sqrMagnitude;

            _queuedLightUpdates.Enqueue(
                new LightUpdate{
                    Position = pos,
                    Color = color,
                    Add = add
                },
                distToPlayer
            );    
        }
    }

    private void ProcessLightUpdate(LightUpdate lightUpdate)
    {
        var affectedChunks = new HashSet<Vector3Int>[] {
            new HashSet<Vector3Int>(),
            new HashSet<Vector3Int>(),
            new HashSet<Vector3Int>()
        };

        var colorChannels = new byte[]{
            lightUpdate.Color.r,
            lightUpdate.Color.g,
            lightUpdate.Color.b
        };

        // Calculate lightmap on a separate task for each color channel
        var taskFactory = new TaskFactory();
        var tasks = new Task[3];
        for(int channel = 0; channel < 3; ++channel)
        {            
            if(lightUpdate.Add)
            {
                tasks[channel] = taskFactory.StartNew(c => _lightMap.AddLight(lightUpdate.Position, (int)c, colorChannels[(int)c], affectedChunks[(int)c]), channel);
            }
            else
            {
                tasks[channel] = taskFactory.StartNew(c => _lightMap.RemoveLight(lightUpdate.Position, (int)c, colorChannels[(int)c], affectedChunks[(int)c]), channel);
            }
        };

        Task.WhenAll(tasks).ContinueWith( _ => 
        {
            var allAffectedChunks = affectedChunks.SelectMany(x => x).Distinct();
            QueueChunksForLightmapUpdate(allAffectedChunks);
        });            
    }

    public Color32 GetLightValue(Vector3Int pos)
    {
        var chunk = GetChunkFromVoxelPosition(pos.x, pos.y, pos.z, false);
        if(chunk == null)
        {
            return new Color32(0, 0, 0, 0);
        }
        var chunkLocalPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(pos);
        return new Color32
        (
            (byte)(Mathf.Clamp(chunk.GetLightChannelValue(chunkLocalPos, 0), 0, 255)),
            (byte)(Mathf.Clamp(chunk.GetLightChannelValue(chunkLocalPos, 1), 0, 255)),
            (byte)(Mathf.Clamp(chunk.GetLightChannelValue(chunkLocalPos, 2), 0, 255)),
            255
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

        minBound = VoxelPosHelper.ChunkToBaseVoxelPos(minBound);
        maxBound = VoxelPosHelper.ChunkToBaseVoxelPos(maxBound) 
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

    public void RebuildVoxel(Vector3Int globalVoxelPos)
    {
        var chunkPos = GetChunkFromVoxelPosition(globalVoxelPos, true).ChunkPos;
        _changedChunks.Add(chunkPos);
    }

    public void BuildChangedChunks()
    {
        var builders = new List<ChunkBuilder>();
        var builderTasks = new List<Task>();

        if(_changedChunks.Count == 0)
        {
            return;
        }

        foreach(var chunkPos in _changedChunks)
        {
            if(!_chunks.ContainsKey(chunkPos)) continue;

            // Delete existing chunk to regenerate it
            _chunks[chunkPos].Reset();

            // Queue all builder tasks
            _chunkBuilders[chunkPos] = new ChunkBuilder(this, chunkPos, _chunks[chunkPos], _textureAtlasMaterial, _textureAtlasTransparentMaterial);
            builders.Add(_chunkBuilders[chunkPos]);
            builderTasks.Add(_chunkBuilders[chunkPos].Build());           
        }

        Task.WaitAll(builderTasks.ToArray());

        // GameObjects must be generated on main thread
        foreach(var builder in builders)
        {
            _chunks[builder.ChunkPos].AddVoxelMeshGameObjects(builder.GetChunkGameObjects());
            _chunks[builder.ChunkPos].BuildBlockGameObjects();
            _chunks[builder.ChunkPos].BuildVoxelColliderGameObjects();
        }        
    
        _changedChunks.Clear();
    }

    public void Clear()
    {
        foreach(var chunk in _chunks.Values)
        {
            chunk.Reset();
        }
        _chunks.Clear();
        _changedChunks.Clear();
    }

    public int? GetHighestVoxelPos(int x, int z)
    {
        var voxelXZPos = new Vector3Int(x, 0, z);
        var chunkXZPos = VoxelPosHelper.VoxelToChunkPos(voxelXZPos);
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

    public IEnumerable<Vector3Int> GetChunkPositions()
    {
        return _chunks.Keys;
    }

    private void QueueChunksForLightmapUpdate(IEnumerable<Vector3Int> chunkPositions)
    {
        lock(_queuedChunkLightMapUpdates)
        {
            foreach(var chunkPos in chunkPositions)
            {
                _queuedChunkLightMapUpdates.Add(chunkPos);
            }
        }
    }

    private List<Vector3Int> GetChunksAdjacentToVoxel(Vector3Int voxelPos)
    {
        var adjacentChunks = new List<Vector3Int>();

        var localPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(voxelPos);
        var chunkPos = VoxelPosHelper.VoxelToChunkPos(voxelPos);

        adjacentChunks.Add(chunkPos);

        if(localPos.x == 0) adjacentChunks.Add(chunkPos + Vector3Int.left);
        if(localPos.x == VoxelInfo.ChunkSize - 1) adjacentChunks.Add(chunkPos + Vector3Int.right);
        if(localPos.y == 0) adjacentChunks.Add(chunkPos + Vector3Int.down);
        if(localPos.y == VoxelInfo.ChunkSize - 1) adjacentChunks.Add(chunkPos + Vector3Int.up);
        if(localPos.z == 0) adjacentChunks.Add(chunkPos + Vector3Int.back);
        if(localPos.z == VoxelInfo.ChunkSize - 1) adjacentChunks.Add(chunkPos + Vector3Int.forward);

        return adjacentChunks;
    }

    public Chunk GetChunkFromVoxelPosition(Vector3Int globalPos, bool create)
    {
        //TODO: Switch implementation with other overloaded method
        return GetChunkFromVoxelPosition(globalPos.x, globalPos.y, globalPos.z, create);
    }

    public Chunk GetChunkFromVoxelPosition(int x, int y, int z, bool create)
    {
        var voxelPos = new Vector3Int(x, y, z);
        var chunkPos = VoxelPosHelper.VoxelToChunkPos(voxelPos);

        lock(_chunkCreationLock)
        {
            if(!_chunks.ContainsKey(chunkPos))
            {
                if(create)
                {
                    _chunks.Add(chunkPos, new Chunk(this, chunkPos));
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
    }

    private object _chunkCreationLock = new object();

    private Dictionary<Vector3Int, Chunk> _chunks;

    private Dictionary<Vector3Int, ChunkBuilder> _chunkBuilders;

    public LightMap _lightMap;

    private PriorityQueue<LightUpdate, float> _queuedLightUpdates;

    private HashSet<Vector3Int> _queuedChunkLightMapUpdates;

    private HashSet<Vector3Int> _changedChunks;

    private Material _textureAtlasMaterial;

    private Material _textureAtlasTransparentMaterial;

    private PlayerController _playerController;
}
