using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static LightMap;

class SunlightHorizontalSpillJob : IWorldUpdateJob
{
    public int UpdateStage => 5;
    public Vector3Int ChunkPos => Vector3Int.zero;
    public HashSet<Vector3Int> AffectedChunks { get; } = new HashSet<Vector3Int>();

    public SunlightHorizontalSpillJob(List<LightNode> sharedSpillover)
    {
        _sharedSpillover = sharedSpillover;
    }

    public bool PreExecuteSync(VoxelWorld world, WorldGenerator worldGenerator)
    {
        _lightMap = world.GetLightMap();
        _spilloverSeeds = new List<LightNode>(_sharedSpillover);
        foreach (var pos in world.GetAllChunkPositions())
            AffectedChunks.Add(pos);
        return _spilloverSeeds.Count > 0;
    }

    public Task ExecuteAsync()
    {
        return Task.Run(() =>
        {
            UnityEngine.Profiling.Profiler.BeginThreadProfiling("WorldUpdateJobs", "SunlightHorizontalSpillJob");
            _lightMap.PropagateSpilloverNodes(_spilloverSeeds, _visitedChunks);
            UnityEngine.Profiling.Profiler.EndThreadProfiling();
        });
    }

    public void PostExecuteSync(VoxelWorld world, WorldGenerator worldGenerator, WorldUpdateScheduler worldUpdateScheduler)
    {
        foreach (var pos in _visitedChunks)
            worldUpdateScheduler.AddChunkLightMappingUpdateJob(pos);
    }

    public override bool Equals(object rhs) => rhs is SunlightHorizontalSpillJob;
    public override int GetHashCode() => HashCode.Combine(typeof(SunlightHorizontalSpillJob));
    public override string ToString() => "SunlightHorizontalSpillJob()";

    private readonly List<LightNode> _sharedSpillover;
    private LightMap _lightMap;
    private List<LightNode> _spilloverSeeds;
    private readonly HashSet<Vector3Int> _visitedChunks = new HashSet<Vector3Int>();
}
