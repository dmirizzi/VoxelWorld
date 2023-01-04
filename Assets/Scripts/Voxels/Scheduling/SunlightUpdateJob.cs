using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

class SunlightUpdateJob : IWorldUpdateJob
{
    public int UpdateStage => 2;

    public Vector3Int ChunkPos { get; private set; }

    public HashSet<Vector3Int> AffectedChunks { get; } = new HashSet<Vector3Int>();

    public bool PreExecuteSync(VoxelWorld world)
    {
        _topMostChunks = world.GetTopMostChunksAndClear();
        _lightMap = world.GetLightMap();
        return _topMostChunks.Any();
    }

    public Task ExecuteAsync()
    {
        return Task.Run(() => _lightMap.UpdateSunlight(_topMostChunks, _affectedChunks));
    }

    public void PostExecuteSync(VoxelWorld world)
    {
        world.QueueChunksForLightMappingUpdate(_affectedChunks);
    }

    public override string ToString() => $"SunlightUpdateJob()";

    private IEnumerable<Chunk> _topMostChunks;

    private HashSet<Vector3Int> _affectedChunks = new HashSet<Vector3Int>();

    private LightMap _lightMap;
}