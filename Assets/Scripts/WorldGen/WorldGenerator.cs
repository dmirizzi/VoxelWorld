using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    public Material TextureAtlasMaterial;

    public Material TextureAtlasTransparentMaterial;

    public GameObject TorchPrefab;

    public bool WorldGenerated { get; private set; }

    public VoxelWorld VoxelWorld { get; private set; }

    void Start()
    {
        VoxelWorld = new VoxelWorld(TextureAtlasMaterial, TextureAtlasTransparentMaterial);

        GenerateWorld();

        WorldGenerated = true;
    }

    private void GenerateWorld()
    {
        var sw = new Stopwatch();
        sw.Start();

        VoxelWorld.Clear();
        /*
                var size = 8;
                for(int x = -size; x < size; ++x)
                {
                    for(int z = -size; z < size; ++z)
                    {
                        for(int y = 0; y <= 0; ++y)
                        {
                            VoxelWorld.SetVoxel(x, y, z, BlockDataRepository.GetBlockTypeId("Dirt"));
                        }
                    }
                }
        */
        //VoxelWorld.AddLight(new Vector3Int(0, 1, 0), new Color32(255, 78, 203, 255), 20);
        //VoxelWorld.AddLight(new Vector3Int(30, 2, 0), new Color32(50, 255, 50, 255), 20);
        //VoxelWorld.AddLight(new Vector3Int(15, 2, -10), new Color32(255, 255, 0, 255), 20);


        GenerateTerrain(128);

        /*
                GenerateCave(
                    new Vector3Int(0, 0, 0),
                    new Vector3Int(
                        UnityEngine.Random.Range(32, 128),
                        UnityEngine.Random.Range(32, 128), 
                        UnityEngine.Random.Range(32, 128)
                    ),
                    _iterations,
                    _birthNeighbors,
                    _deathNeighbors,
                    _emptyChance
                );       
        */

        GenerateTorches(32);

        VoxelWorld.BuildChangedChunks();
        PlacePlayer();

        sw.Stop();
        UnityEngine.Debug.Log($"Generated world in {sw.Elapsed.TotalSeconds} sec");
    }

    private void GenerateTorches(int numTorches)
    {
        for (int i = 0; i < numTorches; ++i)
        {
            var worldPos = VoxelWorld.GetRandomSolidSurfaceVoxel() + Vector3Int.up;
            VoxelWorld.SetVoxel(worldPos, BlockDataRepository.GetBlockTypeId("Torch"));
        }
    }

    private void GenerateTerrain(int size)
    {
        size /= 2;

        var seed = UnityEngine.Random.Range(0, 1000);

        for(int x = -size; x < size; ++x)
        {
            for(int z = -size; z < size; ++z)
            {
                var height = Mathf.Min(64, (int)(Mathf.PerlinNoise(seed + x / 20.0f, seed + z / 20.0f) * 32) - 5);
                //var height = 8;

                bool isWater = height < 0;
                if(isWater) height = 0;

                for(int y = -64; y <= height; ++y)
                {
                    if(y == -64)
                    {
                        VoxelWorld.SetVoxel(x, y, z, BlockDataRepository.GetBlockTypeId("Cobblestone"));
                    }
                    else if(isWater)
                    {

                        VoxelWorld.SetVoxel(x, y, z, BlockDataRepository.GetBlockTypeId("Water"));
                    }
                    else
                    {
                        if(y < height)
                        {
                            VoxelWorld.SetVoxel(x, y, z, BlockDataRepository.GetBlockTypeId("Dirt"));
                        }
                        else
                        {
                            VoxelWorld.SetVoxel(x, y, z, BlockDataRepository.GetBlockTypeId("Grass"));
                        }
                    }                    
                }
            }
        }
    }

    private void GenerateCave(Vector3Int position, Vector3Int size, int iterations, int birthNeighbors, int deathNeighbors, float emptyChance)
    {
        if(size.x == 0 || size.y == 0 || size.z == 0)
        {
            return;
        }

        bool[,,] cells = new bool[size.x, size.y, size.z];

        Func<bool[,,], int, int, int, int> getNeighbors = (cells, x, y, z) => 
        {
            int neighbors = 0;
            for(int dx = x - 1; dx <= x + 1; ++dx)
            {
                for(int dy = y - 1; dy <= y + 1; ++dy)
                {
                    for(int dz = z - 1; dz <= z + 1; ++dz)
                    {
                        if(dx >= 0 && dx < size.x && dy >= 0 && dy < size.y && dz >= 0 && dz < size.z)
                        {
                            if(dx != x || dy != y || dz != z)
                            {
                                if(cells[dx, dy, dz])
                                {
                                    neighbors++;
                                }
                            }
                        }
                    }
                }
            }
            return neighbors;
        };

        // Randomize cave area
        for(int x = 0; x < size.x; ++x)
        {
            for(int y = 0; y < size.y; ++y)
            {
                for(int z = 0; z < size.z; ++z)
                {
                    if(UnityEngine.Random.Range(0f, 1f) <= emptyChance)
                    {
                        cells[x, y, z] = false;
                    }
                    else
                    {
                        cells[x, y, z] = true;
                    }
                }                
            }
        }

        // Run cellular automata
        var oldCells = new bool[size.x, size.y, size.z];
        for(int i = 0; i < iterations; ++i)
        {
            Buffer.BlockCopy(cells, 0, oldCells, 0, size.x * size.y * size.z * sizeof(bool));

            for(int x = 0; x < size.x; ++x)
            {
                for(int y = 0; y < size.y; ++y)
                {
                    for(int z = 0; z < size.z; ++z)
                    {
                        var neighbors = getNeighbors(oldCells, x, y, z);

                        if(!cells[x, y, z])
                        {
                            if(neighbors >= birthNeighbors)
                            {
                                cells[x, y, z] = true; 
                            }
                        }
                        else
                        {
                            if(neighbors <= deathNeighbors)
                            {
                                cells[x, y, z] = false;
                            }
                        }
                    }                
                }
            }
        }

        for(int x = 0; x < size.x; ++x)
        {
            for(int y = 0; y < size.y; ++y)
            {
                for(int z = 0; z < size.z; ++z)
                {
                    if(cells[x, y, z])
                    {
                        VoxelWorld.SetVoxel(
                            position.x + x - size.x / 2, 
                            position.y - y, 
                            position.z + z - size.z / 2, 
                            0 );
                    }
                }                
            }
        }
    }

    private (int, bool[,,]) FloodFill(bool[,,] cells, Vector3Int size, Vector3Int point)
    {
        bool[,,] output = new bool[size.x, size.y, size.z];

        var stack = new Stack<Vector3Int>();
        stack.Push(point);

        int numCells = 0;

        while(stack.Count > 0)
        {
            var currentPoint = stack.Pop();
            if(!cells[currentPoint.x, currentPoint.y, currentPoint.z] && !output[currentPoint.x, currentPoint.y, currentPoint.z])
            {
                output[currentPoint.x, currentPoint.y, currentPoint.z] = true;
                numCells++;

                for(int x = currentPoint.x - 1; x < currentPoint.x + 1; ++x)
                {
                    for(int y = currentPoint.y - 1; y < currentPoint.y + 1; ++y)
                    {
                        for(int z = currentPoint.z - 1; z < currentPoint.z + 1; ++z)
                        {
                            if(x >= 0 && x < size.x && y >= 0 && y < size.y && z >= 0 && z < size.z)
                            {
                                if(x != currentPoint.x || y != currentPoint.y || z != currentPoint.z)
                                {
                                    stack.Push(new Vector3Int(x, y, z));
                                }
                            }    
                        }            
                    }
                }
            }
        }

        return (numCells, output);
    }

    private void PlacePlayer()
    {
        var pos = VoxelWorld.GetRandomSolidSurfaceVoxel();
        var worldPos = VoxelPosHelper.GetVoxelTopCenterSurfaceWorldPos(pos) + Vector3.up;

        var characterController = GameObject.Find("Player").GetComponent<CharacterController>();
        characterController.enabled = false;
        characterController.transform.position = worldPos + Vector3.up * 10;
        characterController.enabled = true;
/*
        var playerController = GameObject.Find("Player").GetComponent<PlayerController>();
        var torch = Instantiate(TorchPrefab, worldPos, Quaternion.identity);
        var playerHoldingController = GameObject.Find("Player").GetComponent<PlayerHoldingController>();
        playerHoldingController.HoldObject(torch);        
*/
        UnityEngine.Debug.Log($"Placing player @ {GameObject.Find("Player").transform.position}");
    }

    private DateTime lastDrop = DateTime.Now;

    private int _birthNeighbors = 13;
    private int _deathNeighbors = 12;
    private int _iterations = 30;
    private float _emptyChance = .54f;

    void OnGUI()
    {
/*
        GUI.Label(new Rect(10, 10, 140, 50), $"BirthNeighbors({_birthNeighbors})");
        _birthNeighbors = (int)GUI.HorizontalSlider(new Rect(150, 10, 250, 50), _birthNeighbors, 0, 26);
        GUI.Label(new Rect(10, 70, 140, 50), $"DeathNeighbors({_deathNeighbors})");
        _deathNeighbors = (int)GUI.HorizontalSlider(new Rect(150, 70, 250, 50), _deathNeighbors, 0, 26);
        GUI.Label(new Rect(10, 130, 140, 50), $"Iterations({_iterations})");
        _iterations  = (int)GUI.HorizontalSlider(new Rect(150, 130, 250, 50), _iterations, 1, 100);
        GUI.Label(new Rect(10, 190, 140, 50), $"EmptyChance({_emptyChance})");
        _emptyChance = GUI.HorizontalSlider(new Rect(150, 190, 250, 50), _emptyChance, 0f, 1.0f);

        if(GUI.Button(new Rect(10, 250, 250, 50), "Generate World") )
        {
            GenerateWorld();
        }
*/
    }

    // Update is called once per frame
    void Update()
    {
        VoxelWorld.Update();

        /*
        if((DateTime.Now - lastDrop).Milliseconds >= 10)
        {
            lastDrop = DateTime.Now;
            var x = UnityEngine.Random.Range(-256, 256);
            var z = UnityEngine.Random.Range(-256, 256);
            
            var highestPoint = VoxelWorld.GetHighestVoxelPos(x, z);
            if(highestPoint.HasValue)
            {
                //_world.SetVoxel(x, highestPoint.Value, z, VoxelType.Empty, true);
                VoxelWorld.SetVoxelSphere(new Vector3Int(x, highestPoint.Value, z), 5, VoxelType.Empty, true);
            }
        }
        */
    }
}
