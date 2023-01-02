using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

interface IWorldUpdateJob
{
    public int UpdateStage { get; }

    public Vector3Int ChunkPos { get; }

    public HashSet<Vector3Int> AffectedChunks { get; }

    public bool PreExecuteSync(VoxelWorld world);

    public Task ExecuteAsync();

    public void PostExecuteSync(VoxelWorld world);
}