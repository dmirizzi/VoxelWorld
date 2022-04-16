using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

// Based on Seed of Andromeda fast flood fill lighting
// https://web.archive.org/web/20210429192404/https://www.seedofandromeda.com/blogs/29-fast-flood-fill-lighting-in-a-blocky-voxel-game-pt-1
public class LightMap
{
    private const float LightAttenuationFactor = 0.75f;

    public LightMap(VoxelWorld world)
    {
        _world = world;
    }

    public void AddLight(Vector3Int sourcePos, int channel, byte intensity, int range, HashSet<Chunk> visitedChunks)
    {
        var firstChunk = _world.GetChunkFromVoxelPosition(sourcePos.x, sourcePos.y, sourcePos.z, true);
        if(firstChunk == null)
        {
            return;
        }
        firstChunk.SetLightChannelValue(VoxelPosConverter.GlobalToChunkLocalVoxelPos(sourcePos), channel, intensity);

        var lightNodes = new Queue<LightNode>();
        lightNodes.Enqueue(new LightNode 
        {
            GlobalPos = sourcePos,
            Chunk = firstChunk
        });

        var visitedNodes = new HashSet<Vector3Int>();
        visitedNodes.Add(sourcePos);

        PropagateLightNodes(lightNodes, channel, visitedNodes, visitedChunks);
    }

    public void UpdateOnRemovedSolidVoxel(Vector3Int globalRemovedVoxelPos, HashSet<Chunk> visitedChunks)
    {
        var lightNodes = new Queue<LightNode>();
        foreach(var dir in fillDirections)
        {
            var neighborGlobalPos = globalRemovedVoxelPos + dir;
            var chunk = _world.GetChunkFromVoxelPosition(neighborGlobalPos.x, neighborGlobalPos.y, neighborGlobalPos.z, true);
            lightNodes.Enqueue(new LightNode
            {
                GlobalPos = neighborGlobalPos,
                Chunk = chunk
            });
        }

        var visitedNodes = new HashSet<Vector3Int>();

        for(int channel = 0; channel < 3; ++channel)
        {
            PropagateLightNodes(lightNodes, channel, visitedNodes, visitedChunks);
        }
    }

    public void RemoveLight(int x, int y, int z, Color32 color)
    {

    }    

    private void PropagateLightNodes(Queue<LightNode> lightNodes, int channel, HashSet<Vector3Int> visitedNodes, HashSet<Chunk> visitedChunks)
    {
        while(lightNodes.Count > 0)
        {
            var node = lightNodes.Dequeue();

            visitedChunks.Add(node.Chunk);

            var localPos = VoxelPosConverter.GlobalToChunkLocalVoxelPos(node.GlobalPos);
            var currentLightLevel = node.Chunk.GetLightChannelValue(localPos, channel);           

            foreach(var dir in fillDirections)
            {
                var neighborGlobalPos = node.GlobalPos + dir;
                if(visitedNodes.Contains(neighborGlobalPos))
                {
                    continue;
                }
                visitedNodes.Add(neighborGlobalPos);

                //TODO: Faster to only get new chunk when crossing chunk borders?
                var chunk = _world.GetChunkFromVoxelPosition(neighborGlobalPos.x, neighborGlobalPos.y, neighborGlobalPos.z, true);
                if(chunk != null)
                {
                    var localNeighborPos = VoxelPosConverter.GlobalToChunkLocalVoxelPos(neighborGlobalPos);
                    if(chunk.GetLightChannelValue(localNeighborPos, channel) + 2 <= currentLightLevel)
                    {
                        chunk.SetLightChannelValue(localNeighborPos, channel, currentLightLevel * LightAttenuationFactor );

                        if(!VoxelBuildHelper.IsVoxelSideOpaque(_world, chunk.GetVoxel(localPos), node.GlobalPos, dir))
                        {
                            lightNodes.Enqueue(new LightNode
                            {
                                GlobalPos = neighborGlobalPos,
                                Chunk = chunk
                            });
                        }
                    }
                }
            }            
        }
    }

    private VoxelWorld _world;

    private struct LightNode
    {
        public Chunk Chunk;
        public Vector3Int GlobalPos;
    }

    private Vector3Int[] fillDirections = 
    {
        Vector3Int.up,
        Vector3Int.down,
        Vector3Int.left,
        Vector3Int.right,
        Vector3Int.forward,
        Vector3Int.back,

        Vector3Int.up + Vector3Int.left,
        Vector3Int.up + Vector3Int.right,
        Vector3Int.up + Vector3Int.forward,
        Vector3Int.up + Vector3Int.back,
        Vector3Int.up + Vector3Int.left + Vector3Int.forward,
        Vector3Int.up + Vector3Int.left + Vector3Int.back,
        Vector3Int.up + Vector3Int.right + Vector3Int.forward,
        Vector3Int.up + Vector3Int.right + Vector3Int.back,

        Vector3Int.down + Vector3Int.left,
        Vector3Int.down + Vector3Int.right,
        Vector3Int.down + Vector3Int.forward,
        Vector3Int.down + Vector3Int.back,
        Vector3Int.down + Vector3Int.left + Vector3Int.forward,
        Vector3Int.down + Vector3Int.left + Vector3Int.back,
        Vector3Int.down + Vector3Int.right + Vector3Int.forward,
        Vector3Int.down + Vector3Int.right + Vector3Int.back,

        Vector3Int.left + Vector3Int.forward,
        Vector3Int.left + Vector3Int.back,
        Vector3Int.right + Vector3Int.forward,
        Vector3Int.right + Vector3Int.back
    };
}

