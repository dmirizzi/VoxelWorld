using System.Collections.Generic;
using UnityEngine;

// Allows custom logic for voxel geometry, placing, removing etc.
public interface IBlockType
{
    bool HasGameObject { get; }

    bool HasCustomVoxelMesh { get; }

    // Called when a chunk mesh is being built. Custom meshes can be added here that will use
    // the global voxel material (ie texture atlas).
    void OnChunkVoxelMeshBuild(
        VoxelWorld world,
        Chunk chunk,
        VoxelType voxelType, 
        Vector3Int globalVoxelPos,
        Vector3Int localVoxelPos, 
        ChunkMesh chunkMesh);

    // Called, when the chunk is (re)built. Any GameObject blocks needed shall be added to the chunk in this method.
    void OnChunkBuild(Chunk chunk, Vector3Int localPosition);

    // Is called when the player attempts to place a block of this type. This will always be called before the build methods,
    // so that voxel auxiliary data can be set for building.
    // Returns true if it can be placed, false otherwise
    bool OnPlace(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition, VoxelFace? placementFace);

    // Is called when the player attempts to remove/replace a block of this type.
    // Returns true if it can be removed, false otherwise
    bool OnRemove(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition);
}
