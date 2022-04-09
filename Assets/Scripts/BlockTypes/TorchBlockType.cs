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
    }

    public bool OnPlace(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition)
    {
        return true;
    }

    public bool OnRemove(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition)
    {
        return true;        
    }

    private GameObject _torchPrefab;
}