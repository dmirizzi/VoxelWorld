using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IBlockType
{
    // Called, when the chunk is (re)built. Any GameObjects needed shall be added to the chunk in this method.
    void OnChunkBuild(Chunk chunk, Vector3Int localPosition);

    // Is called when the player attempts to place a block of this type
    // Returns true if it can be placed, false otherwise
    bool OnPlace(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition, VoxelFace? placementFace);

    // Is called when the player attempts to remove/replace a block of this type
    // Returns true if it can be removed, false otherwise
    bool OnRemove(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition);
}
