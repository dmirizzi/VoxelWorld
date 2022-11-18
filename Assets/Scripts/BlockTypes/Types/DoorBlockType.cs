using System;
using UnityEngine;

public class DoorBlockType : BlockTypeBase
{
    public DoorBlockType(ushort voxelType)
        : base( new PlacementFaceProperty(),
                new DoorStateProperty() )
    {
        this._voxelType = voxelType;
    }

    public override BlockFace GetForwardFace(VoxelWorld world, Vector3Int globalPos)
    {
        var placementFaceProp = GetProperty<PlacementFaceProperty>(world, globalPos);
        if(placementFaceProp != null)
        {
            return placementFaceProp.PlacementFace;
        }
        return BlockFace.Back;
    }

    public override void OnChunkVoxelMeshBuild(
        VoxelWorld world, 
        Chunk chunk, 
        ushort voxelType, 
        Vector3Int globalPos,
        Vector3Int localPos, 
        ChunkMesh chunkMesh)
    {
        var size = VoxelInfo.VoxelSize / 2f;
        var depth = VoxelInfo.VoxelSize / 32f;

        //TODO: Fix texture mapping

        var doorStateProp = GetProperty<DoorStateProperty>(world, globalPos);
        if(doorStateProp == null)        
        {
            return;
        }

        var doorSize = new Vector3(size, size, depth);
        var cornerVertices = new Vector3[]
        {
            new Vector3(-doorSize.x, -doorSize.y, -doorSize.z),
            new Vector3(+doorSize.x, -doorSize.y, -doorSize.z),
            new Vector3(+doorSize.x, -doorSize.y, +doorSize.z),
            new Vector3(-doorSize.x, -doorSize.y, +doorSize.z),
            new Vector3(-doorSize.x, +doorSize.y, -doorSize.z),
            new Vector3(+doorSize.x, +doorSize.y, -doorSize.z),
            new Vector3(+doorSize.x, +doorSize.y, +doorSize.z),
            new Vector3(-doorSize.x, +doorSize.y, +doorSize.z)
        };

        // Rotate door if it's open
        if(doorStateProp.IsOpen)
        {
            VoxelBuildHelper.RotateVerticesAroundInPlace(cornerVertices, Vector3.left, Vector3.up, -90);
            VoxelBuildHelper.TranslateVerticesInPlace(cornerVertices, Vector3.right * (size + depth) + Vector3.back * (size + depth));
        }

        // Rotate door according to placement direction
        var placementFaceProp = GetProperty<PlacementFaceProperty>(world, globalPos);
        var placementDir = placementFaceProp != null ? placementFaceProp.PlacementFace : BlockFace.Back;
        var doorBackVec =  BlockFaceHelper.GetVectorFromBlockFace(placementDir);
        VoxelBuildHelper.PointVerticesTowardsInPlace(cornerVertices, doorBackVec);

        var basePos = localPos + new Vector3(size, size, size) - doorBackVec * (size - depth);
        VoxelBuildHelper.TranslateVerticesInPlace(cornerVertices, basePos);

        var tileUVs = VoxelBuildHelper.GetUVsForVoxelType(_voxelType, doorStateProp.IsTopPart ? BlockFace.Top : BlockFace.Bottom);

        // Top
        chunkMesh.AddQuad(
            new Vector3[] { cornerVertices[4], cornerVertices[7], cornerVertices[6], cornerVertices[5] },
            new Vector2[] { tileUVs[2], tileUVs[0], tileUVs[1], tileUVs[3] }
        );

        // Bottom
        chunkMesh.AddQuad(
            new Vector3[] { cornerVertices[0], cornerVertices[1], cornerVertices[2], cornerVertices[3] },
            new Vector2[] { tileUVs[0], tileUVs[1], tileUVs[3], tileUVs[2] }            
        );

        // Front
        chunkMesh.AddQuad(
            new Vector3[] { cornerVertices[0], cornerVertices[4], cornerVertices[5], cornerVertices[1] },
            new Vector2[] { tileUVs[2], tileUVs[0], tileUVs[1], tileUVs[3] }
        );

        // Back
        chunkMesh.AddQuad(
            new Vector3[] { cornerVertices[3], cornerVertices[2], cornerVertices[6], cornerVertices[7] },
            new Vector2[] { tileUVs[3], tileUVs[2], tileUVs[0], tileUVs[1] }
        );

        // Left
        chunkMesh.AddQuad(
            new Vector3[] { cornerVertices[7], cornerVertices[4], cornerVertices[0], cornerVertices[3] },
            new Vector2[] { tileUVs[0], tileUVs[1], tileUVs[2], tileUVs[3] }
        );

        // Right
        chunkMesh.AddQuad(
            new Vector3[] { cornerVertices[5], cornerVertices[6], cornerVertices[2], cornerVertices[1] },
            new Vector2[] { tileUVs[0], tileUVs[1], tileUVs[3], tileUVs[2] }
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
        var isSecondaryDoorBlock = GetProperty<DoorStateProperty>(world, globalPosition) != null;

        if(lookDir.HasValue)
        {
            SetProperty<PlacementFaceProperty>(world, globalPosition, new PlacementFaceProperty(lookDir.Value));
        }

        if(isSecondaryDoorBlock)
        {
            // This is a second door part being placed, first door part already has taken care of everything
            return true;
        }

        // Door can be placed looking at the floor or the ceiling or at sides of voxels
        bool isTopPart = false;
        Vector3Int secondaryDoorPartPos = Vector3Int.zero;
        if(placementFace.HasValue && lookDir.HasValue)
        {
            var placementFaceRelativeToView = BlockFaceHelper.RotateFaceY(placementFace.Value, lookDir.Value);
            if(placementFaceRelativeToView == BlockFace.Bottom)
            {
                isTopPart = false;
                secondaryDoorPartPos = globalPosition + Vector3Int.up;
            }
            else if(placementFaceRelativeToView == BlockFace.Top)
            {
                isTopPart = true;
                secondaryDoorPartPos = globalPosition + Vector3Int.down;
            }           
            else if(placementFaceRelativeToView == BlockFace.Left || placementFaceRelativeToView == BlockFace.Right) 
            {
                if(world.GetVoxel(globalPosition + Vector3Int.down) != 0)
                {
                    isTopPart = false;
                    secondaryDoorPartPos = globalPosition + Vector3Int.up;
                }
                else if(world.GetVoxel(globalPosition + Vector3Int.down * 2) != 0)
                {
                    isTopPart = true;
                    secondaryDoorPartPos = globalPosition + Vector3Int.down;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        // Door must be placed on solid ground  
        Vector3Int bottomPartPos = !isTopPart ? globalPosition : secondaryDoorPartPos;
        if(world.GetVoxel(bottomPartPos + Vector3Int.down) == 0)
        {          
            return false;
        }

        // Voxel where secondary door part will be placed must be empty
        if(world.GetVoxel(secondaryDoorPartPos) != 0)
        {
            return false;
        }
        
        // There must be solid blocks to the left of the door (i.e. where the hinges are)
        var hingeSide = BlockFaceHelper.GetVectorIntFromBlockFace(BlockFaceHelper.RotateFaceY(BlockFace.Left, lookDir.Value));
        if(world.GetVoxel(globalPosition + hingeSide) == 0 || world.GetVoxel(secondaryDoorPartPos + hingeSide) == 0)
        {
            return false;
        }

        SetProperty<DoorStateProperty>(world, globalPosition, new DoorStateProperty{
            IsTopPart = isTopPart,
            IsOpen = false
        });          
        SetProperty<DoorStateProperty>(world, secondaryDoorPartPos, new DoorStateProperty{
            IsTopPart = !isTopPart,
            IsOpen = false
        });

        // Place secondary part of the door
        world.SetVoxel(
            secondaryDoorPartPos,
            _voxelType,
            placementFace,
            lookDir,
            true);

        return true;
    }

    public override bool OnRemove(
        VoxelWorld world, 
        Chunk chunk, 
        Vector3Int globalPosition, 
        Vector3Int localPosition)
    {
        var doorStateProp = GetProperty<DoorStateProperty>(world, globalPosition);
        if(doorStateProp != null)
        {
            if(doorStateProp.IsTopPart)
            {
                var bottomPartPos = globalPosition + Vector3Int.down;
                world.ClearVoxelAuxiliaryData(bottomPartPos);
                world.SetVoxel(bottomPartPos, 0);
            }
            else
            {
                var topPartPos = globalPosition + Vector3Int.up;
                world.ClearVoxelAuxiliaryData(topPartPos);
                world.SetVoxel(topPartPos, 0);
            }
        }
        return true;
    }

    public override bool OnUse(VoxelWorld world, Vector3Int globalPosition, BlockFace lookDir)
    {
        var secondaryDoorPartPos = GetProperty<DoorStateProperty>(world, globalPosition).IsTopPart 
            ? globalPosition + Vector3Int.down
            : globalPosition + Vector3Int.up;

        Func<DoorStateProperty, DoorStateProperty> switchDoorStateFunc = prop => 
        {
            prop.IsOpen = !prop.IsOpen;
            return prop;
        };

        UpdateProperty<DoorStateProperty>(world, globalPosition, switchDoorStateFunc);
        UpdateProperty<DoorStateProperty>(world, secondaryDoorPartPos, switchDoorStateFunc);

        world.RebuildVoxel(globalPosition);
        return true;
    }

    private readonly ushort _voxelType;
}