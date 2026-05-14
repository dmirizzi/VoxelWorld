using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using static LightMap;

class SunlightUpdateJob : IWorldUpdateJob
{
    public int UpdateStage => 3;

    public Vector3Int ChunkPos { get; private set; }

    public HashSet<Vector3Int> AffectedChunks { get; } = new HashSet<Vector3Int>();

    public bool PreExecuteSync(VoxelWorld world, WorldGenerator worldGenerator)
    {
        // Create a layer of empty chunks above the top chunks to propagate sunlight from. Otherwise voxels on the
        // top border of top chunks won't be lit correctly, as there will be no light map above them        
        var topChunks = world.GetTopMostChunksAndClear();
        foreach(var chunk in topChunks)
        {
            var emptyChunkPos = chunk.ChunkPos + Vector3Int.up;
            var voxelPos = VoxelPosHelper.ChunkPosToGlobalChunkBaseVoxelPos(emptyChunkPos);
            world.SetVoxel(voxelPos, 0);
        }

        _topMostChunks = world.GetTopMostChunksAndClear();
        return _topMostChunks.Any();
    }

    public Task ExecuteAsync()
    {
        return Task.CompletedTask;
    }

    public void PostExecuteSync(VoxelWorld world, WorldGenerator worldGenerator, WorldUpdateScheduler worldUpdateScheduler)
    {
        var sharedSpillover = new List<LightNode>();
        foreach(var chunk in _topMostChunks)
        {
            worldUpdateScheduler.AddSunlightColumnJob(chunk, sharedSpillover);
        }
        worldUpdateScheduler.AddSunlightHorizontalSpillJob(sharedSpillover);
    }

    public override string ToString() => $"SunlightUpdateJob()";

    private List<Chunk> _topMostChunks;
}
