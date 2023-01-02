using System;
using UnityEngine;

public class DoorBlockType : BlockTypeBase
{
    public DoorBlockType(ushort voxelType)
        : base( new PlacementFaceProperty(),
                new DoorStateProperty() )
    {
        this._voxelType = voxelType;

        //TODO: Abstract Base class method: LoadVoxelMesh
        var topMeshObj = Resources.Load<GameObject>("Models/Door_Top");
        _topMesh = new VoxelMesh(topMeshObj.GetComponentInChildren<MeshFilter>().sharedMesh);
        var bottomMeshObj = Resources.Load<GameObject>("Models/Door_Bottom");
        _bottomMesh = new VoxelMesh(bottomMeshObj.GetComponentInChildren<MeshFilter>().sharedMesh);
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
        var doorStateProp = GetProperty<DoorStateProperty>(world, globalPos);
        if(doorStateProp == null)        
        {
            return;
        }

        var placementDirProp = GetProperty<PlacementFaceProperty>(world, globalPos);
        var doorMesh = doorStateProp.IsTopPart ? _topMesh.Clone() : _bottomMesh.Clone();

        // Rotate door if it's open
        if(doorStateProp.IsOpen)
        {
            var size = VoxelInfo.VoxelSize / 2f;
            var depth = VoxelInfo.VoxelSize / 32f;

            doorMesh.RotateAround(Vector3.left, Vector3.up, -90);
            doorMesh.Translate(Vector3.right * (size + depth) + Vector3.back * (size + depth));
        }
        
        var backDir = BlockFaceHelper.GetVectorFromBlockFace(placementDirProp.PlacementFace);
        chunkMesh.AddMesh(doorMesh, localPos, backDir);

        chunk.AddVoxelCollider(localPos, doorMesh.CalculateBounds());
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
        // Automatically delete the second door part
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

        world.QueueVoxelForRebuild(globalPosition);
        world.QueueVoxelForRebuild(secondaryDoorPartPos);
        return true;
    }

    private readonly ushort _voxelType;

    private VoxelMesh _topMesh;

    private VoxelMesh _bottomMesh;

}