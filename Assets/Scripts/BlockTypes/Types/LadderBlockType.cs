using System.Collections.Generic;
using UnityEngine;

public class LadderBlockType : BlockTypeBase
{
    public LadderBlockType(ushort voxelType, BlockData blockData)
        : base( voxelType,
                blockData,
                new PlacementFaceProperty())
    {
        var meshObj = Resources.Load<GameObject>("Models/Ladder");
        _mesh = new VoxelMesh(meshObj.GetComponentInChildren<MeshFilter>().sharedMesh);
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

    public override void OnChunkVoxelMeshBuild(
        VoxelWorld world, 
        Chunk chunk, 
        ushort voxelType, 
        Vector3Int globalVoxelPos, 
        Vector3Int localVoxelPos, 
        ChunkMesh chunkMesh)
    {
        var placementDirProp = GetProperty<PlacementFaceProperty>(world, globalVoxelPos);
        var backDir = BlockFaceHelper.GetVectorFromBlockFace(placementDirProp.PlacementFace);
        chunkMesh.AddMesh(_mesh.Clone(), localVoxelPos, backDir);
    }

    public override bool OnPlace(
        VoxelWorld world, 
        Vector3Int globalPosition, 
        BlockFace? placementFace, 
        BlockFace? lookDir)
    {
        if(!placementFace.HasValue)
        {
            return false;
        }

        if(placementFace.Value == BlockFace.Top || placementFace.Value == BlockFace.Bottom)
        {
            if(!lookDir.HasValue)
            {
                return false;
            }
            SetProperty<PlacementFaceProperty>(world, globalPosition, new PlacementFaceProperty(lookDir.Value));
        }
        else
        {
            SetProperty<PlacementFaceProperty>(world, globalPosition, new PlacementFaceProperty(placementFace.Value));
        }

        return true;
    }

    public override void OnTouchStart(VoxelWorld world, Vector3Int globalPosition, PlayerController player)
    {
        player.UpdateClimbingCounter(+1);
    }

    public override void OnTouchEnd(VoxelWorld world, Vector3Int globalPosition, PlayerController player)
    {
        player.UpdateClimbingCounter(-1);
    }


    private static readonly HashSet<BlockFace> _allowedPlacementFaces = new HashSet<BlockFace>{
        BlockFace.Back,
        BlockFace.Front,
        BlockFace.Left,
        BlockFace.Right
    };

    private VoxelMesh _mesh;
}