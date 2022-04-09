using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum VoxelType
{
    Empty = 0,
    Grass,
    Dirt,
    Water,
    Cobblestone,
    Torch
}

public enum VoxelFace
{
    Top = 0,
    Bottom,
    Front,
    Back,
    Left,
    Right
}

public static class VoxelFaceHelper
{
    private static Dictionary<Vector3, VoxelFace> _mapping = new Dictionary<Vector3, VoxelFace>()
    {
        { Vector3.up,       VoxelFace.Top },
        { Vector3.down,     VoxelFace.Bottom },
        { Vector3.back,     VoxelFace.Front },
        { Vector3.forward,  VoxelFace.Back },
        { Vector3.left,     VoxelFace.Left },
        { Vector3.right,    VoxelFace.Right }
    };

    public static VoxelFace GetVoxelFaceFromNormal(Vector3 normal)
    {
        foreach(var vec in _mapping.Keys)
        {
            if(Mathf.Approximately(Vector3.Dot(normal, vec), 1f))
            {
                return _mapping[vec];
            }
        }

        throw new System.ArgumentException($"Invalid voxel face vector: {normal}. Must be cardinal direction.");
    }

    public static Vector3 GetVectorFromVoxelFace(VoxelFace face)
    {
        return _mapping.Single(kv => kv.Value == face).Key;
    }

    public static VoxelFace GetOppositeFace(VoxelFace face)
    {
        int faceInt = (int)face;
        if(faceInt % 2 == 0)
        {
            faceInt++;
        }
        else
        {
            faceInt--;
        }
        return (VoxelFace)faceInt;
    }
}

public struct VoxelFaceData
{
    public VoxelFaceData(VoxelFace voxelFace, Vector3Int intDirection, Vector3 floatDirection, int[] vertexIndices, int[] uvIndices)
    {
        VoxelFace = voxelFace;
        Direction = intDirection;
        VertexIndices = vertexIndices;
        UVIndices = uvIndices;
        Normals = Enumerable.Repeat(floatDirection, 4).ToArray();
    }

    public VoxelFace VoxelFace;

    public Vector3Int Direction;

    public Vector3[] Normals;

    public int[] VertexIndices;

    public int[] UVIndices;
}

public static class VoxelInfo
{
    public const int ChunkSize = 16;

    // Do not change this. Voxels are not scaleable, this is just for code readability.
    public const float VoxelSize = 1f;

    public const int TextureTileSize = 16;

    public const int TextureAtlasWidth = 128;

    public const int TextureAtlasHeight = 128;

    public static bool IsGameObjectBlock(VoxelType voxelType)
    {
        switch(voxelType)
        {
            case VoxelType.Empty:           return false;
            case VoxelType.Grass:           return false;
            case VoxelType.Dirt:            return false;
            case VoxelType.Water:           return false;
            case VoxelType.Cobblestone:     return false;
            case VoxelType.Torch:           return true;

            default: 
                throw new System.ArgumentException($"Invalid voxel type {voxelType}");
        }
    }

    public static bool IsSolid(VoxelType voxelType)
    {
        switch(voxelType)
        {
            case VoxelType.Empty:           return false;
            case VoxelType.Grass:           return true;
            case VoxelType.Dirt:            return true;
            case VoxelType.Water:           return false;
            case VoxelType.Cobblestone:     return true;
            case VoxelType.Torch:           return false;

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
    
    public static readonly VoxelFaceData[] VoxelFaceData = new VoxelFaceData[]
    {
        new VoxelFaceData( 
            VoxelFace.Bottom, 
            Vector3Int.down, 
            Vector3.down,
            new int[] { 0, 1, 2, 3 },
            new int[] { 0, 1, 3, 2 } 
        ),
        new VoxelFaceData( 
            VoxelFace.Top, 
            Vector3Int.up, 
            Vector3.up,
            new int[] { 5, 4, 7, 6 },
            new int[] { 3, 2, 0, 1 } 
        ),
        new VoxelFaceData( 
            VoxelFace.Front, 
            Vector3Int.back, 
            Vector3.back,
            new int[] { 0, 4, 5, 1 },
            new int[] { 2, 0, 1, 3 } 
        ),
        new VoxelFaceData( 
            VoxelFace.Back, 
            Vector3Int.forward, 
            Vector3.forward,
            new int[] { 3, 2, 6, 7 },
            new int[] { 3, 2, 0, 1 } 
        ),
        new VoxelFaceData( 
            VoxelFace.Left, 
            Vector3Int.left, 
            Vector3.left,
            new int[] { 4, 0, 3, 7 },
            new int[] { 1, 3, 2, 0 } 
        ),
        new VoxelFaceData( 
            VoxelFace.Right, 
            Vector3Int.right, 
            Vector3.right,
            new int[] { 5, 6, 2, 1 },
            new int[] { 0, 1, 3, 2 } 
        )
    };
}
