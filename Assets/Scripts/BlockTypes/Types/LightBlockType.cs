using UnityEngine;

public class LightBlockType : BlockTypeBase
{
    public LightBlockType(ushort voxelType, BlockData blockData)
        : base(voxelType, blockData)
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
        Chunk chunk, 
        Vector3Int globalPosition, 
        Vector3Int localPosition, 
        BlockFace? placementFace, 
        BlockFace? lookDir) 
    {         
        world.AddLight(globalPosition, BlockData.LightColor.Value);

        return true; 
    }

    public override bool OnRemove(
        VoxelWorld world, 
        Chunk chunk, 
        Vector3Int globalPosition, 
        Vector3Int localPosition) 
    { 
        world.RemoveLight(globalPosition, false);

        return true; 
    }

    public override bool OnUse(
        VoxelWorld world, 
        Vector3Int globalPosition, 
        BlockFace lookDir) 
    { 
        return true; 
    }

    public override BlockFace GetForwardFace(
        VoxelWorld world, 
        Vector3Int globalPosition)
    {
        return BlockFace.Back;
    }
}
 