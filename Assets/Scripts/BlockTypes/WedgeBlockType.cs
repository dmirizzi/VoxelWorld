using System.Collections.Generic;
using System.Linq;
using UnityEngine;

class WedgeBlockType : IBlockType
{
    public WedgeBlockType(VoxelType voxelType, VoxelType voxelTypeTexture)
    {
        // Specific type of this voxel (e.g. CobblestoneWedge)
        _voxelType = voxelType;

        // The wedge will use the same tile from the texture atlas as the given voxelType as a texture.
        // e.g. could be VoxelType.Cobblestone, then the wedge will have the cobblestone texture.
        _voxelTypeTexture = voxelTypeTexture;
    }

    public bool HasGameObject => false;

    public bool HasCustomVoxelMesh => true;

    public void OnChunkBuild(Chunk chunk, Vector3Int localPosition)
    {
    }

    public void OnChunkVoxelMeshBuild(VoxelWorld world, 
                                      Chunk chunk, 
                                      VoxelType voxelType, 
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

        var placementFace = chunk.GetAuxiliaryData(localVoxelPos);
        var placementDir = Vector3.forward;
        if(placementFace.HasValue)
        {
            placementDir = VoxelFaceHelper.GetVectorFromVoxelFace((VoxelFace)placementFace.Value);
            cornerVertices = VoxelBuildHelper.PointVerticesTowards(cornerVertices, placementDir);
        }

        var basePos = localVoxelPos + new Vector3(size, size, size);
        for(int i = 0; i < cornerVertices.Length; ++i)
        {
            cornerVertices[i] += basePos;
        }

        int vertexBaseIdx = chunkMesh.Vertices.Count;
        var tileUV = VoxelBuildHelper.GetUVsForVoxelType(_voxelTypeTexture, VoxelFace.Bottom);

        if(VoxelBuildHelper.VoxelSideVisible(world, _voxelType, globalVoxelPos, Vector3Int.down))
        {
            chunkMesh.AddQuad(
                new Vector3[] { cornerVertices[0], cornerVertices[1], cornerVertices[2], cornerVertices[3] },
                new Vector2[] { tileUV[2], tileUV[3], tileUV[1], tileUV[0] }
            );
        }
            
        if(VoxelBuildHelper.VoxelSideVisible(world, _voxelType, globalVoxelPos, Vector3Int.FloorToInt(placementDir)))
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

    public bool OnPlace(VoxelWorld world, 
                        Chunk chunk, 
                        Vector3Int globalPosition, 
                        Vector3Int localPosition, 
                        VoxelFace? placementFace)
    {
        // Remember placement direction to build the torch on the right wall
        if(placementFace.HasValue)
        {
            if(placementFace == VoxelFace.Top)
            {
                return false;
            }
            else if(placementFace == VoxelFace.Bottom)
            {
                placementFace = VoxelFace.Back;
            }
            chunk.SetAuxiliaryData(localPosition, (byte)placementFace);
        }

        return true;
    }

    public bool OnRemove(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition)
    {
        return true;
    }

    private VoxelType _voxelType;

    private VoxelType _voxelTypeTexture;
}