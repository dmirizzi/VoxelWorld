using System;
using System.Collections.Generic;
using UnityEngine;

// Based on Seed of Andromeda fast flood fill lighting
// https://web.archive.org/web/20210429192404/https://www.seedofandromeda.com/blogs/29-fast-flood-fill-lighting-in-a-blocky-voxel-game-pt-1
public class LightMap
{
    public LightMap(VoxelWorld world)
    {
        _world = world;
    }

    public void AddLight(Vector3Int lightPos, int channel, byte intensity, HashSet<Vector3Int> visitedChunks)
    {
        var firstChunk = _world.GetChunkFromVoxelPosition(lightPos, true);
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
        // Fill in the gap in the light map left by the removed solid voxel by propagating the surrounding light nodes
        var lightNodes = new Queue<LightNode>();
        foreach(var neighborFace in fillDirections)
        {
            var neighborDir = BlockFaceHelper.GetVectorIntFromBlockFace(neighborFace);
            var neighborGlobalPos = globalRemovedVoxelPos + neighborDir;
            var chunk = _world.GetChunkFromVoxelPosition(neighborGlobalPos, true);
            lightNodes.Enqueue(new LightNode
            {
                GlobalPos = neighborGlobalPos,
                Chunk = chunk
            });
        }

        for(int channel = 0; channel < 4; ++channel)
        {
            var visitedNodes = new HashSet<Vector3Int>();
            var nodes = new Queue<LightNode>(lightNodes);
            PropagateLightNodes(nodes, channel, visitedNodes, visitedChunks, channel == 3);
        }
    }

    public void RemoveLight(Vector3Int lightPos, int channel, HashSet<Vector3Int> visitedChunks)
    {
        var firstChunk = _world.GetChunkFromVoxelPosition(lightPos, true);
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
            foreach(var chunk in GetAffectedChunks(removeLightNode.Chunk.ChunkPos, localPos))
            {
                visitedChunks.Add(chunk);
            }

            foreach(var neighborFace in fillDirections)
            {
                var neighborDir = BlockFaceHelper.GetVectorIntFromBlockFace(neighborFace);
                var neighborGlobalPos = removeLightNode.GlobalPos + neighborDir;
                if(visitedNodes.Contains(neighborGlobalPos))
                {
                    continue;
                }
                visitedNodes.Add(neighborGlobalPos);

                var neighborChunk = _world.GetChunkFromVoxelPosition(neighborGlobalPos, true);
                if(neighborChunk != null)
                {
                    var localNeighborPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(neighborGlobalPos);
                    var neighborLightLevel = neighborChunk.GetLightChannelValue(localNeighborPos, channel);
                    if(neighborLightLevel > 0 && neighborLightLevel < removeLightNode.LightLevel

                        // Remove sunlight downwards as long as its at max level
                        || (neighborDir == Vector3Int.down && channel == Chunk.SunlightChannel && removeLightNode.LightLevel == 15))                
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

    public void UpdateSunlight(IEnumerable<Chunk> topMostChunks, HashSet<Vector3Int> visitedChunks)
    {        
        var lightNodes = new Queue<LightNode>();

        foreach(var chunk in topMostChunks)
        {
            for(int x = 0; x < VoxelInfo.ChunkSize; ++x)
            {
                for(int z = 0; z < VoxelInfo.ChunkSize; ++z)
                {
                    var localVoxelPos = new Vector3Int(x, VoxelInfo.ChunkSize - 1, z);
                    chunk.SetLightChannelValue(localVoxelPos, Chunk.SunlightChannel, 15);
                    lightNodes.Enqueue(new LightNode{
                        Chunk = chunk,
                        GlobalPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(localVoxelPos, chunk.ChunkPos)
                    });
                }
            }
        }


        PropagateLightNodes(
            lightNodes,
            Chunk.SunlightChannel,
            new HashSet<Vector3Int>(),
            visitedChunks,
            true
        );
    }

    // Propagate the light from all the surrounding voxels into the new chunk
    public void PropagateSurroundingLightsOnNewChunk(Vector3Int chunkPos)
    {
        var lightNodes = new Queue<LightNode>();

        void EnqueueLightNode(Chunk chunk, Vector3Int chunkPos, Vector3Int localVoxelPos)
        {
            if(chunk.HasAnyBlockLight(localVoxelPos))
            {
                lightNodes.Enqueue(new LightNode{
                    Chunk = chunk,
                    GlobalPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(localVoxelPos, chunkPos)
                });
            } 
        }

        var topChunkPos = chunkPos + Vector3Int.up;
        if(_world.TryGetChunk(topChunkPos, out var topChunk))
        {
            for(int x = 0; x < VoxelInfo.ChunkSize; ++x)
            {
                for(int z = 0; z < VoxelInfo.ChunkSize; ++z)
                {
                    var localVoxelPos = new Vector3Int(x, 0, z);
                    EnqueueLightNode(topChunk, topChunkPos, localVoxelPos);
                }
            }
        }

        var bottomChunkPos = chunkPos + Vector3Int.down;
        if(_world.TryGetChunk(bottomChunkPos, out var bottomChunk))
        {
            for(int x = 0; x < VoxelInfo.ChunkSize; ++x)
            {
                for(int z = 0; z < VoxelInfo.ChunkSize; ++z)
                {
                    var localVoxelPos = new Vector3Int(x, VoxelInfo.ChunkSize - 1, z);
                    EnqueueLightNode(bottomChunk, bottomChunkPos, localVoxelPos);
                }
            }
        }

        var leftChunkPos = chunkPos + Vector3Int.left;
        if(_world.TryGetChunk(leftChunkPos, out var leftChunk))
        {
            for(int y = 0; y < VoxelInfo.ChunkSize; ++y)
            {
                for(int z = 0; z < VoxelInfo.ChunkSize; ++z)
                {
                    var localVoxelPos = new Vector3Int(VoxelInfo.ChunkSize - 1, y, z);
                    EnqueueLightNode(leftChunk, leftChunkPos, localVoxelPos);
                }
            }
        }

        var rightChunkPos = chunkPos + Vector3Int.right;
        if(_world.TryGetChunk(rightChunkPos, out var rightChunk))
        {
            for(int y = 0; y < VoxelInfo.ChunkSize; ++y)
            {
                for(int z = 0; z < VoxelInfo.ChunkSize; ++z)
                {
                    var localVoxelPos = new Vector3Int(0, y, z);
                    EnqueueLightNode(rightChunk, rightChunkPos, localVoxelPos);
                }
            }
        }

        var forwardChunkPos = chunkPos + Vector3Int.forward;
        if(_world.TryGetChunk(forwardChunkPos, out var forwardChunk))
        {
            for(int x = 0; x < VoxelInfo.ChunkSize; ++x)
            {
                for(int y = 0; y < VoxelInfo.ChunkSize; ++y)
                {
                    var localVoxelPos = new Vector3Int(x, y, 0);
                    EnqueueLightNode(forwardChunk, forwardChunkPos, localVoxelPos);
                }
            }
        }

        var backChunkPos = chunkPos + Vector3Int.back;
        if(_world.TryGetChunk(backChunkPos, out var backChunk))
        {
            for(int x = 0; x < VoxelInfo.ChunkSize; ++x)
            {
                for(int y = 0; y < VoxelInfo.ChunkSize; ++y)
                {
                    var localVoxelPos = new Vector3Int(x, y, VoxelInfo.ChunkSize - 1);
                    EnqueueLightNode(backChunk, backChunkPos, localVoxelPos);
                }
            }
        }

        for(int channel = 0; channel < 3; ++channel)
        {
            PropagateLightNodes(
                new Queue<LightNode>(lightNodes),
                channel,
                new HashSet<Vector3Int>(),
                new HashSet<Vector3Int>(),
                false
            );
        }
    }

    private void PropagateLightNodes(
        Queue<LightNode> lightNodes, 
        int channel, 
        HashSet<Vector3Int> visitedNodes, 
        HashSet<Vector3Int> visitedChunks,
        bool isSunlight = false )
    {        
        //TODO:Alternative approach:
        //TODO: - Connect all chunks via pointers
        //TODO: - Move from voxel to voxel with local positions + current chunk pointer
        //TODO: - If voxelPos < 0 or > 15, move over to appropriate chunk via pointer from current chunk
        //TODO: - Update visitedChunks when crossing over chunks
        //TODO: -> Avoid frequent conversions and chunk lookups, but need to update pointers when chunks are loaded/unloaded
        //TODO: -> Chunk could "notify" its neighbors when it is created or deleted
        while(lightNodes.Count > 0)
        {
            var lightNode = lightNodes.Dequeue();


            var localPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(lightNode.GlobalPos);

            // ~250ms / ~1100ms
            foreach(var chunk in GetAffectedChunks(lightNode.Chunk.ChunkPos, localPos))
            {
                visitedChunks.Add(chunk);
            }


            foreach(var neighborFace in fillDirections)
            {                
                var neighborDir = BlockFaceHelper.GetVectorIntFromBlockFace(neighborFace);
                var neighborGlobalPos = lightNode.GlobalPos + neighborDir;                
                // ~280ms / ~1100ms
                var neighborChunk = _world.GetChunkFromVoxelPosition(neighborGlobalPos, false);                 

                if(neighborChunk == null)
                {
                    visitedNodes.Add(neighborGlobalPos);
                    continue;
                }

                var localNeighborPos = VoxelPosHelper.GlobalToChunkLocalVoxelPos(neighborGlobalPos);
                var neighborLightLevel = neighborChunk.GetLightChannelValue(localNeighborPos, channel);

                // In case of sunlight, allow a light node to be set again if it is being lit directly by the sun and only 
                // indirectly before
                var currentLightLevel = lightNode.Chunk.GetLightChannelValue(localPos, channel);           
                if(!isSunlight || currentLightLevel < 15 || neighborLightLevel == 15 )
                {
                    // Otherwise, skip already processed nodes
                    if(visitedNodes.Contains(neighborGlobalPos))
                    {
                        continue;
                    }
                }

                var neighborOpaque = VoxelBuildHelper.NeighborVoxelHasOpaqueSide(_world, lightNode.GlobalPos, neighborFace, neighborDir);

                if(neighborChunk != null 
                    && !neighborOpaque
                    && neighborLightLevel + 2 <= currentLightLevel)
                {
                    visitedNodes.Add(neighborGlobalPos);

                    byte newLightLevel = GetNewLightLevel(currentLightLevel, neighborDir, isSunlight);
                    neighborChunk.SetLightChannelValue(localNeighborPos, channel, newLightLevel);


                    //TODO: Do we actually need to store the chunk in the light node? Seems like we 
                    //TODO: fetch it for each neighbor anyways (compare performance with & without)
                    lightNodes.Enqueue(new LightNode
                    {
                        GlobalPos = neighborGlobalPos,
                        Chunk = neighborChunk
                    });
                }

            }                
        }
    }

    private byte GetNewLightLevel(byte currentLightLevel, Vector3Int fillDir, bool isSunlight)
    {
        if(isSunlight && fillDir == Vector3Int.down && currentLightLevel == 15)
        {
            // Propagate sunlight downwards infinitely until we hit an opaque block
            return 15;
        }
        else
        {
            // For normal block light, reduce light level by 1 for every step away from the light source
            return (byte)(currentLightLevel - 1);
        }
    }

    private IEnumerable<Vector3Int> GetAffectedChunks(Vector3Int chunkPos, Vector3Int localVoxelPos)
    {
        var chunks = new List<Vector3Int>();

        chunks.Add(chunkPos);

        if(localVoxelPos.x == 0)                           chunks.Add(chunkPos + Vector3Int.left);
        if(localVoxelPos.x == VoxelInfo.ChunkSize - 1)     chunks.Add(chunkPos + Vector3Int.right);
        if(localVoxelPos.y == 0)                           chunks.Add(chunkPos + Vector3Int.up);
        if(localVoxelPos.y == VoxelInfo.ChunkSize - 1)     chunks.Add(chunkPos + Vector3Int.down);
        if(localVoxelPos.z == 0)                           chunks.Add(chunkPos + Vector3Int.back);
        if(localVoxelPos.z == VoxelInfo.ChunkSize - 1)     chunks.Add(chunkPos + Vector3Int.forward);

        return chunks;
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

    private BlockFace[] fillDirections = 
    {
        BlockFace.Top,
        BlockFace.Bottom,
        BlockFace.Left,
        BlockFace.Right,
        BlockFace.Back,
        BlockFace.Front
    };
}

