using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

class ChunkVoxelCreationJob : IWorldUpdateJob
{
    public int UpdateStage => 1;

    public Vector3Int ChunkPos { get; private set; }

    public HashSet<Vector3Int> AffectedChunks { get; private set; }

    public ChunkVoxelCreationJob(Vector3Int chunkPos, ushort[,,] voxelData, bool hasVoxelData,
                                 Dictionary<Vector3Int, ushort> localAuxData)
    {
        ChunkPos = chunkPos;
        AffectedChunks = new HashSet<Vector3Int> { chunkPos };
        _voxelData    = voxelData;
        _hasVoxelData = hasVoxelData;
        _localAuxData = localAuxData;
    }

    public bool PreExecuteSync(VoxelWorld world, WorldGenerator worldGenerator) => true;

    public Task ExecuteAsync() => Task.CompletedTask;

    public void PostExecuteSync(VoxelWorld world, WorldGenerator worldGenerator, WorldUpdateScheduler worldUpdateScheduler)
    {
        var backloggedVoxels = worldGenerator.PopBackloggedChunk(ChunkPos);

        if (_hasVoxelData || backloggedVoxels != null)
        {
            var chunk = world.GetOrCreateChunk(ChunkPos);

            if (_hasVoxelData)
            {
                chunk.PopulateFromBuffer(_voxelData);

                foreach (var kvp in _localAuxData)
                {
                    var globalPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(kvp.Key, ChunkPos);
                    world.SetVoxelAuxiliaryData(globalPos, kvp.Value);
                }
            }

            if (backloggedVoxels != null)
            {
                foreach (var voxel in backloggedVoxels)
                {
                    chunk.SetVoxel(voxel.LocalVoxelPos, voxel.Type);
                    if (voxel.AuxData.HasValue)
                    {
                        var globalPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(voxel.LocalVoxelPos, ChunkPos);
                        world.SetVoxelAuxiliaryData(globalPos, voxel.AuxData.Value);
                    }
                }
            }

            worldUpdateScheduler.AddChunkMeshRebuildJob(ChunkPos);
        }
    }

    public override bool Equals(object rhs) => (rhs is ChunkVoxelCreationJob rhsJob) && (rhsJob.ChunkPos == ChunkPos);

    public override int GetHashCode() => HashCode.Combine(typeof(ChunkGenerationJob), ChunkPos);

    public override string ToString() => $"ChunkVoxelCreationJob(ChunkPos={ChunkPos})";

    private readonly ushort[,,]                   _voxelData;
    private readonly bool                          _hasVoxelData;
    private readonly Dictionary<Vector3Int, ushort> _localAuxData;
}
