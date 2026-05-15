using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

class ChunkVoxelCreationJob : IWorldUpdateJob
{
    public int UpdateStage => 1;

    public Vector3Int ChunkPos { get; private set; }

    public HashSet<Vector3Int> AffectedChunks { get; private set; }

    public ChunkVoxelCreationJob(Vector3Int chunkPos, ushort[,,] voxelData, bool hasVoxelData)
    {
        ChunkPos = chunkPos;
        AffectedChunks = new HashSet<Vector3Int>
        {
            chunkPos
        };
        _voxelData = voxelData;
        _hasVoxelData = hasVoxelData;
    }

    public bool PreExecuteSync(VoxelWorld world, WorldGenerator worldGenerator) => true;

    public Task ExecuteAsync() => Task.CompletedTask;

    public void PostExecuteSync(VoxelWorld world, WorldGenerator worldGenerator, WorldUpdateScheduler worldUpdateScheduler)
    {
        var backloggedVoxels = worldGenerator.PopBackloggedChunk(ChunkPos);

        if (_hasVoxelData || backloggedVoxels != null)
        {
            var chunk = world.GetOrCreateChunk(ChunkPos);

            if (_hasVoxelData)
            {
                chunk.PopulateFromBuffer(_voxelData);
            }

            if (backloggedVoxels != null)
            {
                foreach (var voxel in backloggedVoxels)
                {
                    chunk.SetVoxel(voxel.LocalVoxelPos, voxel.Type);
                }
            }

            worldUpdateScheduler.AddChunkMeshRebuildJob(ChunkPos);
        }
    }

    public override bool Equals(object rhs) => (rhs is ChunkVoxelCreationJob rhsJob) && (rhsJob.ChunkPos == ChunkPos);

    public override int GetHashCode() => HashCode.Combine(typeof(ChunkGenerationJob), ChunkPos);

    public override string ToString() => $"ChunkVoxelCreationJob(ChunkPos={ChunkPos})";

    private readonly ushort[,,] _voxelData;
    private readonly bool _hasVoxelData;
}
