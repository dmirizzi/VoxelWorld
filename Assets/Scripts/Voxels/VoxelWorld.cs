using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class VoxelWorld
{
    public VoxelWorld(Material textureAtlasMaterial, Material textureAtlasTransparentMaterial)
    {
        _chunks = new Dictionary<Vector3Int, Chunk>();
        _chunkBuilders = new Dictionary<Vector3Int, ChunkBuilder>();
        _changedChunks = new HashSet<Vector3Int>();
        _lightMapCalculator = new LightMapCalculator(this);
        _textureAtlasMaterial = textureAtlasMaterial;
        _textureAtlasTransparentMaterial = textureAtlasTransparentMaterial;
    }

    public void SetVoxelAndRebuild(Vector3Int pos, ushort type, BlockFace? placementDir = null, BlockFace? lookDir = null)
    {
        SetVoxel(pos.x, pos.y, pos.z, type, placementDir, lookDir);
        BuildChangedChunks();
    }

    public void SetVoxelAndRebuild(int x, int y, int z, ushort type, BlockFace? placementDir = null, BlockFace? lookDir = null)
    {
        SetVoxel(x, y, z, type, placementDir, lookDir);
        BuildChangedChunks();
    }

    public void SetVoxel(Vector3Int globalPos, ushort type, BlockFace? placementDir = null, BlockFace? lookDir = null)
    {
        SetVoxel(globalPos.x, globalPos.y, globalPos.z, type, placementDir, lookDir);
    }

    public void SetVoxel(int x, int y, int z, ushort type, BlockFace? placementDir = null, BlockFace? lookDir = null)
    {
        var globalPos = new Vector3Int(x, y, z);
        var chunk = GetChunkFromVoxelPosition(x, y, z, true);
        var chunkLocalPos = VoxelPosConverter.GlobalToChunkLocalVoxelPos(globalPos);

        if(!chunk.SetVoxel(chunkLocalPos, type, placementDir, lookDir))
        {
            // Voxel cant be placed
            return;
        }

        foreach(var affectedChunk in GetChunksAdjacentToVoxel(globalPos))
        {
            _changedChunks.Add(affectedChunk);
        }
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

    public void AddLight(Vector3Int pos, Color32 color, int range)
    {
        var affectedChunks = new HashSet<Chunk>[] {
            new HashSet<Chunk>(),
            new HashSet<Chunk>(),
            new HashSet<Chunk>()
        };

        var tasks = new Task[]
        {
            Task.Run(() => _lightMapCalculator.AddLight(pos, 0, color.r, range, affectedChunks[0])),
            Task.Run(() => _lightMapCalculator.AddLight(pos, 1, color.g, range, affectedChunks[1])),
            Task.Run(() => _lightMapCalculator.AddLight(pos, 2, color.b, range, affectedChunks[2]))
        };
        Task.WaitAll(tasks);

        foreach(var chunk in affectedChunks.OrderByDescending(x => x.Count).First())
        {
            if(_chunkBuilders.ContainsKey(chunk.ChunkPos))
            {
                _chunkBuilders[chunk.ChunkPos].UpdateLightVertexColors();
            }
        }
    }

    public Color32 GetLightValue(Vector3Int pos)
    {
        var chunk = GetChunkFromVoxelPosition(pos.x, pos.y, pos.z, false);
        if(chunk == null)
        {
            return new Color32(0, 0, 0, 0);
        }
        var chunkLocalPos = VoxelPosConverter.GlobalToChunkLocalVoxelPos(pos);
        return new Color32
        (
            (byte)(Mathf.Clamp(chunk.GetLightChannelValue(chunkLocalPos, 0), 0, 255)),
            (byte)(Mathf.Clamp(chunk.GetLightChannelValue(chunkLocalPos, 1), 0, 255)),
            (byte)(Mathf.Clamp(chunk.GetLightChannelValue(chunkLocalPos, 2), 0, 255)),
            255
        );
    }

    public byte? GetVoxelAuxiliaryData(Vector3Int pos)
    {
        return GetVoxelAuxiliaryData(pos.x, pos.y, pos.z);
    }

    public byte? GetVoxelAuxiliaryData(int x, int y, int z)
    {
        var chunk = GetChunkFromVoxelPosition(x, y, z, false);
        if(chunk == null)
        {
            return 0;
        }
        var chunkLocalPos = VoxelPosConverter.GlobalToChunkLocalVoxelPos(new Vector3Int(x, y, z));
        return chunk.GetAuxiliaryData(chunkLocalPos);
    }

    public ushort GetVoxel(int x, int y, int z)
    {
        var chunk = GetChunkFromVoxelPosition(x, y, z, false);
        if(chunk == null)
        {
            return 0;
        }
        var chunkLocalPos = VoxelPosConverter.GlobalToChunkLocalVoxelPos(new Vector3Int(x, y, z));
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

        minBound = VoxelPosConverter.ChunkToBaseVoxelPos(minBound);
        maxBound = VoxelPosConverter.ChunkToBaseVoxelPos(maxBound) 
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

    public void BuildChangedChunks()
    {
        var builders = new List<ChunkBuilder>();
        var builderTasks = new List<Task>();

        foreach(var chunkPos in _changedChunks)
        {
            if(!_chunks.ContainsKey(chunkPos)) continue;

            // Delete existing chunk to regenerate it
            _chunks[chunkPos].DestroyGameObject();

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
        }        
    
        _changedChunks.Clear();
    }

    public void Clear()
    {
        foreach(var chunk in _chunks.Values)
        {
            chunk.DestroyGameObject();
        }
        _chunks.Clear();
        _changedChunks.Clear();
    }

    public int? GetHighestVoxelPos(int x, int z)
    {
        var voxelXZPos = new Vector3Int(x, 0, z);
        var chunkXZPos = VoxelPosConverter.VoxelToChunkPos(voxelXZPos);
        var chunkPositions = _chunks.Keys
            .Where(c => c.x == chunkXZPos.x && c.z == chunkXZPos.z)
            .OrderByDescending(c => c.y);

        var localVoxelPos = VoxelPosConverter.GlobalToChunkLocalVoxelPos(voxelXZPos);
        foreach(var chunkPos in chunkPositions)
        {
            var chunk = _chunks[chunkPos];
            for(int y = VoxelInfo.ChunkSize - 1; y >= 0; --y)
            {
                if(chunk.GetVoxel(localVoxelPos.x, y, localVoxelPos.z) != 0)
                {
                    return VoxelPosConverter.ChunkLocalVoxelPosToGlobal(
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

    private List<Vector3Int> GetChunksAdjacentToVoxel(Vector3Int voxelPos)
    {
        var adjacentChunks = new List<Vector3Int>();

        var localPos = VoxelPosConverter.GlobalToChunkLocalVoxelPos(voxelPos);
        var chunkPos = VoxelPosConverter.VoxelToChunkPos(voxelPos);

        adjacentChunks.Add(chunkPos);

        if(localPos.x == 0) adjacentChunks.Add(chunkPos + Vector3Int.left);
        if(localPos.x == VoxelInfo.ChunkSize - 1) adjacentChunks.Add(chunkPos + Vector3Int.right);
        if(localPos.y == 0) adjacentChunks.Add(chunkPos + Vector3Int.down);
        if(localPos.y == VoxelInfo.ChunkSize - 1) adjacentChunks.Add(chunkPos + Vector3Int.up);
        if(localPos.z == 0) adjacentChunks.Add(chunkPos + Vector3Int.back);
        if(localPos.z == VoxelInfo.ChunkSize - 1) adjacentChunks.Add(chunkPos + Vector3Int.forward);

        return adjacentChunks;
    }

    public Chunk GetChunkFromVoxelPosition(int x, int y, int z, bool create)
    {

        var voxelPos = new Vector3Int(x, y, z);
        var chunkPos = VoxelPosConverter.VoxelToChunkPos(voxelPos);

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

    private Dictionary<Vector3Int, Chunk> _chunks;

    private Dictionary<Vector3Int, ChunkBuilder> _chunkBuilders;

    private LightMapCalculator _lightMapCalculator;

    private HashSet<Vector3Int> _changedChunks;

    private Material _textureAtlasMaterial;

    private Material _textureAtlasTransparentMaterial;
}
