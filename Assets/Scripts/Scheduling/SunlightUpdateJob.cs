using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

class SunlightUpdateJob : IWorldUpdateJob
{
    public int UpdateStage => 3;

    public Vector3Int ChunkPos { get; private set; }

    public HashSet<Vector3Int> AffectedChunks { get; } = new HashSet<Vector3Int>();

    public bool PreExecuteSync(VoxelWorld world, WorldGenerator worldGenerator)
    {
        _lightMap = world.GetLightMap();

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
        return Task.Run(() => 
        {
            UnityEngine.Profiling.Profiler.BeginThreadProfiling("WorldUpdateJobs", "SunlightUpdateJob");
            _lightMap.UpdateSunlight(_topMostChunks, _affectedChunks);
            UnityEngine.Profiling.Profiler.EndThreadProfiling();
        });
    }

    public void PostExecuteSync(VoxelWorld world, WorldGenerator worldGenerator, WorldUpdateScheduler worldUpdateScheduler)
    {
        world.QueueChunksForLightMappingUpdate(_affectedChunks);
    }

    public override string ToString() => $"SunlightUpdateJob()";

    private List<Chunk> _topMostChunks;

    private HashSet<Vector3Int> _affectedChunks = new HashSet<Vector3Int>();

    private LightMap _lightMap;
}