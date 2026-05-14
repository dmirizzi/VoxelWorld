using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static LightMap;

class SunlightColumnJob : IWorldUpdateJob
{
    public int UpdateStage => 4;
    public Vector3Int ChunkPos { get; }
    public HashSet<Vector3Int> AffectedChunks { get; } = new HashSet<Vector3Int>();

    public SunlightColumnJob(Chunk topChunk, List<LightNode> sharedSpillover)
    {
        _topChunk = topChunk;
        ChunkPos = topChunk.ChunkPos;
        _sharedSpillover = sharedSpillover;
    }

    public bool PreExecuteSync(VoxelWorld world, WorldGenerator worldGenerator)
    {
        _lightMap = world.GetLightMap();
        var currentPos = _topChunk.ChunkPos;
        while (world.TryGetChunk(currentPos, out _))
        {
            AffectedChunks.Add(currentPos);
            currentPos += Vector3Int.down;
        }
        return AffectedChunks.Count > 0;
    }

    public Task ExecuteAsync()
    {
        return Task.Run(() =>
        {
            UnityEngine.Profiling.Profiler.BeginThreadProfiling("WorldUpdateJobs", "SunlightColumnJob");
            _lightMap.UpdateSunlightColumnVertical(_topChunk, _localSpillover);
            UnityEngine.Profiling.Profiler.EndThreadProfiling();
        });
    }

    public void PostExecuteSync(VoxelWorld world, WorldGenerator worldGenerator, WorldUpdateScheduler worldUpdateScheduler)
    {
        _sharedSpillover.AddRange(_localSpillover);
        foreach (var pos in AffectedChunks)
            worldUpdateScheduler.AddChunkLightMappingUpdateJob(pos);
    }

    public override bool Equals(object rhs) =>
        rhs is SunlightColumnJob rhsJob && _topChunk.ChunkPos == rhsJob._topChunk.ChunkPos;

    public override int GetHashCode() =>
        HashCode.Combine(typeof(SunlightColumnJob), _topChunk.ChunkPos);

    public override string ToString() => $"SunlightColumnJob(TopChunk={_topChunk.ChunkPos})";

    private readonly Chunk _topChunk;
    private readonly List<LightNode> _sharedSpillover;
    private LightMap _lightMap;
    private readonly List<LightNode> _localSpillover = new List<LightNode>();
}
