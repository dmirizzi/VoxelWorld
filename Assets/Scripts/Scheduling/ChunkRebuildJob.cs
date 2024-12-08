using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

class ChunkRebuildJob : IWorldUpdateJob
{
    public int UpdateStage => 3;

    public Vector3Int ChunkPos { get; private set; }

    public HashSet<Vector3Int> AffectedChunks { get; private set; }

    public ChunkRebuildJob(Vector3Int chunkPos)
    {
        ChunkPos = chunkPos;
        AffectedChunks = new HashSet<Vector3Int>();
        AffectedChunks.Add(chunkPos);
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
            _chunkBuilder.Build();
        });
    }

    public void PostExecuteSync(VoxelWorld world, WorldGenerator worldGenerator, WorldUpdateScheduler worldUpdateScheduler)
    {
        world.QueueLightFillOnNewChunk(ChunkPos);
    }

    public override bool Equals(object rhs) =>
        (rhs is ChunkRebuildJob rhsJob)
            && (ChunkPos == rhsJob.ChunkPos);

    public override int GetHashCode() => 
        HashCode.Combine(
            typeof(ChunkRebuildJob), 
            ChunkPos);    

    public override string ToString()
     => $"ChunkRebuildJob(ChunkPos={ChunkPos})";

    private ChunkBuilder _chunkBuilder;
}
