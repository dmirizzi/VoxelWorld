using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public interface IWorldUpdateJob
{
    public int UpdateStage { get; }

    public Vector3Int ChunkPos { get; }

    public HashSet<Vector3Int> AffectedChunks { get; }

    public bool PreExecuteSync(VoxelWorld world, WorldGenerator worldGenerator);

    public Task ExecuteAsync();

    public void PostExecuteSync(VoxelWorld world, WorldGenerator worldGenerator, WorldUpdateScheduler worldUpdateScheduler);
}