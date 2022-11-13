using UnityEngine;

class WedgeBlockType : BlockTypeBase
{
    public WedgeBlockType(ushort voxelType, ushort voxelTypeTexture)
        : base( new PlacementFaceProperty() )
    {
        // Specific type of this voxel (e.g. CobblestoneWedge)
        _voxelType = voxelType;

        // The wedge will use the same tile from the texture atlas as the given voxelType as a texture.
        // e.g. could be VoxelType.Cobblestone, then the wedge will have the cobblestone texture.
        _voxelTypeTexture = voxelTypeTexture;
    }

    public override void OnChunkBuild(VoxelWorld world, Chunk chunk, Vector3Int globalPos, Vector3Int localPos)
    {
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

        var placementFace = GetProperty<PlacementFaceProperty>(world, globalVoxelPos);
        var placementDir = Vector3.forward;
        if(placementFace != null)
        {
            placementDir = BlockFaceHelper.GetVectorFromBlockFace(placementFace.PlacementFace);
            cornerVertices = VoxelBuildHelper.PointVerticesTowards(cornerVertices, placementDir);
        }

        var basePos = localVoxelPos + new Vector3(size, size, size);
        for(int i = 0; i < cornerVertices.Length; ++i)
        {
            cornerVertices[i] += basePos;
        }

        int vertexBaseIdx = chunkMesh.Vertices.Count;
        var tileUV = VoxelBuildHelper.GetUVsForVoxelType(_voxelTypeTexture, BlockFace.Bottom);

        if(VoxelBuildHelper.IsVoxelSideVisible(world, _voxelType, globalVoxelPos, Vector3Int.down))
        {
            chunkMesh.AddQuad(
                new Vector3[] { cornerVertices[0], cornerVertices[1], cornerVertices[2], cornerVertices[3] },
                new Vector2[] { tileUV[2], tileUV[3], tileUV[1], tileUV[0] }
            );
        }
            
        if(VoxelBuildHelper.IsVoxelSideVisible(world, _voxelType, globalVoxelPos, Vector3Int.FloorToInt(placementDir)))
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

        chunkMesh.AddTriangle(
            new Vector3[] { cornerVertices[5], cornerVertices[0], cornerVertices[3] },
            new Vector2[] { tileUV[2], tileUV[1], tileUV[0] }
        );

        chunkMesh.AddTriangle(
            new Vector3[] { cornerVertices[4], cornerVertices[2], cornerVertices[1] },
            new Vector2[] { tileUV[3], tileUV[1], tileUV[0] }
        );
    }

    public override bool OnPlace(
        VoxelWorld world, 
        Chunk chunk, 
        Vector3Int globalPosition, 
        Vector3Int localPosition, 
        BlockFace? placementFace,
        BlockFace? lookDir)
    {
        // Remember placement direction to build the torch on the right wall
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

    public override bool OnRemove(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition)
    {
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

    public override bool OnUse(VoxelWorld world, Vector3Int globalPosition, BlockFace lookDir)
    {
        return true;
    }

    private ushort _voxelType;

    private ushort _voxelTypeTexture;
}