using System;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    public Material TextureAtlasMaterial;

    public Material TextureAtlasTransparentMaterial;

    private VoxelWorld _world;

    // Start is called before the first frame update
    void Start()
    {
        _world = new VoxelWorld(TextureAtlasMaterial, TextureAtlasTransparentMaterial);

        var seed = UnityEngine.Random.Range(0, 1000);

        for(int x = 0; x < 128; ++x)
        {
            for(int z = 0; z < 128; ++z)
            {
                var height = Mathf.Min(0, (int)(Mathf.PerlinNoise(seed + x / 20.0f, seed + z / 20.0f) * 16) - 3);

                bool isWater = height < 0;
                if(isWater) height = 0;

                for(int y = -64; y <= height; ++y)
                {
                    if(isWater)
                    {
                        if(y == -64)
                        {
                            _world.SetVoxel(x, y, z, VoxelType.Dirt);
                        }
                        else
                        {
                            _world.SetVoxel(x, y, z, VoxelType.Water);
                        }
                    }
                    else
                    {
                        if(y < height)
                        {
                            _world.SetVoxel(x, y, z, VoxelType.Dirt);
                        }
                        else
                        {
                            _world.SetVoxel(x, y, z, VoxelType.Grass);
                        }
                    }                    
                }
            }
        }
        var sw = new Stopwatch();
        sw.Start();
        _world.Build();
        sw.Stop();
        UnityEngine.Debug.Log($"Built world in {sw.Elapsed.TotalSeconds}s");
    }

    private DateTime lastDrop = DateTime.Now;

    // Update is called once per frame
    void Update()
    {
        /*
        if((DateTime.Now - lastDrop).Milliseconds >= 10)
        {
            lastDrop = DateTime.Now;
            var x = UnityEngine.Random.Range(0, 128);
            var z = UnityEngine.Random.Range(0, 128);
            
            var highestPoint = _world.GetHighestPoint(x, z);
            if(highestPoint.HasValue)
            {
                //_world.SetVoxel(x, highestPoint.Value, z, VoxelType.Empty, true);
                _world.SetVoxelSphere(new Vector3Int(x, highestPoint.Value, z), 5, VoxelType.Empty, true);
            }
        }
        */
    }
}
