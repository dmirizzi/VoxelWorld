using System.Collections.Generic;
using UnityEngine;

public struct ChunkUpdate
{
    public Vector3Int ChunkPos;

    public List<VoxelCreationAction> Voxels;

    public Dictionary<Vector3Int, List<VoxelCreationAction>> Backlog;
}

public class ChunkUpdateBuilder
{
    public ChunkUpdateBuilder(Vector3Int chunkPos)
    {
        _chunkUpdate = new ChunkUpdate
        {
            ChunkPos = chunkPos,
            Voxels = new List<VoxelCreationAction>(VoxelInfo.NumVoxelsPerChunk),
            Backlog = new Dictionary<Vector3Int, List<VoxelCreationAction>>()
        };
    }

    public void QueueVoxel(Vector3Int localVoxelPos, ushort type)
    {
        // Fast check if local voxel position is inside the chunk using bitwise operations. 
        // This is equivalent to checking if any of the local voxel position's components are greater than or equal 
        // to the chunk size, but without branching.
        const int mask = ~(VoxelInfo.ChunkSize - 1);
        bool voxelInsideChunk = ((localVoxelPos.x | localVoxelPos.y | localVoxelPos.z) & mask) == 0;

        if(voxelInsideChunk)
        {
            _chunkUpdate.Voxels.Add(new VoxelCreationAction{
                LocalVoxelPos = localVoxelPos,
                Type = type
            });
        }

        // Generated voxel is outside player chunk radius, put into backlog (e.g. a tree being generated across chunk boundaries)
        else
        {
            if(!_chunkUpdate.Backlog.ContainsKey(_chunkUpdate.ChunkPos))
            {
                _chunkUpdate.Backlog[_chunkUpdate.ChunkPos] = new List<VoxelCreationAction>();
            }
            _chunkUpdate.Backlog[_chunkUpdate.ChunkPos].Add(new VoxelCreationAction{
                LocalVoxelPos = localVoxelPos,
                Type = type
            });
        }
    }

    public ChunkUpdate GetChunkUpdate() => _chunkUpdate;

    private ChunkUpdate _chunkUpdate;
}