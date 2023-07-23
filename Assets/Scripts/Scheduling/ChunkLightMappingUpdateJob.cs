using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

class ChunkLightMappingUpdateJob : IWorldUpdateJob
{
    public int UpdateStage => 6;

    public Vector3Int ChunkPos { get; private set; }

    public HashSet<Vector3Int> AffectedChunks { get; private set; }

    public ChunkLightMappingUpdateJob(Vector3Int chunkPos)
    {
        ChunkPos = chunkPos;
        AffectedChunks = new HashSet<Vector3Int>();
        AffectedChunks.Add(chunkPos);
    }

    public bool PreExecuteSync(VoxelWorld world, WorldGenerator worldGenerator)
    {
        if(!world.ChunkExists(ChunkPos) || !world.ChunkBuilderExists(ChunkPos)) return false;
        _chunkBuilder = world.GetChunkBuilder(ChunkPos);
        return true;
    }

    public Task ExecuteAsync()
    {
        return Task.Run(() => 
        {
            //var token = Profiler.StartProfiling($"{GetType()}-Async");
            _lightColorMapping = _chunkBuilder.CreateChunkLightColorMapping();
            //Profiler.StopProfiling(token);
        });
    }

    public void PostExecuteSync(VoxelWorld world, WorldGenerator worldGenerator, WorldUpdateScheduler worldUpdateScheduler)
    {
        var chunk = world.GetChunk(ChunkPos);

        // Finalize chunk game object to apply light mapping
        var chunkGameObjects = _chunkBuilder.CreateMeshGameObjects();
        chunk.Reset();                 
        chunk.AddChunkMeshGameObjects(chunkGameObjects);
        chunk.BuildBlockGameObjects();
        chunk.BuildVoxelColliderGameObjects();

        _chunkBuilder.UpdateLightVertexColors(_lightColorMapping);
    }

    public override bool Equals(object rhs) =>
        (rhs is ChunkLightMappingUpdateJob rhsJob)
            && (ChunkPos == rhsJob.ChunkPos);

    public override int GetHashCode() => 
        HashCode.Combine(
            typeof(ChunkLightMappingUpdateJob), 
            ChunkPos);    

    public override string ToString()
     => $"ChunkLightMappingUpdateJob(ChunkPos={ChunkPos})";

    private ChunkBuilder.ChunkLightColorMapping _lightColorMapping;

    private ChunkBuilder _chunkBuilder;
}