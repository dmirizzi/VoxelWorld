using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

class SunlightUpdateJob : IWorldUpdateJob
{
    public int UpdateStage => 5;

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
        return Task.Run(() => _lightMap.UpdateSunlight(_topMostChunks, _affectedChunks));
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