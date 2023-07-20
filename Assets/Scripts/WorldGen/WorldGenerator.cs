using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    public int ChunkGenerationRadius = 4;

    public int MaxNumChunkGenerationTasks = 4;

    public int MaxNumChunksCreatedPerFrame = 2;

    public int WorldSeed = 123456789;

    public bool WorldGenerated { get; private set; }

    public ChunkGenerator ChunkGenerator { get; private set; }

    public void AddBackloggedVoxels(Vector3Int chunkPos, List<VoxelCreationAction> voxels)
    {
        if(_chunkCreationBacklog.ContainsKey(chunkPos))
        {
            // Merge new chunk update backlog into existing backlog
            _chunkCreationBacklog[chunkPos].AddRange(voxels);
        }
        else
        {
            // If no backlog exists for this chunk yet, take over the one from the chunk update
            _chunkCreationBacklog[chunkPos] = voxels;
        }    
    }

    public List<(Vector3Int ChunkPos, List<VoxelCreationAction> Voxels)> PopAllBackloggedChunksWithinGenerationRadius()
    {
        var chunks = new List<(Vector3Int ChunkPos, List<VoxelCreationAction> Voxels)>();

        foreach(var chunkBacklog in _chunkCreationBacklog)
        {
            var chunkPos = chunkBacklog.Key;
            var sqrDistToPlayer = VoxelPosHelper.GetChunkSqrDistanceToWorldPos(_player.transform.position, chunkPos);
            if(sqrDistToPlayer <= _chunkGenerationRadiusSqr)
            {
                chunks.Add((chunkPos, chunkBacklog.Value));
            }
        }

        foreach(var chunk in chunks)
        {
            _chunkCreationBacklog.Remove(chunk.ChunkPos);
        }

        return chunks;
    }

    public bool TryPopBackloggedChunk(Vector3Int chunkPos, out List<VoxelCreationAction> voxels)
    {
        if(!_chunkCreationBacklog.ContainsKey(chunkPos))
        {
            voxels = null;
            return false;
        }

        voxels = _chunkCreationBacklog[chunkPos];
        _chunkCreationBacklog.Remove(chunkPos);
        return true;
    }

    void Awake()
    {
        ChunkGenerator = new ChunkGenerator();
        _chunkGenerationRadiusSqr = ChunkGenerationRadius * ChunkGenerationRadius;
        _updateScheduler = FindObjectOfType<WorldUpdateScheduler>();
        _voxelWorld = FindObjectOfType<VoxelWorld>();
        _player = FindObjectOfType<PlayerController>();

        _updateScheduler.BatchFinished += () => 
        {
            if(!WorldGenerated)
            {
                PlacePlayer(Vector3Int.zero);            
                WorldGenerated = true;
            }
        };
    }

    void Update()
    {       
        var currentPlayerChunkPos = VoxelPosHelper.WorldPosToChunkPos(_player.transform.position);
        if(!_initialChunkBatchGenerated || (currentPlayerChunkPos - _lastChunkGenerationCenter).magnitude > 1)
        {
            _initialChunkBatchGenerated = true;

            _updateScheduler.StartBatch();

            GenerateChunksAroundCenter(currentPlayerChunkPos);
            _updateScheduler.AddBackloggedVoxelCreationJob();
            _updateScheduler.AddSunlightUpdateJob();
            
            _updateScheduler.FinishBatch();
        }

        /*
        // stress test!!
        if(WorldGenerated && (DateTime.Now - lastDrop).TotalMilliseconds >= 50)
        {
            lastDrop = DateTime.Now;
            var x = UnityEngine.Random.Range(-256, 256);
            var z = UnityEngine.Random.Range(-256, 256);
            
            //var highestPoint = _voxelWorld.GetHighestVoxelPos(x, z);
            var highestPoint = _voxelWorld.GetRandomSolidSurfaceVoxel();
            _voxelWorld.SetVoxelSphere(highestPoint, 5, 0);
        }
        */
        
    }

    void OnGUI()
    {
        var profiling = Profiler.GetProfilingResults();
        int i = 0;
        foreach(var prof in profiling)
        {
            GUI.Label(new Rect(10, 400 + 20 * i, 600, 30), $"{prof.Key}: {prof.Value}ms");
            i++;
        }
    }

    private void GenerateChunksAroundCenter(Vector3Int centerChunkPos)
    {
        _lastChunkGenerationCenter = centerChunkPos;

        for(int z = -ChunkGenerationRadius; z <= ChunkGenerationRadius; ++z)
        {
            for(int y = -ChunkGenerationRadius; y <= ChunkGenerationRadius; ++y)
            //for(int y = 0; y <= 0; ++y)
            {
                for(int x = -ChunkGenerationRadius; x <= ChunkGenerationRadius; ++x)
                {
                    var chunkPos = centerChunkPos + new Vector3Int(x, y, z);
                    if(_currentlyLoadedChunks.Contains(chunkPos)) continue;

                    var sqrDistToPlayer = VoxelPosHelper.GetChunkSqrDistanceToWorldPos(_player.transform.position, chunkPos);
                    if(sqrDistToPlayer <= _chunkGenerationRadiusSqr)
                    {
                        //_chunkGenerationQueue.Enqueue(chunkPos, sqrDistToPlayer);
                        _updateScheduler.AddChunkGenerationJob(chunkPos);

                        _currentlyLoadedChunks.Add(chunkPos);
                    }
                }                
            }
        }
    }   

    private void GenerateCuboidByCorners(Vector3Int p1, Vector3Int p2, ushort type)
    {
        var xs = Math.Min(p1.x, p2.x);
        var xe = Math.Max(p1.x, p2.x);
        var ys = Math.Min(p1.y, p2.y);
        var ye = Math.Max(p1.y, p2.y);
        var zs = Math.Min(p1.z, p2.z);
        var ze = Math.Max(p1.z, p2.z);

        for(int x = xs; x <= xe; ++x)
        {
            for(int z = zs; z <= ze; ++z)
            {
                for(int y = ys; y <= ye; ++y)
                {
                    _voxelWorld.SetVoxel(x, y, z, type);
                }
            }
        }
    }

    private void GenerateCuboid(Vector3Int pos, Vector3Int size, ushort type)
    {
        for(int x = -size.x; x < size.x; ++x)
        {
            for(int z = -size.z; z < size.z; ++z)
            {
                for(int y = -size.y; y <= size.y; ++y)
                {
                    _voxelWorld.SetVoxel(pos.x + x, pos.y + y, pos.z + z, type);
                }
            }
        }
    }

    private void GenerateCube(Vector3Int pos, int size, ushort type)
    {
        GenerateCuboid(pos, new Vector3Int(size, size, size), type);
    }

    private void GenerateCubeRoom(int size)
    {
        for(int x = -size; x < size; ++x)
        {
            for(int z = -size; z < size; ++z)
            {
                for(int y = -size; y < size; ++y)
                {
                    if(x == -size || x == size-1 || y == -size || y == size-1 || z == -size || z == size-1)
                    {
                        _voxelWorld.SetVoxel(x, y, z, BlockDataRepository.GetBlockTypeId("Dirt"));
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
                        _voxelWorld.SetVoxel(
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

    private void PlacePlayer(Vector3Int? startPos = null)
    {
        var pos = startPos.HasValue 
            ? new Vector3Int(startPos.Value.x, _voxelWorld.GetHighestVoxelPos(startPos.Value.x, startPos.Value.z).Value, startPos.Value.z) 
            : _voxelWorld.GetRandomSolidSurfaceVoxel();
        var worldPos = VoxelPosHelper.GetVoxelTopCenterSurfaceWorldPos(pos) + Vector3.up;

        var player = GameObject.Find("Player");

        var characterController = player.GetComponent<CharacterController>();
        characterController.enabled = false;
        characterController.transform.position = worldPos + Vector3.up;
        characterController.enabled = true;

        var playerController = player.GetComponent<PlayerController>();
        playerController.SetGravityActive(true);
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


    bool _initialChunkBatchGenerated = false;

    private int _chunkGenerationRadiusSqr;

    private WorldUpdateScheduler _updateScheduler;

    private VoxelWorld _voxelWorld;

    private PlayerController _player;

    private Vector3Int _lastChunkGenerationCenter;

    private HashSet<Vector3Int> _currentlyLoadedChunks = new HashSet<Vector3Int>();

    // Holds queued voxel creation actions that are outside of the player radius and will be applied, once the chunks they are in are loaded/generated
    private Dictionary<Vector3Int, List<VoxelCreationAction>> _chunkCreationBacklog = new Dictionary<Vector3Int, List<VoxelCreationAction>>();
}
