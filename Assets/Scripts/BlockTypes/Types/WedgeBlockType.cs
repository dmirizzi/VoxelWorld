using UnityEngine;

class WedgeBlockType : BlockTypeBase
{
    public WedgeBlockType(ushort voxelType, BlockData blockData, ushort voxelTypeTexture)
        : base( 
            voxelType,
            blockData,
            new PlacementFaceProperty() )
    {
        // Specific type of this voxel (e.g. CobblestoneWedge)
        _voxelType = voxelType;

        // The wedge will use the same tile from the texture atlas as the given voxelType as a texture.
        // e.g. could be VoxelType.Cobblestone, then the wedge will have the cobblestone texture.
        _voxelTypeTexture = voxelTypeTexture;
    }

    public override void OnChunkVoxelMeshBuild(
        VoxelWorld world, 
        Chunk chunk, 
        ushort blockType, 
        Vector3Int globalVoxelPos, 
        Vector3Int localVoxelPos, 
        ChunkMesh chunkMesh)
    {
        var size = VoxelInfo.VoxelSize / 2f;

        var cornerVertices = new Vector3[]
        {
            new Vector3(-size, -size, -size),
            new Vector3(+size, -size, -size),
            new Vector3(+size, -size, +size),
            new Vector3(-size, -size, +size),
            new Vector3(+size, +size, +size),
            new Vector3(-size, +size, +size)
        };

        var placementFaceProperty = GetProperty<PlacementFaceProperty>(world, globalVoxelPos);
        var placementDir = BlockFace.Back;
        if(placementFaceProperty != null)
        {
            placementDir = placementFaceProperty.PlacementFace;
            VoxelBuildHelper.PointVerticesTowardsInPlace(cornerVertices, BlockFaceHelper.GetVectorFromBlockFace(placementDir));
        }

        var basePos = localVoxelPos + new Vector3(size, size, size);
        VoxelBuildHelper.TranslateVerticesInPlace(cornerVertices, basePos);

        int vertexBaseIdx = chunkMesh.Vertices.Count;
        var tileUV = VoxelBuildHelper.GetUVsForVoxelType(_voxelTypeTexture, BlockFace.Bottom);

        if(VoxelBuildHelper.IsVoxelSideVisible(world, _voxelType, globalVoxelPos, BlockFace.Bottom))
        {
            chunkMesh.AddQuad(
                new Vector3[] { cornerVertices[0], cornerVertices[1], cornerVertices[2], cornerVertices[3] },
                new Vector2[] { tileUV[2], tileUV[3], tileUV[1], tileUV[0] }
            );
        }
            
        if(VoxelBuildHelper.IsVoxelSideVisible(world, _voxelType, globalVoxelPos, placementDir))
        {
            chunkMesh.AddQuad(
                new Vector3[] { cornerVertices[3], cornerVertices[2], cornerVertices[4], cornerVertices[5] },
                new Vector2[] { tileUV[1], tileUV[0], tileUV[2], tileUV[3] }
            );
        }

        chunkMesh.AddQuad(
            new Vector3[] { cornerVertices[0], cornerVertices[5], cornerVertices[4], cornerVertices[1] },
            new Vector2[] { tileUV[0], tileUV[2], tileUV[3], tileUV[1] }
        );


        if(VoxelBuildHelper.IsVoxelSideVisible(world, _voxelType, globalVoxelPos, BlockFaceHelper.RotateFaceY(placementDir, -90)))
        {
            chunkMesh.AddTriangle(
                new Vector3[] { cornerVertices[5], cornerVertices[0], cornerVertices[3] },
                new Vector2[] { tileUV[2], tileUV[1], tileUV[0] }
            );
        }

        if(VoxelBuildHelper.IsVoxelSideVisible(world, _voxelType, globalVoxelPos, BlockFaceHelper.RotateFaceY(placementDir, 90)))
        {
            chunkMesh.AddTriangle(
                new Vector3[] { cornerVertices[4], cornerVertices[2], cornerVertices[1] },
                new Vector2[] { tileUV[3], tileUV[1], tileUV[0] }
            );
        }
    }

    public override bool OnPlace(
        VoxelWorld world, 
        Vector3Int globalPosition, 
        BlockFace? placementFace,
        BlockFace? lookDir)
    {
        // Remember placement direction to build the wedge on the right wall
        if(placementFace.HasValue)
        {
            if(placementFace == BlockFace.Top)
            {
                return false;
            }
            if(placementFace == BlockFace.Bottom)
            {
                placementFace = lookDir;
            }

            SetProperty<PlacementFaceProperty>(world, globalPosition, new PlacementFaceProperty(placementFace.Value));
        }

        return true;
    }

    public override BlockFace GetForwardFace(VoxelWorld world, Vector3Int globalPos)
    {
        var prop = GetProperty<PlacementFaceProperty>(world, globalPos);
        if(prop != null)
        {
            return prop.PlacementFace;
        }
        return BlockFace.Back;
    }

    private ushort _voxelType;

    private ushort _voxelTypeTexture;
}