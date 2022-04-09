using UnityEngine;

public class TorchBlockType : IBlockType
{
    public TorchBlockType()
    {
        _torchPrefab = (GameObject)Resources.Load("Prefabs/Torch", typeof(GameObject));
    }

    public void OnChunkBuild(Chunk chunk, Vector3Int localPosition)
    {
        var torch = GameObject.Instantiate(_torchPrefab, Vector3.zero, Quaternion.identity);
        chunk.AddBlockGameObject(localPosition, torch);

        // Handle torches that are placed on wall
        var placementFace = chunk.GetAuxiliaryData(localPosition);
        if(placementFace.HasValue)
        {
            var vec = VoxelFaceHelper.GetVectorFromVoxelFace((VoxelFace)placementFace.Value);
            if((VoxelFace)placementFace.Value != VoxelFace.Bottom)
            {
                torch.transform.localPosition += vec * .5f;
                torch.transform.localRotation = Quaternion.Euler(-vec.z * 20f, 0, vec.x * 20f);
            }
        }
    }

    public bool OnPlace(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition, VoxelFace? placementFace)
    {
        if(placementFace.HasValue)
        {
            if(placementFace == VoxelFace.Top)
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
        return true;
    }

    public bool OnRemove(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition)
    {
        return true;        
    }

    private GameObject _torchPrefab;
}