using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

class ChunkLightFillUpdateJob : IWorldUpdateJob
{
    public int UpdateStage => 6;

    public Vector3Int ChunkPos { get; private set; }

    public HashSet<Vector3Int> AffectedChunks { get; private set; }

    public ChunkLightFillUpdateJob(Vector3Int chunkPos)
    {
        ChunkPos = chunkPos;
        AffectedChunks = new HashSet<Vector3Int>();
        for(int z = -1; z <= 1; ++z)
        {
            for(int y = -1; y <= 1; ++y)
            {
                for(int x = -1; x <= 1; ++x)
                {
                    AffectedChunks.Add(ChunkPos + new Vector3Int(x, y, z));
                }                
            }
        }
    }

    public bool PreExecuteSync(VoxelWorld world, WorldGenerator worldGenerator)
    {
        _lightMap = world.GetLightMap();
        return _lightMap != null;
    }

    public Task ExecuteAsync()
    {
        return Task.Run(() => 
        {
            UnityEngine.Profiling.Profiler.BeginThreadProfiling("WorldUpdateJobs", "ChunkLightFillUpdateJob");
            _lightMap.PropagateSurroundingLightsOnNewChunk(ChunkPos);
            UnityEngine.Profiling.Profiler.EndThreadProfiling();
        });
    }

    public void PostExecuteSync(VoxelWorld world, WorldGenerator worldGenerator, WorldUpdateScheduler worldUpdateScheduler)
    {
        worldUpdateScheduler.AddChunkLightMappingUpdateJob(ChunkPos);

        // Re-run light mapping for face-adjacent neighbors: their boundary vertices sample
        // light from this chunk via TryGetNeighboringChunkVoxel, but those samples were 0
        // when this chunk didn't exist yet (during initial world gen), leaving stale dark
        // vertices at the boundary.
        worldUpdateScheduler.AddChunkLightMappingUpdateJob(ChunkPos + Vector3Int.left);
        worldUpdateScheduler.AddChunkLightMappingUpdateJob(ChunkPos + Vector3Int.right);
        worldUpdateScheduler.AddChunkLightMappingUpdateJob(ChunkPos + Vector3Int.up);
        worldUpdateScheduler.AddChunkLightMappingUpdateJob(ChunkPos + Vector3Int.down);
        worldUpdateScheduler.AddChunkLightMappingUpdateJob(ChunkPos + Vector3Int.forward);
        worldUpdateScheduler.AddChunkLightMappingUpdateJob(ChunkPos + Vector3Int.back);
    }

    public override bool Equals(object rhs) =>
        (rhs is ChunkLightFillUpdateJob rhsJob)
            && (ChunkPos == rhsJob.ChunkPos);

    public override int GetHashCode() => 
        HashCode.Combine(
            typeof(ChunkLightFillUpdateJob), 
            ChunkPos);    

    public override string ToString()
     => $"ChunkLightFillUpdateJob(ChunkPos={ChunkPos})";

    private LightMap _lightMap;
}