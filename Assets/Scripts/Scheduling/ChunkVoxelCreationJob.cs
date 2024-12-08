using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

class ChunkVoxelCreationJob : IWorldUpdateJob
{
    public int UpdateStage => 1;

    public Vector3Int ChunkPos { get; private set; }

    public HashSet<Vector3Int> AffectedChunks { get; private set; }

    public ChunkVoxelCreationJob(Vector3Int chunkPos, List<VoxelCreationAction> voxels)
    {
        ChunkPos = chunkPos;
        AffectedChunks = new HashSet<Vector3Int>
        {
            chunkPos
        };
        _voxels = voxels;
    }

    public bool PreExecuteSync(VoxelWorld world, WorldGenerator worldGenerator) => true;

    public Task ExecuteAsync() => Task.CompletedTask;

    public void PostExecuteSync(VoxelWorld world, WorldGenerator worldGenerator, WorldUpdateScheduler worldUpdateScheduler)
    {
        var backloggedVoxels = worldGenerator.PopBackloggedChunk(ChunkPos);

        if(_voxels.Count > 0 || backloggedVoxels != null)
        {
            var chunk = world.GetOrCreateChunk(ChunkPos);

            foreach(var voxel in _voxels)
            {
                chunk.SetVoxel(voxel.LocalVoxelPos, voxel.Type);
            }

            if(backloggedVoxels != null)
            {
                foreach(var voxel in backloggedVoxels)
                {
                    chunk.SetVoxel(voxel.LocalVoxelPos, voxel.Type);
                }
            }

            world.QueueAffectedChunkForRebuild(ChunkPos);
        }
    }

    public override bool Equals(object rhs) => (rhs is ChunkVoxelCreationJob rhsJob) && (rhsJob.ChunkPos == ChunkPos);

    public override int GetHashCode() => HashCode.Combine(typeof(ChunkGenerationJob), ChunkPos);    

    public override string ToString()
     => $"ChunkVoxelCreationJob(ChunkPos={ChunkPos})";


    private List<VoxelCreationAction> _voxels;
}