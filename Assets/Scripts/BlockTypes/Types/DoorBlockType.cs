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

        // Remember placement direction to build the torch on the right wall
        if(lookDir.HasValue)
        {
            SetProperty<PlacementFaceProperty>(world, globalPosition, new PlacementFaceProperty(lookDir.Value));
        }

        if(isSecondaryDoorBlock)
        {
            // This is a second door part being placed, first door part already has taken care of everything
            return true;
        }

        bool isTopPart = false;
        if(placementFace.HasValue)
        {
            if(placementFace.Value == BlockFace.Bottom)
            {
                isTopPart = false;
            }
            else if(placementFace.Value == BlockFace.Top)
            {
                isTopPart = true;
            }            
            //TODO: Left/Right
        }

        if(isTopPart)
        {            
            SetProperty<DoorStateProperty>(world, globalPosition, new DoorStateProperty{
                IsTopPart = true,
                OpenState = DoorOpenState.Closed
            });          
            SetProperty<DoorStateProperty>(world, globalPosition + Vector3Int.down, new DoorStateProperty{
                IsTopPart = false,
                OpenState = DoorOpenState.Closed
            });   
       
            world.SetVoxel(
                globalPosition + Vector3Int.down,
                _voxelType,
                placementFace,
                lookDir,
                true);
        }
        else
        {
            SetProperty<DoorStateProperty>(world, globalPosition, new DoorStateProperty{
                IsTopPart = false,
                OpenState = DoorOpenState.Closed
            });          
            SetProperty<DoorStateProperty>(world, globalPosition + Vector3Int.up, new DoorStateProperty{
                IsTopPart = true,
                OpenState = DoorOpenState.Closed
            });          

            world.SetVoxel(
                globalPosition + Vector3Int.up,
                _voxelType,
                placementFace,
                lookDir,
                true);
        }

        //TODO: Find top and bottom position of door based on placement face
        //TODO: Blocks around door have to be solid
        //TODO: Place secondary door block -> how to avoid second one creating a mesh too? Dont go via OnPlace?

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