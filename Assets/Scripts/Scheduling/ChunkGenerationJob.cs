using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

class ChunkGenerationJob : IWorldUpdateJob
{
    public int UpdateStage => 0;

    public Vector3Int ChunkPos { get; private set;}

    public HashSet<Vector3Int> AffectedChunks { get; private set; }

    public ChunkGenerationJob(Vector3Int chunkPos)
    {
        ChunkPos = chunkPos;
        AffectedChunks = new HashSet<Vector3Int>
        {
            chunkPos
        };
    }

    public bool PreExecuteSync(VoxelWorld world, WorldGenerator worldGenerator)
    {
        if(world.TryGetChunk(ChunkPos, out var chunk))
        {
            chunk.ResetLightMap();
        }

        _chunkGenerator = worldGenerator.ChunkGenerator;
        return true;
    }

    public Task ExecuteAsync()
    {
        return Task.Run(() => _chunkUpdate = _chunkGenerator.GenerateChunk(ChunkPos));
    }

    public void PostExecuteSync(VoxelWorld world, WorldGenerator worldGenerator, WorldUpdateScheduler worldUpdateScheduler)
    {
        foreach(var chunkBacklog in _chunkUpdate.Backlog)
        {
            worldGenerator.AddBackloggedVoxels(chunkBacklog.Key, chunkBacklog.Value);
        }

        worldUpdateScheduler.AddChunkVoxelCreationJob(ChunkPos, _chunkUpdate.Voxels);
    }

    public override bool Equals(object rhs) =>
        (rhs is ChunkGenerationJob rhsJob)
            && (ChunkPos == rhsJob.ChunkPos);

    public override int GetHashCode() => 
        HashCode.Combine(
            typeof(ChunkGenerationJob), 
            ChunkPos);    

    public override string ToString()
     => $"ChunkDataGenerationJob(ChunkPos={ChunkPos})";

    private ChunkUpdate _chunkUpdate;

    private ChunkGenerator _chunkGenerator;
}