using System.Collections.Generic;
using UnityEngine;

public struct ChunkUpdate
{
    public Vector3Int ChunkPos;

    public float ChunkDistanceToPlayer;

    public List<VoxelCreationAction> Voxels;

    public Dictionary<Vector3Int, List<VoxelCreationAction>> Backlog;
}

public class ChunkUpdateBuilder
{
    public ChunkUpdateBuilder(Vector3Int chunkPos, float chunkDistanceToPlayer, Vector3 playerPos, float maxChunkDistSqr)
    {
        _chunkUpdate = new ChunkUpdate
        {
            ChunkPos = chunkPos,
            ChunkDistanceToPlayer = chunkDistanceToPlayer,
            Voxels = new List<VoxelCreationAction>(VoxelInfo.NumVoxelsPerChunk),
            Backlog = new Dictionary<Vector3Int, List<VoxelCreationAction>>()
        };
        _playerPos = playerPos;
        _maxChunkDistSqr = maxChunkDistSqr;
    }

    public void QueueVoxel(Vector3Int globalVoxelPos, ushort type)
    {
        var chunkPos = VoxelPosHelper.GlobalVoxelPosToChunkPos(globalVoxelPos);
        var chunkDistToPlayer = VoxelPosHelper.GetChunkSqrDistanceToWorldPos(_playerPos, _chunkUpdate.ChunkPos);

        if(chunkDistToPlayer <= _maxChunkDistSqr)
        {
            _chunkUpdate.Voxels.Add(new VoxelCreationAction{
                GlobalVoxelPos = globalVoxelPos,
                Type = type
            });
        }

        // Generated voxel is outside player chunk radius, put into backlog (e.g. a tree being generated across chunk boundaries)
        else
        {
            if(!_chunkUpdate.Backlog.ContainsKey(chunkPos))
            {
                _chunkUpdate.Backlog[chunkPos] = new List<VoxelCreationAction>();
            }
            _chunkUpdate.Backlog[chunkPos].Add(new VoxelCreationAction{
                GlobalVoxelPos = globalVoxelPos,
                Type = type
            });
        }
    }

    public ChunkUpdate GetChunkUpdate() => _chunkUpdate;

    private ChunkUpdate _chunkUpdate;

    private Vector3 _playerPos;

    private float _maxChunkDistSqr;
}