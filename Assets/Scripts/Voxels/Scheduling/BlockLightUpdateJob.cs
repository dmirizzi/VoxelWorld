using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

class BlockLightUpdateJob : IWorldUpdateJob
{
    public int UpdateStage => 1;

    public Vector3Int ChunkPos { get; private set; }

    public Vector3Int LightPos { get; private set; }

    public Color32 LightColor { get; private set; }

    public bool AddLight { get; private set; }

    public HashSet<Vector3Int> AffectedChunks { get; private set; }

    public BlockLightUpdateJob(Vector3Int chunkPos, Vector3Int lightPos, Color32 lightColor, bool addLight)
    {
        ChunkPos = chunkPos;
        LightPos = lightPos;
        LightColor = lightColor;
        AddLight = addLight;

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

    public bool PreExecuteSync(VoxelWorld world)
    {
        _lightMap = world.GetLightMap();
        return _lightMap != null;
    }

    public Task ExecuteAsync()
    {
        return Task.Run(() => 
        {
            var colorChannels = new byte[]{
                (byte)(LightColor.r >> 4),
                (byte)(LightColor.g >> 4),
                (byte)(LightColor.b >> 4)
            };

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
        });
    }

    public void PostExecuteSync(VoxelWorld world)
    {
        world.QueueChunksForLightMappingUpdate(_affectedChunks);
    }

    public override bool Equals(object rhs) =>
        (rhs is BlockLightUpdateJob rhsJob)
            && (ChunkPos == rhsJob.ChunkPos)
            && (LightPos == rhsJob.LightPos)
            && (AddLight == rhsJob.AddLight);

    public override int GetHashCode() => 
        HashCode.Combine(
            typeof(BlockLightUpdateJob), 
            ChunkPos,
            LightPos,
            AddLight);

    public override string ToString() => 
        $"BlockLightUpdateJob(Pos={LightPos}|Col={LightColor}|Add={AddLight})";

    private LightMap _lightMap;

    private HashSet<Vector3Int> _affectedChunks = new HashSet<Vector3Int>();
}