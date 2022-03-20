using UnityEngine;

public static class VoxelPosConverter
{
    public static Vector3Int ChunkLocalVoxelPosToGlobal(Vector3Int localVoxelPos, Vector3Int chunkPos)
    {
        return ChunkToBaseVoxelPos(chunkPos) + localVoxelPos;
    }
    
    public static Vector3Int GlobalToChunkLocalVoxelPos(Vector3Int voxelPos)
    {
        var x = voxelPos.x % VoxelInfo.ChunkSize;
        if(x != 0 && voxelPos.x < 0) x += VoxelInfo.ChunkSize;
        var y = voxelPos.y % VoxelInfo.ChunkSize;
        if(y != 0 && voxelPos.y < 0) y += VoxelInfo.ChunkSize;
        var z = voxelPos.z % VoxelInfo.ChunkSize;
        if(z != 0 && voxelPos.z < 0) z += VoxelInfo.ChunkSize;

        return new Vector3Int(x, y, z);
    }

    public static Vector3Int VoxelToChunkPos(Vector3Int voxelPos)
    {
        var x = (int)voxelPos.x;
        if(voxelPos.x < 0) x += 1;
        x /= VoxelInfo.ChunkSize;
        if(voxelPos.x < 0) x -= 1;

        var y = (int)voxelPos.y;
        if(voxelPos.y < 0) y += 1;
        y /= VoxelInfo.ChunkSize;
        if(voxelPos.y < 0) y -= 1;

        var z = (int)voxelPos.z;
        if(voxelPos.z < 0) z += 1;
        z /= VoxelInfo.ChunkSize;
        if(voxelPos.z < 0) z -= 1;

        return new Vector3Int(x, y, z);
    }

    public static Vector3Int ChunkToBaseVoxelPos(Vector3Int chunkPos)
    {
        return new Vector3Int(
            chunkPos.x * VoxelInfo.ChunkSize,
            chunkPos.y * VoxelInfo.ChunkSize,
            chunkPos.z * VoxelInfo.ChunkSize
        );
    }

}
