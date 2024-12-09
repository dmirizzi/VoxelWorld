using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

class BlockLightUpdateJob : IWorldUpdateJob
{
    public int UpdateStage => 4;

    public Vector3Int ChunkPos { get; private set; }

    public Vector3Int LightPos { get; private set; }

    public Color32 LightColor { get; private set; }

    public bool AddLight { get; private set; }

    public bool Sunlight { get; private set; }

    public HashSet<Vector3Int> AffectedChunks { get; private set; }

    public BlockLightUpdateJob(Vector3Int chunkPos, Vector3Int lightPos, Color32 lightColor, bool addLight, bool sunlight)
    {
        ChunkPos = chunkPos;
        LightPos = lightPos;
        LightColor = lightColor;
        AddLight = addLight;
        Sunlight = sunlight;

        AffectedChunks = new HashSet<Vector3Int>();
        for(int z = -1; z <= 1; ++z)
        {
            for(int y = -1; y <= 1; ++y)
            {
                for(int x = -1; x <= 1; ++x)
                {
                    AffectedChunks.Add(ChunkPos + new Vector3Int(x, y, z));
                }                
            }
        }
    }

    public bool PreExecuteSync(VoxelWorld world, WorldGenerator worldGenerator)
    {
        _lightMap = world.GetLightMap();
        return _lightMap != null;
    }

    public Task ExecuteAsync()
    {
        return Task.Run(() => 
        {
            UnityEngine.Profiling.Profiler.BeginThreadProfiling("WorldUpdateJobs", "BlockLightUpdateJobThread");

            var colorChannels = new byte[]{
                (byte)(LightColor.r >> 4),
                (byte)(LightColor.g >> 4),
                (byte)(LightColor.b >> 4)
            };

            if(Sunlight && !AddLight)
            {
                _lightMap.RemoveLight(LightPos, Chunk.SunlightChannel, _affectedChunks);
            }
            else
            {
                for(int channel = 0; channel < 3; ++channel)
                {            
                    if(AddLight)
                    {
                        _lightMap.AddLight(LightPos, channel, colorChannels[channel], _affectedChunks);
                    }
                    else
                    {
                        _lightMap.RemoveLight(LightPos, channel, _affectedChunks);                
                    }
                }
            }

            UnityEngine.Profiling.Profiler.EndThreadProfiling();
        });
    }

    public void PostExecuteSync(VoxelWorld world, WorldGenerator worldGenerator, WorldUpdateScheduler worldUpdateScheduler)
    {
        world.QueueChunksForLightMappingUpdate(_affectedChunks);
    }

    public override bool Equals(object rhs) =>
        (rhs is BlockLightUpdateJob rhsJob)
            && (ChunkPos == rhsJob.ChunkPos)
            && (LightPos == rhsJob.LightPos)
            && (AddLight == rhsJob.AddLight)
            && (Sunlight == rhsJob.Sunlight);

    public override int GetHashCode() => 
        HashCode.Combine(
            typeof(BlockLightUpdateJob), 
            ChunkPos,
            LightPos,
            AddLight,
            Sunlight);

    public override string ToString() => 
        $"BlockLightUpdateJob(Pos={LightPos}|Col={LightColor}|Add={AddLight}|Sunlight={Sunlight})";

    private LightMap _lightMap;

    private HashSet<Vector3Int> _affectedChunks = new HashSet<Vector3Int>();
}