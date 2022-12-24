using UnityEngine;

public class NpcSpawnController : MonoBehaviour
{
    public GameObject BlobPrefab;

    private VoxelWorld _voxelWorld;

    private WorldGenerator _worldGen;

    public int TargetNumberBlobs { get; set; }

    public int NumBlobs { get; private set; }

    void Start()
    {
        _voxelWorld = GameObject.FindObjectOfType<VoxelWorld>();
        _worldGen = GameObject.FindObjectOfType<WorldGenerator>();
    }

    Vector3Int GetRandomSolidSurfaceVoxel()
    {
        var bounds = _voxelWorld.GetWorldBoundaries();

        while(true)
        {
            var x = Random.Range(bounds.Item1.x, bounds.Item2.x);
            var z = Random.Range(bounds.Item1.z, bounds.Item2.z);
            var y = _voxelWorld.GetHighestVoxelPos(x, z);
            if(y.HasValue)
            {
                return new Vector3Int(x, y.Value, z);
            }
        }
    }

    void Update()
    {
        if(NumBlobs < TargetNumberBlobs && _worldGen.WorldGenerated)
        {
            var pos = VoxelPosHelper.GetVoxelTopCenterSurfaceWorldPos(GetRandomSolidSurfaceVoxel());
            pos += Vector3.up * (BlobPrefab.GetComponent<Renderer>().bounds.size.y / 2);
            Instantiate(BlobPrefab, pos, new Quaternion());
            NumBlobs++;
        }
        
    }
}
