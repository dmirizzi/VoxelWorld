using System;
using System.Diagnostics;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    public Material TextureAtlasMaterial;

    public Material TextureAtlasTransparentMaterial;

    public bool WorldGenerated { get; private set; }

    public VoxelWorld VoxelWorld { get; private set; }

    // Start is called before the first frame update
    void Start()
    {
        VoxelWorld = new VoxelWorld(TextureAtlasMaterial, TextureAtlasTransparentMaterial);

        var seed = UnityEngine.Random.Range(0, 1000);

        for(int x = 0; x < 64; ++x)
        {
            for(int z = 0; z < 64; ++z)
            {
                var height = Mathf.Min(16, (int)(Mathf.PerlinNoise(seed + x / 20.0f, seed + z / 20.0f) * 32) - 3);

                bool isWater = height < 0;
                if(isWater) height = 0;

                for(int y = -64; y <= height; ++y)
                {
                    if(isWater)
                    {
                        if(y == -64)
                        {
                            VoxelWorld.SetVoxel(x, y, z, VoxelType.Dirt);
                        }
                        else
                        {
                            VoxelWorld.SetVoxel(x, y, z, VoxelType.Water);
                        }
                    }
                    else
                    {
                        if(y < height)
                        {
                            VoxelWorld.SetVoxel(x, y, z, VoxelType.Dirt);
                        }
                        else
                        {
                            VoxelWorld.SetVoxel(x, y, z, VoxelType.Grass);
                        }
                    }                    
                }
            }
        }
        var sw = new Stopwatch();
        sw.Start();
        VoxelWorld.Build();
        sw.Stop();
        UnityEngine.Debug.Log($"Built world in {sw.Elapsed.TotalSeconds}s");

        WorldGenerated = true;
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
