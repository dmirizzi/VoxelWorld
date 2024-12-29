using System;
using System.Collections.Generic;
using System.Linq;
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

    public static void PointVerticesTowardsInPlace(Vector3[] vertices, Vector3 direction)
    {
        var rotation = Quaternion.FromToRotation(Vector3.forward, direction);
        rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);

        for(int i = 0; i < vertices.Length; ++i)
        {
            vertices[i] = rotation * vertices[i];
        }
    }

    public static void RotateVertices(Vector3[] vertices, Quaternion rotation)
    {
        for(int i = 0; i < vertices.Length; ++i)
        {
            vertices[i] = rotation * vertices[i];
        }
    }

    public static void RotateVerticesAroundInPlace(Vector3[] vertices, Vector3 pivotPoint, Vector3 axis, float angle)
    {
        var rotation = Quaternion.AngleAxis(angle, axis);

        for(int i = 0; i < vertices.Length; ++i)
        {
            vertices[i] = rotation * (vertices[i] - pivotPoint) + pivotPoint;
        }
    }

    public static void TranslateVerticesInPlace(Vector3[] vertices, Vector3 translation)
    {
        for(int i = 0; i < vertices.Length; ++i)
        {
            vertices[i] = vertices[i] + translation;
        }
    }

    public static Vector3[] TranslateVertices(Vector3[] vertices, Vector3 translation)
    {
        var result = new Vector3[vertices.Length];
        int i = 0;

        foreach(var vertex in vertices)
        {
            result[i++] = vertex + translation;
        }

        return result;
    }

    public static bool NeighborVoxelHasOpaqueSide(VoxelWorld world, Vector3Int globalVoxelPos, BlockFace direction, Vector3Int directionVec)
    {
        var globalNeighborPos = globalVoxelPos + directionVec;
        var neighbor = world.GetVoxel(globalNeighborPos);
        var neighborFace = BlockFaceHelper.GetOppositeFace(direction);

        var blockType = BlockDataRepository.GetBlockType(neighbor);
        int yRotation = 0;
        if(blockType != null)
        {
            yRotation = BlockFaceHelper.GetYAngleBetweenFaces(
                blockType.GetForwardFace(world, globalNeighborPos),
                BlockFace.Back
            );
        }

        return VoxelInfo.IsOpaque(neighbor, neighborFace, yRotation);        
    }

    public static bool IsVoxelSideVisible(VoxelWorld world, ushort voxelType, Vector3Int globalVoxelPos, BlockFace direction)
    {       
        var dirVector = BlockFaceHelper.GetVectorIntFromBlockFace(direction);
        var neighbor = world.GetVoxel(globalVoxelPos + dirVector);

        if(VoxelInfo.IsTransparent(voxelType))
        {
            // Transparent neighboring voxels only hide their shared face if they are of the same type
            return voxelType != neighbor;
        }

        var blockType = BlockDataRepository.GetBlockType(neighbor);
        int yRotation = 0;
        if(blockType != null)
        {
            yRotation = BlockFaceHelper.GetYAngleBetweenFaces(
                blockType.GetForwardFace(world, globalVoxelPos + dirVector),
                BlockFace.Back
            );
        }

        var neighborFace = BlockFaceHelper.GetOppositeFace(direction);
        return !VoxelInfo.IsOpaque(neighbor, neighborFace, yRotation);
    }

    public static void BuildVoxelUVCache()
    {
        _voxelUVCache = new Dictionary<(ushort, BlockFace), Vector2[]>();

        var faces = Enum.GetValues(typeof(BlockFace)).Cast<BlockFace>();

        var blockData = BlockDataRepository.GetAllBlockData();
        for(ushort voxelType = 1; voxelType < blockData.Count; ++voxelType)
        {
            foreach(var face in faces)
            {
                if(TryCalcUVsForVoxel(voxelType, face, out var uvs))
                {
                    _voxelUVCache[(voxelType, face)] = uvs;
                }
            }
        }
    }

    //TODO: Cache calculated UVs -> any significant performance increase?
    //TODO: Either thread-safe dict access or precalculate all uvs before building world
    public static Vector2[] GetUVsForVoxelType(ushort voxelType, BlockFace face)
    {     
        return _voxelUVCache[(voxelType, face)];
    }

    public static bool TryCalcUVsForVoxel(ushort voxelType, BlockFace face, out Vector2[] uvs)
    {     
        // Shift the UV coordinates by a tiny amount to probe the texture pixel colors away from the border
        // of the pixel rather than at the border to avoid interpolation between atlas tiles
        Vector2 texelOffset = new Vector2(
          .0000001f,
          .0000001f
        );
    
        if(VoxelInfo.TryGetAtlasUVOffsetForVoxel(voxelType, face, out var uvOffset))
        {
            uvOffset += texelOffset;
            var uvTileSize = new Vector2(
                VoxelInfo.TextureTileSize * 1.0f / VoxelInfo.TextureAtlasWidth - texelOffset.x * 2,
                VoxelInfo.TextureTileSize * 1.0f / VoxelInfo.TextureAtlasHeight + texelOffset.y * 2
            );

            uvs = new Vector2[]
            {
                uvOffset + new Vector2(0f, 0f),
                uvOffset + new Vector2(uvTileSize.x, 0f),
                uvOffset + new Vector2(0f, -uvTileSize.y),
                uvOffset + new Vector2(uvTileSize.x, -uvTileSize.y)
            };

            return true;
        }

        uvs = null;
        return false;
    }    

    private static Dictionary<(ushort, BlockFace), Vector2[]> _voxelUVCache;
}