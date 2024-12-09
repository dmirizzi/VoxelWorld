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
        var result = new Vector3Int(
            (int)worldPos.x + (worldPos.x < 0f ? -1 : 0),
            (int)worldPos.y + (worldPos.y < 0f ? -1 : 0),
            (int)worldPos.z + (worldPos.z < 0f ? -1 : 0)
        );

        return result;
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
        return new Vector3Int(
            ( chunkPos.x << VoxelInfo.ChunkSizePowerOfTwo ) + localVoxelPos.x,
            ( chunkPos.y << VoxelInfo.ChunkSizePowerOfTwo ) + localVoxelPos.y,
            ( chunkPos.z << VoxelInfo.ChunkSizePowerOfTwo ) + localVoxelPos.z
        );
    }
    
    public static Vector3Int GlobalToChunkLocalVoxelPos(Vector3Int voxelPos)
    {
        int mask = VoxelInfo.ChunkSize - 1;

        return new Vector3Int(
            voxelPos.x & mask, 
            voxelPos.y & mask, 
            voxelPos.z & mask
        );
    }

    public static Vector3Int WorldPosToChunkPos(Vector3 worldPos)
    {
        var globalVoxelPos = WorldPosToGlobalVoxelPos(worldPos);
        return GlobalVoxelPosToChunkPos(globalVoxelPos);
    }

    public static Vector3Int GlobalVoxelPosToChunkPos(Vector3Int globalVoxelPos)
    {
        int chunkSize = VoxelInfo.ChunkSize;
        int x = globalVoxelPos.x;
        int y = globalVoxelPos.y;
        int z = globalVoxelPos.z;

        // Equivalent to ((x + 1) / chunkSize - 1) for negative numbers and 
        // (x / chunkSize) for positive numbers
        x = (x + ((x >> 31) & 1)) / chunkSize + (x >> 31);
        y = (y + ((y >> 31) & 1)) / chunkSize + (y >> 31);
        z = (z + ((z >> 31) & 1)) / chunkSize + (z >> 31);

        return new Vector3Int(x, y, z);
    }    

    public static Vector3Int LocalVoxelPosToRelativeChunkPos(Vector3Int localVoxelPos)
    {
        return new Vector3Int(
            localVoxelPos.x >> VoxelInfo.ChunkSizePowerOfTwo,
            localVoxelPos.y >> VoxelInfo.ChunkSizePowerOfTwo,
            localVoxelPos.z >> VoxelInfo.ChunkSizePowerOfTwo
        );
    }

    public static Vector3Int LocalVoxelPosToNeighboringLocalVoxelPos(Vector3Int localVoxelPos)
    {
        return new Vector3Int(
            localVoxelPos.x < 0 ?  (localVoxelPos.x % VoxelInfo.ChunkSize) + VoxelInfo.ChunkSize : (localVoxelPos.x % VoxelInfo.ChunkSize),
            localVoxelPos.y < 0 ?  (localVoxelPos.y % VoxelInfo.ChunkSize) + VoxelInfo.ChunkSize : (localVoxelPos.y % VoxelInfo.ChunkSize),
            localVoxelPos.z < 0 ?  (localVoxelPos.z % VoxelInfo.ChunkSize) + VoxelInfo.ChunkSize : (localVoxelPos.z % VoxelInfo.ChunkSize)
        );
    }

    public static Vector3Int ChunkPosToGlobalChunkBaseVoxelPos(Vector3Int chunkPos)
    {
        return new Vector3Int(
            chunkPos.x << VoxelInfo.ChunkSizePowerOfTwo,
            chunkPos.y << VoxelInfo.ChunkSizePowerOfTwo,
            chunkPos.z << VoxelInfo.ChunkSizePowerOfTwo
        );
    }

    public static float GetChunkSqrDistanceToWorldPos(Vector3 worldPos, Vector3Int chunkPos)
    {
        var playerVoxelPos = VoxelPosHelper.WorldPosToGlobalVoxelPos(worldPos);
        var playerChunkPos = VoxelPosHelper.GlobalVoxelPosToChunkPos(playerVoxelPos);
        return (playerChunkPos - chunkPos).sqrMagnitude;
    }
}
