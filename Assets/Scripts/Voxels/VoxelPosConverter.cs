using UnityEngine;

public static class VoxelPosConverter
{
    public static Vector3Int GetVoxelPosFromWorldPos(Vector3 worldPos)
    {
        // Correct for voxels in negative space - they are offset by 1 compared to positive space
        // i.e. voxels in positive space start at 0, in negative they start at -1
        Vector3Int negativeOffset = new Vector3Int(
            worldPos.x < 0f ? -1 : 0,
            worldPos.y < 0f ? -1 : 0,
            worldPos.z < 0f ? -1 : 0
        );

        return new Vector3Int(
            (int)worldPos.x,
            (int)worldPos.y,
            (int)worldPos.z
        ) + negativeOffset;
    }

    public static Vector3 GetVoxelCenterWorldPos(Vector3Int globalVoxelPos)
    {
        return new Vector3(
            globalVoxelPos.x + VoxelInfo.VoxelSize * 0.5f,
            globalVoxelPos.y + VoxelInfo.VoxelSize * 0.5f,
            globalVoxelPos.z + VoxelInfo.VoxelSize * 0.5f
        );
    }

        public static Vector3 GetVoxelCenterSurfaceWorldPos(Vector3Int globalVoxelPos)
    {
        return new Vector3(
            globalVoxelPos.x + VoxelInfo.VoxelSize * 0.5f,
            globalVoxelPos.y,
            globalVoxelPos.z + VoxelInfo.VoxelSize * 0.5f
        );
    }

    public static Vector3 GetVoxelTopCenterSurfaceWorldPos(Vector3Int globalVoxelPos)
    {
        return new Vector3(
            globalVoxelPos.x + VoxelInfo.VoxelSize * 0.5f,
            globalVoxelPos.y + VoxelInfo.VoxelSize,
            globalVoxelPos.z + VoxelInfo.VoxelSize * 0.5f
        );
    }

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
