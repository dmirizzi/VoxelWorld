using System;
using UnityEngine;

public class LightBlockType : BlockTypeBase
{
    public LightBlockType(ushort voxelType, BlockData blockData)
        : base(
            voxelType, 
            blockData,
            new OnOffProperty())
    {
    }

    public override void OnChunkVoxelMeshBuild(
        VoxelWorld world, 
        Chunk chunk, 
        ushort voxelType, 
        Vector3Int globalVoxelPos, 
        Vector3Int localVoxelPos, 
        ChunkMesh chunkMesh) 
    {     
        var chunkBuilder = world.GetChunkBuilder(chunk.ChunkPos);

        chunkBuilder.AddVoxelVertices(
                VoxelType,
                globalVoxelPos,
                localVoxelPos,
                chunkMesh);
    }

    public override bool OnPlace(
        VoxelWorld world, 
        Vector3Int globalPosition, 
        BlockFace? placementFace, 
        BlockFace? lookDir) 
    {         
        world.AddLight(globalPosition, BlockData.LightColor.Value);
        SetProperty<OnOffProperty>(world, globalPosition, OnOffProperty.On);

        return true; 
    }

    public override bool OnRemove(
        VoxelWorld world, 
        Vector3Int globalPosition) 
    { 
        world.RemoveLight(globalPosition, false);

        return true; 
    }

    public override bool OnUse(
        VoxelWorld world, 
        Vector3Int globalPosition, 
        BlockFace lookDir) 
    { 
        Func<OnOffProperty, OnOffProperty> updateFunc = prop => 
        {
            if(prop.OnOffState)
            {
                world.RemoveLight(globalPosition, false);
            }
            else
            {
                world.AddLight(globalPosition, BlockData.LightColor.Value);
            }

            prop.OnOffState = !prop.OnOffState;
            return prop;
        };

        UpdateProperty<OnOffProperty>(world, globalPosition, updateFunc);

        return true; 
    }

    public override BlockFace GetForwardFace(
        VoxelWorld world, 
        Vector3Int globalPosition)
    {
        return BlockFace.Back;
    }
}
 