using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class VoxelWorld
{
    private Dictionary<Vector3Int, byte[,,]> _chunks;

    private Dictionary<Vector3Int, GameObject[]> _chunkGameObjects;

    private ChunkBuilder _chunkBuilder;

    private HashSet<Vector3Int> _changedChunks;

    public VoxelWorld(Material textureAtlasMaterial, Material textureAtlasTransparentMaterial)
    {
        _chunks = new Dictionary<Vector3Int, byte[,,]>();
        _changedChunks = new HashSet<Vector3Int>();
        _chunkBuilder = new ChunkBuilder(this, textureAtlasMaterial, textureAtlasTransparentMaterial);
        _chunkGameObjects = new Dictionary<Vector3Int, GameObject[]>();
    }

    public void SetVoxel(int x, int y, int z, VoxelType type, bool rebuild = false)
    {
        var chunk = GetChunkFromVoxelPosition(x, y, z, true);
        var chunkLocalPos = VoxelPosConverter.GlobalToChunkLocalVoxelPos(new Vector3Int(x, y, z));

        chunk[chunkLocalPos.x, chunkLocalPos.y, chunkLocalPos.z] = (byte)type;

        foreach(var affectedChunk in GetChunksAdjacentToVoxel(new Vector3Int(x, y, z)))
        {
            _changedChunks.Add(affectedChunk);
        }

        if(rebuild)
        {
            Build();
        }
    }

    public void SetVoxelSphere(Vector3Int center, int radius, VoxelType voxelType, bool rebuild)
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
            Build();
        }
    }

    public VoxelType GetVoxel(int x, int y, int z)
    {
        var chunkLocalPos = VoxelPosConverter.GlobalToChunkLocalVoxelPos(new Vector3Int(x, y, z));
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

    public void Build()
    {
        foreach(var chunkPos in _changedChunks)
        {
            if(!_chunks.ContainsKey(chunkPos)) continue;

            // Delete existing chunk to regenerate it
            if(_chunkGameObjects.ContainsKey(chunkPos))
            {
                foreach(var chunkGameObject in _chunkGameObjects[chunkPos])
                {
                    GameObject.Destroy(chunkGameObject);
                }
            }

            _chunkGameObjects[chunkPos] = _chunkBuilder.Build(chunkPos, _chunks[chunkPos]);
        }
        _changedChunks.Clear();
    }

    public void Clear()
    {
        foreach(var chunk in _chunkGameObjects.Values)
        {
            foreach(var obj in chunk)
            {
                GameObject.Destroy(obj);
            }
        }

        _chunkGameObjects.Clear();
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
            var chunkData = _chunks[chunkPos];
            for(int y = VoxelInfo.ChunkSize - 1; y >= 0; --y)
            {
                if((VoxelType)chunkData[localVoxelPos.x, y, localVoxelPos.z] != VoxelType.Empty)
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
        if(localPos.x == VoxelInfo.ChunkSize) adjacentChunks.Add(chunkPos + Vector3Int.right);
        if(localPos.y == 0) adjacentChunks.Add(chunkPos + Vector3Int.down);
        if(localPos.y == VoxelInfo.ChunkSize) adjacentChunks.Add(chunkPos + Vector3Int.up);
        if(localPos.z == 0) adjacentChunks.Add(chunkPos + Vector3Int.back);
        if(localPos.z == VoxelInfo.ChunkSize) adjacentChunks.Add(chunkPos + Vector3Int.forward);

        return adjacentChunks;
    }

    private byte[,,] GetChunkFromVoxelPosition(int x, int y, int z, bool create)
    {

        var voxelPos = new Vector3Int(x, y, z);
        var chunkPos = VoxelPosConverter.VoxelToChunkPos(voxelPos);

        if(!_chunks.ContainsKey(chunkPos))
        {
            if(create)
            {
                _chunks.Add(chunkPos, new byte[VoxelInfo.ChunkSize, VoxelInfo.ChunkSize, VoxelInfo.ChunkSize]);
            }
            else
            {
                return null;
            }

        }
        return _chunks[chunkPos];        
    }
}
