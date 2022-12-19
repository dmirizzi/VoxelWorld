using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

// Based on Seed of Andromeda fast flood fill lighting
// https://web.archive.org/web/20210429192404/https://www.seedofandromeda.com/blogs/29-fast-flood-fill-lighting-in-a-blocky-voxel-game-pt-1
public class LightMap
{
    private const float LightAttenuationFactor = 0.75f;

    public List<Vector3Int> LastUpdatedVoxels = new List<Vector3Int>();

    public LightMap(VoxelWorld world)
    {
        _world = world;
    }

    public void AddLight(Vector3Int lightPos, int channel, byte intensity, HashSet<Vector3Int> visitedChunks)
    {
        var firstChunk = _world.GetChunkFromVoxelPosition(lightPos.x, lightPos.y, lightPos.z, true);
        if(firstChunk == null)
        {
            return;
        }
        firstChunk.SetLightChannelValue(VoxelPosHelper.GlobalToChunkLocalVoxelPos(lightPos), channel, intensity);

        var lightNodes = new Queue<LightNode>();
        lightNodes.Enqueue(new LightNode 
        {
            GlobalPos = lightPos,
            Chunk = firstChunk
        });

        var visitedNodes = new HashSet<Vector3Int>();
        visitedNodes.Add(lightPos);

        PropagateLightNodes(lightNodes, channel, visitedNodes, visitedChunks);
    }

    public void UpdateOnRemovedSolidVoxel(Vector3Int globalRemovedVoxelPos, HashSet<Vector3Int> visitedChunks)
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

        UnityEngine.Debug.Log($"Removed solid voxel: {_world.GetVoxel(globalRemovedVoxelPos)}");

        for(int channel = 0; channel < 3; ++channel)
        {
            var visitedNodes = new HashSet<Vector3Int>();
            var nodes = new Queue<LightNode>(lightNodes);
            PropagateLightNodes(nodes, channel, visitedNodes, visitedChunks);
        }
    }

    public void RemoveLight(Vector3Int lightPos, int channel, byte intensity, HashSet<Vector3Int> visitedChunks)
    {
        var firstChunk = _world.GetChunkFromVoxelPosition(lightPos.x, lightPos.y, lightPos.z, true);
        if(firstChunk == null)
        {
            return;
        }
        var localPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(lightPos);
        var lightLevel = firstChunk.GetLightChannelValue(localPos, channel);
        firstChunk.SetLightChannelValue(localPos, channel, 0);

        var removeLightNodes = new Queue<RemoveLightNode>();
        removeLightNodes.Enqueue(new RemoveLightNode
        {
            Chunk = firstChunk,
            GlobalPos = lightPos,
            LightLevel = lightLevel
        });

        var visitedNodes = new HashSet<Vector3Int>();
        var lightNodes = new Queue<LightNode>();

        while(removeLightNodes.Count > 0)
        {
            var removeLightNode = removeLightNodes.Dequeue();
            visitedChunks.Add(removeLightNode.Chunk.ChunkPos);

            foreach(var dir in fillDirections)
            {
                var neighborGlobalPos = removeLightNode.GlobalPos + dir;
                if(visitedNodes.Contains(neighborGlobalPos))
                {
                    continue;
                }
                visitedNodes.Add(neighborGlobalPos);

                var neighborChunk = _world.GetChunkFromVoxelPosition(neighborGlobalPos.x, neighborGlobalPos.y, neighborGlobalPos.z, true);
                if(neighborChunk != null)
                {
                    var localNeighborPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(neighborGlobalPos);
                    var neighborLightLevel = neighborChunk.GetLightChannelValue(localNeighborPos, channel);
                    if(neighborLightLevel > 0 && neighborLightLevel < removeLightNode.LightLevel)                
                    {
                        neighborChunk.SetLightChannelValue(localNeighborPos, channel, 0);
                        removeLightNodes.Enqueue(new RemoveLightNode
                        {
                            Chunk = neighborChunk,
                            GlobalPos = neighborGlobalPos,
                            LightLevel = neighborLightLevel
                        });
                    }
                    else if(neighborLightLevel >= removeLightNode.LightLevel)
                    {
                        lightNodes.Enqueue(new LightNode
                        {
                            GlobalPos = neighborGlobalPos,
                            Chunk = neighborChunk
                        });
                    }
                }
            }
        }

        PropagateLightNodes(lightNodes, channel, new HashSet<Vector3Int>(), visitedChunks);
    }    

    private void PropagateLightNodes(Queue<LightNode> lightNodes, int channel, HashSet<Vector3Int> visitedNodes, HashSet<Vector3Int> visitedChunks)
    {
        //TODO: Do we even need to pass visitedNodes into here? We always pass in a new hashset!!

        LastUpdatedVoxels.Clear();

        int totalNodesProcessed = 0;

        UnityEngine.Debug.Log($"Processing channel {channel}, {lightNodes.Count} nodes, {visitedNodes.Count} already visited");

        var sw = new Stopwatch();
        sw.Start();

        while(lightNodes.Count > 0)
        {
            var node = lightNodes.Dequeue();

            //TODO: Should we only do this for chunks where a light value was actually changed below?
            visitedChunks.Add(node.Chunk.ChunkPos);

            var localPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(node.GlobalPos);
            var currentLightLevel = node.Chunk.GetLightChannelValue(localPos, channel);           

            foreach(var neighborDir in fillDirections)
            {
                var neighborGlobalPos = node.GlobalPos + neighborDir;
                if(visitedNodes.Contains(neighborGlobalPos))
                {
                    continue;
                }
                
                var neighborChunk = _world.GetChunkFromVoxelPosition(neighborGlobalPos.x, neighborGlobalPos.y, neighborGlobalPos.z, true);

                var localNeighborPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(neighborGlobalPos);

                var newLightLevel = currentLightLevel * LightAttenuationFactor;

                if(neighborChunk != null 
                    && !VoxelBuildHelper.IsVoxelNeighborOpaque(_world, node.GlobalPos, neighborDir)

                    // Should this check via LightAttenuationFactor instead of +2?
                    && neighborChunk.GetLightChannelValue(localNeighborPos, channel) + 2 <= currentLightLevel)
                {
                    visitedNodes.Add(neighborGlobalPos);
                    LastUpdatedVoxels.Add(neighborGlobalPos);

                    totalNodesProcessed++;
                    
                    neighborChunk.SetLightChannelValue(localNeighborPos, channel, newLightLevel);

                    lightNodes.Enqueue(new LightNode
                    {
                        GlobalPos = neighborGlobalPos,
                        Chunk = neighborChunk
                    });
                }
            }            
        }

        sw.Stop();

        UnityEngine.Debug.Log($"Total Nodes Processed = {totalNodesProcessed} in {sw.ElapsedMilliseconds}ms");
    }

    private VoxelWorld _world;

    private struct LightNode
    {
        public Chunk Chunk;
        public Vector3Int GlobalPos;
    }

    private struct RemoveLightNode
    {
        public Chunk Chunk;

        public Vector3Int GlobalPos;

        public float LightLevel;
    }

    private Vector3Int[] fillDirections = 
    {
        Vector3Int.up,
        Vector3Int.down,
        Vector3Int.left,
        Vector3Int.right,
        Vector3Int.forward,
        Vector3Int.back,
/*
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
*/        
    };
}

