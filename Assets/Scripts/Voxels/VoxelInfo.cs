using System.Linq;
using UnityEngine;

/*
public enum VoxelType
{
    Empty = 0,
    Grass,
    Dirt,
    Water,
    Cobblestone,
    Torch,
    CobblestoneWedge
}
*/

public struct VoxelFaceData
{
    public VoxelFaceData(BlockFace voxelFace, Vector3Int intDirection, Vector3 floatDirection, int[] vertexIndices, int[] uvIndices)
    {
        VoxelFace = voxelFace;
        Direction = intDirection;
        VertexIndices = vertexIndices;
        UVIndices = uvIndices;
        Normals = Enumerable.Repeat(floatDirection, 4).ToArray();
    }

    public BlockFace VoxelFace;

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

    public static bool IsTransparent(ushort blockType)
    {
        return BlockDataRepository.GetBlockData(blockType).Transparent;
    }

    public static bool IsOpaque(ushort blockType, BlockFace face, int yRotation)
    {
        return BlockDataRepository.GetBlockData(blockType).IsFaceOpaque(face, yRotation);
    }

    public static float GetVoxelHeightOffset(ushort blockType)
    {
        return BlockDataRepository.GetBlockData(blockType).HeightOffset;
    }

    public static Vector2 GetAtlasUVOffsetForVoxel(ushort blockType, BlockFace face)
    {
        var tilePosCoords = BlockDataRepository.GetBlockData(blockType).GetFaceTextureTileCoords(face);

        return new Vector2(
            (float)TextureTileSize / TextureAtlasWidth * tilePosCoords[0],
            (float)TextureTileSize / TextureAtlasHeight * tilePosCoords[1]
        );
    }
    
    public static readonly VoxelFaceData[] VoxelFaceData = new VoxelFaceData[]
    {
        new VoxelFaceData( 
            BlockFace.Bottom, 
            Vector3Int.down, 
            Vector3.down,
            new int[] { 0, 1, 2, 3 },
            new int[] { 0, 1, 3, 2 } 
        ),
        new VoxelFaceData( 
            BlockFace.Top, 
            Vector3Int.up, 
            Vector3.up,
            new int[] { 5, 4, 7, 6 },
            new int[] { 3, 2, 0, 1 } 
        ),
        new VoxelFaceData( 
            BlockFace.Front, 
            Vector3Int.back, 
            Vector3.back,
            new int[] { 0, 4, 5, 1 },
            new int[] { 2, 0, 1, 3 } 
        ),
        new VoxelFaceData( 
            BlockFace.Back, 
            Vector3Int.forward, 
            Vector3.forward,
            new int[] { 3, 2, 6, 7 },
            new int[] { 3, 2, 0, 1 } 
        ),
        new VoxelFaceData( 
            BlockFace.Left, 
            Vector3Int.left, 
            Vector3.left,
            new int[] { 4, 0, 3, 7 },
            new int[] { 1, 3, 2, 0 } 
        ),
        new VoxelFaceData( 
            BlockFace.Right, 
            Vector3Int.right, 
            Vector3.right,
            new int[] { 5, 6, 2, 1 },
            new int[] { 0, 1, 3, 2 } 
        )
    };
}
