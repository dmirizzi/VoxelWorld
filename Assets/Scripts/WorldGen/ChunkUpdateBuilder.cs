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
        bool voxelInsideChunk = localVoxelPos.x >= 0 && localVoxelPos.x < VoxelInfo.ChunkSize &&
                                localVoxelPos.y >= 0 && localVoxelPos.y < VoxelInfo.ChunkSize &&
                                localVoxelPos.z >= 0 && localVoxelPos.z < VoxelInfo.ChunkSize;

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

    private Vector3 _playerPos;
}