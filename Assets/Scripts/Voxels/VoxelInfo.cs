using UnityEngine;

public enum VoxelType
{
    Empty = 0,
    Grass = 1,
    Dirt = 2,
    Water = 3,
    Cobblestone = 4
}

public enum VoxelFace
{
    Top,
    Bottom,
    Front,
    Back,
    Left,
    Right
}

public static class VoxelInfo
{
    public const int ChunkSize = 16;

    // Do not change this. Voxels are not scaleable, this is just for code readability.
    public const float VoxelSize = 1f;

    public const int TextureTileSize = 16;

    public const int TextureAtlasWidth = 128;

    public const int TextureAtlasHeight = 128;

    public static bool IsSolid(VoxelType voxelType)
    {
        switch(voxelType)
        {
            case VoxelType.Empty:           return false;
            case VoxelType.Grass:           return true;
            case VoxelType.Dirt:            return true;
            case VoxelType.Water:           return false;
            case VoxelType.Cobblestone:     return true;

            default: 
                throw new System.ArgumentException($"Invalid voxel type {voxelType}");
        }
    }

    public static float GetVoxelHeightOffset(VoxelType voxelType)
    {
        switch(voxelType)
        {
            case VoxelType.Water:       return 0.075f;
            default:                    return 0.0f;
        }
    }

    public static Vector2 GetAtlasUVOffsetForVoxel(VoxelType voxelType, VoxelFace face)
    {
        var tilePosX = 0;
        var tilePosY = 0;

        switch(voxelType)
        {
            case VoxelType.Empty: throw new System.ArgumentException("No texture for empty voxel!");
            case VoxelType.Grass:
                if(face == VoxelFace.Top)
                {
                    tilePosX = 1;
                    tilePosY = 0;
                }
                else if(face == VoxelFace.Bottom)
                {
                    tilePosX = 2;
                    tilePosY = 0;
                }
                else 
                {
                    tilePosX = 0;
                    tilePosY = 0;
                }
            break;
            case VoxelType.Dirt:
                tilePosX = 2;
                tilePosY = 0;
            break;
            case VoxelType.Water:
                tilePosX = 3;
                tilePosY = 0;
            break;
            case VoxelType.Cobblestone:
                tilePosX = 4;
                tilePosY = 0;
            break;
        }

        return new Vector2(
            (float)TextureTileSize / TextureAtlasWidth * tilePosX,
            (float)TextureTileSize / TextureAtlasHeight * tilePosY
        );
    }
}
