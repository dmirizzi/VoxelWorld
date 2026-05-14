using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

class ChunkMeshRebuildJob : IWorldUpdateJob
{
    public int UpdateStage => 4;

    public Vector3Int ChunkPos { get; private set; }

    public HashSet<Vector3Int> AffectedChunks { get; private set; }

    public ChunkMeshRebuildJob(Vector3Int chunkPos)
    {
        ChunkPos = chunkPos;
        AffectedChunks = new HashSet<Vector3Int>
        {
            chunkPos
        };
    }

    public bool PreExecuteSync(VoxelWorld world, WorldGenerator worldGenerator)
    {
        if(!world.ChunkExists(ChunkPos)) return false;
        _chunkBuilder = world.CreateNewChunkBuilder(ChunkPos);
        return true;
    }

    public Task ExecuteAsync()
    {
        return Task.Run(() => 
        {
            UnityEngine.Profiling.Profiler.BeginThreadProfiling("WorldUpdateJobs", "ChunkMeshRebuildJob");
            _chunkBuilder.Build();
            UnityEngine.Profiling.Profiler.EndThreadProfiling();
        });
    }

    public void PostExecuteSync(VoxelWorld world, WorldGenerator worldGenerator, WorldUpdateScheduler worldUpdateScheduler)
    {
        worldUpdateScheduler.AddChunkLightFillUpdateJob(ChunkPos);
    }

    public override bool Equals(object rhs) =>
        (rhs is ChunkMeshRebuildJob rhsJob)
            && (ChunkPos == rhsJob.ChunkPos);

    public override int GetHashCode() => 
        HashCode.Combine(
            typeof(ChunkMeshRebuildJob), 
            ChunkPos);    

    public override string ToString()
     => $"ChunkMeshRebuildJob(ChunkPos={ChunkPos})";

    private ChunkMeshBuilder _chunkBuilder;
}
