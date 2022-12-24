using UnityEngine;

public class WorldGenUtil
{
    public static float GetChunkSqrDistanceToWorldPos(Vector3 worldPos, Vector3Int chunkPos)
    {
        var playerVoxelPos = VoxelPosHelper.WorldPosToGlobalVoxelPos(worldPos);
        var playerChunkPos = VoxelPosHelper.GlobalVoxelPosToChunkPos(playerVoxelPos);
        return (playerChunkPos - playerVoxelPos).sqrMagnitude;
    }
}