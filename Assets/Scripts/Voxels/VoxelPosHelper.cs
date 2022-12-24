using UnityEngine;

public static class VoxelPosHelper
{
    public static bool WorldPosIsOnVoxelSurface(Vector3 worldPos)
    {
        for(var i = 0; i < 3; ++i)
        {
            var fraction = Mathf.Abs(worldPos[i] - Mathf.Floor(worldPos[i]));
            if(fraction < 0.00001f || fraction > 0.99999f)
            {
                // For some reason, sometimes floor(x) rounds down to the lower integer (e.g. 14 -> 13)
                // which causes (x - floor(x)) to be close to 1, so we need to check both whether the diff
                // is either close to 0 or close to 1
                return true;
            }
        }

        return false;
    }

    public static Vector3Int WorldPosToGlobalVoxelPos(Vector3 worldPos)
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
        return ChunkPosToGlobalChunkBaseVoxelPos(chunkPos) + localVoxelPos;
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

    public static Vector3Int WorldPosToChunkPos(Vector3 worldPos)
    {
        var globalVoxelPos = WorldPosToGlobalVoxelPos(worldPos);
        return GlobalVoxelPosToChunkPos(globalVoxelPos);
    }

    public static Vector3Int GlobalVoxelPosToChunkPos(Vector3Int globalVoxelPos)
    {
        var x = (int)globalVoxelPos.x;
        if(globalVoxelPos.x < 0) x += 1;
        x /= VoxelInfo.ChunkSize;
        if(globalVoxelPos.x < 0) x -= 1;

        var y = (int)globalVoxelPos.y;
        if(globalVoxelPos.y < 0) y += 1;
        y /= VoxelInfo.ChunkSize;
        if(globalVoxelPos.y < 0) y -= 1;

        var z = (int)globalVoxelPos.z;
        if(globalVoxelPos.z < 0) z += 1;
        z /= VoxelInfo.ChunkSize;
        if(globalVoxelPos.z < 0) z -= 1;

        return new Vector3Int(x, y, z);
    }

    public static Vector3Int ChunkPosToGlobalChunkBaseVoxelPos(Vector3Int chunkPos)
    {
        return new Vector3Int(
            chunkPos.x * VoxelInfo.ChunkSize,
            chunkPos.y * VoxelInfo.ChunkSize,
            chunkPos.z * VoxelInfo.ChunkSize
        );
    }

}
