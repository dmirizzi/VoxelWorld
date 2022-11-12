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

    public override void OnChunkBuild(VoxelWorld world, Chunk chunk, Vector3Int globalPos, Vector3Int localPos)
    {
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
        var depth = VoxelInfo.VoxelSize / 16f;

        var placementFaceProp = GetProperty<PlacementFaceProperty>(world, globalPos);
        var dir = placementFaceProp != null ? placementFaceProp.PlacementFace : BlockFace.Back;

        //TODO: Fix texture mapping

        var doorStateProp = GetProperty<DoorStateProperty>(world, globalPos);
        if(doorStateProp != null)
        {
            var tileUVs = VoxelBuildHelper.GetUVsForVoxelType(_voxelType, doorStateProp.IsTopPart ? BlockFace.Top : BlockFace.Bottom);
            var basePos = localPos + new Vector3(size, size, size);
            chunkMesh.AddBox(basePos, new Vector3(size, size, depth), tileUVs, BlockFaceHelper.GetVectorFromBlockFace(dir));
        }
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

        SetProperty<DoorStateProperty>(world, globalPosition, new DoorStateProperty{
            IsTopPart = isTopPart,
            OpenState = DoorOpenState.Closed
        });          
        SetProperty<DoorStateProperty>(world, secondaryDoorPartPos, new DoorStateProperty{
            IsTopPart = !isTopPart,
            OpenState = DoorOpenState.Closed
        });   
    
        // Place secondary part of the door
        world.SetVoxel(
            secondaryDoorPartPos,
            _voxelType,
            placementFace,
            lookDir,
            true);

        //TODO: Blocks around door have to be solid

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

    private readonly ushort _voxelType;
}