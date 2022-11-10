using UnityEngine;

public class TorchBlockType : IBlockType
{
    public TorchBlockType()
    {
        _torchPrefab = (GameObject)Resources.Load("Prefabs/Torch", typeof(GameObject));
    }
    
    public void OnChunkVoxelMeshBuild(VoxelWorld world, Chunk chunk, ushort voxelType, Vector3Int globalVoxelPos, Vector3Int localVoxelPos, ChunkMesh chunkMesh)
    {
    }

    public void OnChunkBuild(Chunk chunk, Vector3Int localPosition)
    {
        var torch = GameObject.Instantiate(_torchPrefab, Vector3.zero, Quaternion.identity);
        chunk.AddBlockGameObject(localPosition, torch);

        // Handle torches that are placed on wall
        var placementFace = chunk.GetAuxiliaryData(localPosition);
        if(placementFace.HasValue)
        {
            var vec = BlockFaceHelper.GetVectorFromBlockFace((BlockFace)placementFace.Value);
            if((BlockFace)placementFace.Value != BlockFace.Bottom)
            {
                torch.transform.localPosition += vec * .5f;
                torch.transform.localRotation = Quaternion.Euler(-vec.z * 20f, 0, vec.x * 20f);
            }
        }
    }

    public bool OnPlace(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition, BlockFace? placementFace, BlockFace? lookDir)
    {
        if(placementFace.HasValue)
        {
            if(placementFace == BlockFace.Top)
            {
                // Torch cannot be placed on ceiling
                return false;
            }
        }

        // Remember placement direction to build the torch on the right wall
        if(placementFace.HasValue)
        {
            chunk.SetAuxiliaryData(localPosition, (byte)placementFace);
        }

        var data = BlockDataRepository.GetBlockData("Torch");
        world.SetLight(globalPosition, data.LightColor.Value, true);

        return true;
    }

    public bool OnRemove(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition)
    {
        var data = BlockDataRepository.GetBlockData("Torch");
        world.SetLight(globalPosition, data.LightColor.Value, false);
        return true;        
    }

    public BlockFace GetForwardFace(VoxelWorld world, Vector3Int globalPosition)
    {
        return BlockFace.Back;
    }

    private GameObject _torchPrefab;
}