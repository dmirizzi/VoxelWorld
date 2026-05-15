using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

class BackloggedVoxelCreationJob : IWorldUpdateJob
{
    public int UpdateStage => 2;

    public Vector3Int ChunkPos => Vector3Int.zero;

    public HashSet<Vector3Int> AffectedChunks { get; } = new HashSet<Vector3Int>();

    public bool PreExecuteSync(VoxelWorld world, WorldGenerator worldGenerator) => true;

    public Task ExecuteAsync() => Task.CompletedTask;

    public void PostExecuteSync(VoxelWorld world, WorldGenerator worldGenerator, WorldUpdateScheduler worldUpdateScheduler)
    {
        foreach(var chunk in worldGenerator.PopAllBackloggedChunksWithinGenerationRadius())
        {
            foreach(var voxel in chunk.Voxels)
            {
                var globalVoxelPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(voxel.LocalVoxelPos, chunk.ChunkPos);
                world.SetVoxel(globalVoxelPos, voxel.Type);
            }
        }
    }
}