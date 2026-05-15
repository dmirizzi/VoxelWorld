using System.Collections.Generic;
using UnityEngine;

public struct ChunkUpdate
{
    public Vector3Int ChunkPos;
    public ushort[,,] VoxelData;
    public bool HasVoxelData;
    public Dictionary<Vector3Int, List<VoxelCreationAction>> Backlog;
}

public class ChunkUpdateBuilder
{
    public ChunkUpdateBuilder(Vector3Int chunkPos)
    {
        _chunkUpdate = new ChunkUpdate
        {
            ChunkPos = chunkPos,
            VoxelData = new ushort[VoxelInfo.ChunkSize, VoxelInfo.ChunkSize, VoxelInfo.ChunkSize],
            HasVoxelData = false,
            Backlog = new Dictionary<Vector3Int, List<VoxelCreationAction>>()
        };
    }

    // Use when the position is guaranteed to be within chunk bounds (0–15 per axis).
    public void QueueVoxelInChunk(int x, int y, int z, ushort type)
    {
        _chunkUpdate.VoxelData[x, y, z] = type;
        _chunkUpdate.HasVoxelData = true;
    }

    // Use when the position may fall outside the chunk (e.g. structures spanning chunk boundaries).
    public void QueueVoxel(Vector3Int localVoxelPos, ushort type)
    {
        // Fast check if local voxel position is inside the chunk using bitwise operations. 
        // This is equivalent to checking if any of the local voxel position's components are greater than or equal 
        // to the chunk size, but without branching.
        const int mask = ~(VoxelInfo.ChunkSize - 1);
        bool voxelInsideChunk = ((localVoxelPos.x | localVoxelPos.y | localVoxelPos.z) & mask) == 0;

        if (voxelInsideChunk)
        {
            _chunkUpdate.VoxelData[localVoxelPos.x, localVoxelPos.y, localVoxelPos.z] = type;
            _chunkUpdate.HasVoxelData = true;
        }
        else
        {
            var globalPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(localVoxelPos, _chunkUpdate.ChunkPos);
            var destChunkPos = VoxelPosHelper.GlobalVoxelPosToChunkPos(globalPos);
            var destLocalPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(globalPos);

            if (!_chunkUpdate.Backlog.ContainsKey(destChunkPos))
            {
                _chunkUpdate.Backlog[destChunkPos] = new List<VoxelCreationAction>();
            }
            _chunkUpdate.Backlog[destChunkPos].Add(new VoxelCreationAction
            {
                LocalVoxelPos = destLocalPos,
                Type = type
            });
        }
    }

    public ChunkUpdate GetChunkUpdate() => _chunkUpdate;

    private ChunkUpdate _chunkUpdate;
}
