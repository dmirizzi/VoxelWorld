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

    public static Vector3[] RotateVertices(Vector3[] vertices, Quaternion rotation)
    {
        var result = new Vector3[vertices.Length];
        for(int i = 0; i < vertices.Length; ++i)
        {
            result[i] = rotation * vertices[i];
        }
        return result;
    }

    public static Vector3[] TranslateVertices(Vector3[] vertices, Vector3 translation)
    {
        var result = new Vector3[vertices.Length];
        for(int i = 0; i < vertices.Length; ++i)
        {
            result[i] = vertices[i] + translation;
        }
        return result;
    }

    public static bool IsVoxelSideOpaque(VoxelWorld world, ushort voxelType, Vector3Int globalVoxelPos, Vector3Int direction)
    {
        var neighbor = world.GetVoxel(globalVoxelPos + direction);
        var neighborFace = BlockFaceHelper.GetBlockFaceFromVector(-direction);
        if(!neighborFace.HasValue)
        {
            throw new System.ArgumentException($"Direction vector must be a cardinal direction! Instead is {direction}");
        }

        var blockType = BlockTypeRegistry.GetBlockType(neighbor);
        int yRotation = 0;
        if(blockType != null)
        {
            yRotation = BlockFaceHelper.GetYAngleBetweenFaces(
                blockType.GetForwardFace(world, globalVoxelPos + direction),
                BlockFace.Back
            );
        }

        return VoxelInfo.IsOpaque(neighbor, neighborFace.Value, yRotation);        
    }

    public static bool IsVoxelSideVisible(VoxelWorld world, ushort voxelType, Vector3Int globalVoxelPos, Vector3Int direction)
    {        
        var neighbor = world.GetVoxel(globalVoxelPos + direction);

        if(VoxelInfo.IsTransparent(voxelType))
        {
            // Transparent neighboring voxels only hide their shared face if they are of the same type
            return voxelType != neighbor;
        }

        var neighborFace = BlockFaceHelper.GetBlockFaceFromVector(-direction);
        if(!neighborFace.HasValue)
        {
            throw new System.ArgumentException($"Direction vector must be a cardinal direction! Instead is {direction}");
        }

        var blockType = BlockTypeRegistry.GetBlockType(neighbor);
        int yRotation = 0;
        if(blockType != null)
        {
            yRotation = BlockFaceHelper.GetYAngleBetweenFaces(
                blockType.GetForwardFace(world, globalVoxelPos + direction),
                BlockFace.Back
            );
        }

        return !VoxelInfo.IsOpaque(neighbor, neighborFace.Value, yRotation);
    }

    //TODO: Cache calculated UVs -> any significant performance increase?
    //TODO: Either thread-safe dict access or precalculate all uvs before building world
    public static Vector2[] GetUVsForVoxelType(ushort voxelType, BlockFace face)
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