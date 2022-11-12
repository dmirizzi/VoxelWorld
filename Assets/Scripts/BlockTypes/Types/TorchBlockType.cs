using UnityEngine;

public class TorchBlockType : BlockTypeBase
{
    public TorchBlockType() : base( new PlacementFaceProperty() )
    {
        _torchPrefab = (GameObject)Resources.Load("Prefabs/Torch", typeof(GameObject));
    }
    
    public override void OnChunkVoxelMeshBuild(VoxelWorld world, Chunk chunk, ushort voxelType, Vector3Int globalVoxelPos, Vector3Int localVoxelPos, ChunkMesh chunkMesh)
    {
    }

    public override void OnChunkBuild(VoxelWorld world, Chunk chunk, Vector3Int globalPos, Vector3Int localPos)
    {
        var torch = GameObject.Instantiate(_torchPrefab, Vector3.zero, Quaternion.identity);
        chunk.AddBlockGameObject(localPos, torch);

        // Handle torches that are placed on wall
        var placement = GetProperty<PlacementFaceProperty>(world, globalPos);
        if(placement != null)
        {
            var vec = BlockFaceHelper.GetVectorFromBlockFace(placement.PlacementFace);
            if(placement.PlacementFace != BlockFace.Bottom)
            {
                torch.transform.localPosition += vec * .5f;
                torch.transform.localRotation = Quaternion.Euler(-vec.z * 20f, 0, vec.x * 20f);
            }
        }
    }

    public override bool OnPlace(VoxelWorld world, Chunk chunk, Vector3Int globalPos, Vector3Int localPos, BlockFace? placementFace, BlockFace? lookDir)
    {
        if(placementFace.HasValue && placementFace == BlockFace.Top)
        {
            // Torch cannot be placed on ceiling
            return false;
        }

        // Remember placement direction to build the torch on the right wall
        if(placementFace.HasValue)
        {
            SetProperty<PlacementFaceProperty>(world, globalPos, new PlacementFaceProperty(placementFace.Value));
        }

        var data = BlockDataRepository.GetBlockData("Torch");
        world.SetLight(globalPos, data.LightColor.Value, true);

        return true;
    }

    public override bool OnRemove(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition)
    {
        var data = BlockDataRepository.GetBlockData("Torch");
        world.SetLight(globalPosition, data.LightColor.Value, false);
        return true;        
    }

    public override BlockFace GetForwardFace(VoxelWorld world, Vector3Int globalPosition)
    {
        return BlockFace.Back;
    }

    private GameObject _torchPrefab;
}