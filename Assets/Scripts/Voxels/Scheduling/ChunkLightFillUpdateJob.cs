using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ChunkLightFillUpdateJob : IWorldUpdateJob
{
    public int UpdateStage => 1;

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

    public bool PreExecuteSync(VoxelWorld world)
    {
        _lightMap = world.GetLightMap();
        return _lightMap != null;
    }

    public Task ExecuteAsync()
    {
        return Task.Run(() => _lightMap.PropagateSurroundingLightsOnNewChunk(ChunkPos));
    }

    public void PostExecuteSync(VoxelWorld world)
    {
        world.QueueChunkForLightMappingUpdate(ChunkPos);
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