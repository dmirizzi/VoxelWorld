using System.Collections.Generic;
using UnityEngine;

public static class VoxelBuildHelper
{
    // Assuming that initially mesh made up by the given vertices points foward (z+)
    // this method rotates the given vertices towards the given direction
    public static Vector3[] PointVerticesTowards(Vector3[] vertices, Vector3 direction)
    {
        var result = new Vector3[vertices.Length];

        var rotation = Quaternion.FromToRotation(Vector3.forward, direction);
        rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);

        for(int i = 0; i < vertices.Length; ++i)
        {
            result[i] = rotation * vertices[i];
        }

        return result;
    }

    public static bool VoxelSideVisible(VoxelWorld world, VoxelType voxelType, Vector3Int globalVoxelPos, Vector3Int direction)
    {
        var neighbor = world.GetVoxel(globalVoxelPos + direction);

        if(VoxelInfo.IsTransparent(voxelType))
        {
            // Transparent neighboring voxels only hide their shared face if they are of the same type
            return voxelType != neighbor;
        }

        // Solid neighboring voxels hide their shared face
        return !VoxelInfo.IsSolid(neighbor);
    }

    //TODO: Cache calculated UVs -> any significant performance increase?
    //TODO: Either thread-safe dict access or precalculate all uvs before building world
    public static Vector2[] GetUVsForVoxelType(VoxelType voxelType, VoxelFace face)
    {     
        // Shift the UV coordinates by a tiny amount to probe the texture pixel colors away from the border
        // of the pixel rather than at the border to avoid interpolation between atlas tiles
        Vector2 texelOffset = new Vector2(
          .0000001f,
          .0000001f
        );

        var uvOffset = VoxelInfo.GetAtlasUVOffsetForVoxel(voxelType, face) + texelOffset;
        var uvTileSize = new Vector2(
            VoxelInfo.TextureTileSize * 1.0f / VoxelInfo.TextureAtlasWidth - texelOffset.x * 2,
            VoxelInfo.TextureTileSize * 1.0f / VoxelInfo.TextureAtlasHeight + texelOffset.y * 2
        );

        var uvs = new Vector2[]
        {
            uvOffset + new Vector2(0f, 0f),
            uvOffset + new Vector2(uvTileSize.x, 0f),
            uvOffset + new Vector2(0f, -uvTileSize.y),
            uvOffset + new Vector2(uvTileSize.x, -uvTileSize.y)
        };

        return uvs;
    }
}