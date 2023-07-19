using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    public int ChunkGenerationRadius = 4;

    public int MaxNumChunkGenerationTasks = 4;

    public int MaxNumChunksCreatedPerFrame = 2;

    public int WorldSeed = 123456789;

    public bool WorldGenerated { get; private set; }

    void Awake()
    {
        _dirtType = BlockDataRepository.GetBlockTypeId("Dirt");
        _grassType = BlockDataRepository.GetBlockTypeId("Grass");
        _torchType = BlockDataRepository.GetBlockTypeId("Torch");
        _logType = BlockDataRepository.GetBlockTypeId("Log");
        _leavesType = BlockDataRepository.GetBlockTypeId("Leaves");

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
        //Profiler.StartProfiling("WorldGen-1-GenerateChunksAroundCenter");

        var currentPlayerChunkPos = VoxelPosHelper.WorldPosToChunkPos(_player.transform.position);
        if(!_initialChunkBatchGenerated || (currentPlayerChunkPos - _lastChunkGenerationCenter).magnitude > 1)
        {
            _initialChunkBatchGenerated = true;
            _currentWorldUpdateStarted = true;

            _updateScheduler.StartBatch();
            GenerateChunksAroundCenter(currentPlayerChunkPos);
        }

        if(AnyChunksPending())
        {
            ProcessChunkGenerationQueue();
            HandleChunkGenerationTasks();
            ProcessChunkCreationQueue();
        }
        else if(_currentWorldUpdateStarted)
        {
            _updateScheduler.AddSunlightUpdateJob();
            _updateScheduler.FinishBatch();

            _currentWorldUpdateStarted = false;
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
        GUI.Label(new Rect(10, 50, 600, 20), $"Generating={_chunkGenerationQueue.Count} | Creating={_chunkCreationQueue.Count} | LastCenter={_lastChunkGenerationCenter}");

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
                        _chunkGenerationQueue.Enqueue(chunkPos, sqrDistToPlayer);
                        _currentlyLoadedChunks.Add(chunkPos);
                    }
                }                
            }
        }

        BuildBackloggedVoxelsWithinPlayerRadius();
    }

    private void BuildBackloggedVoxelsWithinPlayerRadius()
    {
        var processedChunks = new List<Vector3Int>();

        foreach(var chunkBacklog in _chunkCreationBacklog)
        {
            var chunkPos = chunkBacklog.Key;
            var sqrDistToPlayer = VoxelPosHelper.GetChunkSqrDistanceToWorldPos(_player.transform.position, chunkPos);
            if(sqrDistToPlayer <= _chunkGenerationRadiusSqr)
            {
                foreach(var voxel in chunkBacklog.Value)
                {
                    var globalVoxelPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(voxel.LocalVoxelPos, chunkPos);
                    _voxelWorld.SetVoxel(globalVoxelPos, voxel.Type);
                }

                processedChunks.Add(chunkPos);
            }
        }

        foreach(var processedChunk in processedChunks)
        {
            _chunkCreationBacklog.Remove(processedChunk);
        }
    }

    private void ProcessChunkGenerationQueue()
    {
        var playerPos = _player.transform.position;
        while(_chunkGenerationTasks.Count < MaxNumChunkGenerationTasks && _chunkGenerationQueue.TryDequeue(out var chunkPos))
        {
            var task = Task.Run<ChunkUpdate>(() => GenerateChunk(chunkPos, playerPos));
            _chunkGenerationTasks.Add(task);
        }
    }

    private ChunkUpdate GenerateChunk(Vector3Int chunkPos, Vector3 playerPos)
    {
        var chunkDistToPlayer = VoxelPosHelper.GetChunkSqrDistanceToWorldPos(playerPos, chunkPos);

        var builder = new ChunkUpdateBuilder(chunkPos, chunkDistToPlayer, playerPos, _chunkGenerationRadiusSqr);

        var chunkBasePos = VoxelPosHelper.ChunkPosToGlobalChunkBaseVoxelPos(chunkPos);

        for(int z = 0; z < VoxelInfo.ChunkSize; ++z)
        {
            for(int y = 0; y < VoxelInfo.ChunkSize; ++y)
            {
                for(int x = 0; x < VoxelInfo.ChunkSize; ++x)
                {
                    var localVoxelPos = new Vector3Int(x, y, z);
                    var globalVoxelPos = chunkBasePos + localVoxelPos;
                    var terrainHeight = GetTerrainHeight(globalVoxelPos);

                    if(globalVoxelPos.y < terrainHeight)
                    {
                        builder.QueueVoxel(localVoxelPos, _dirtType);
                    }
                    else if(globalVoxelPos.y == terrainHeight)
                    {
                        builder.QueueVoxel(localVoxelPos, _grassType);

                        if(TreeShouldBePlaced(localVoxelPos) && (x % 15 == 0 || y % 15 == 0 || z % 15 == 0))
                        {
                            PlaceTree(builder, localVoxelPos, 4, 3);
                        }
                    }
                    else if(globalVoxelPos.y == terrainHeight + 1)
                    {
                        var rand = new System.Random();
                        if(rand.NextDouble() > 0.999)
                        {
                            builder.QueueVoxel(localVoxelPos, _torchType);
                        }
                    }
                }
            }
        }

        return builder.GetChunkUpdate();
    }

    private void PlaceTree(
        ChunkUpdateBuilder builder, 
        Vector3Int localRootPos, 
        int trunkHeight, 
        int crownRadius)
    {
        for(int ty = 1; ty <= trunkHeight; ++ty)
        {
            builder.QueueVoxel(localRootPos + Vector3Int.up * ty, _logType);
        }

        for(int tz = -crownRadius; tz <= crownRadius; ++tz)
        {
            for(int ty = -crownRadius; ty <= crownRadius; ++ty)
            {
                for(int tx = -crownRadius; tx <= crownRadius; ++tx)
                {
                    builder.QueueVoxel(localRootPos + Vector3Int.up * trunkHeight + new Vector3Int(tx, ty + trunkHeight, tz), _leavesType);
                }
            }
        }
    }

    private bool TreeShouldBePlaced(Vector3Int globalVoxelPos)
    {
        //var rand = new System.Random(globalVoxelPos.x + 1000 * globalVoxelPos.y + 1000000 + globalVoxelPos.z);
        var rand = new System.Random();
        return rand.NextDouble() <= 0.001f;
    }

    private void HandleChunkGenerationTasks()
    {
        var tasksToRemove = new HashSet<Task<ChunkUpdate>>();

        foreach(var task in _chunkGenerationTasks)
        {
            if(task.IsCompleted)
            {
                var chunkUpdate = task.Result;

                _chunkCreationMap[chunkUpdate.ChunkPos] = chunkUpdate.Voxels;
                _chunkCreationQueue.Enqueue(chunkUpdate.ChunkPos, chunkUpdate.ChunkDistanceToPlayer);

                foreach(var chunkUpdateBacklog in chunkUpdate.Backlog)
                {
                    if(_chunkCreationBacklog.ContainsKey(chunkUpdateBacklog.Key))
                    {
                        // Merge new chunk update backlog into existing backlog
                        _chunkCreationBacklog[chunkUpdateBacklog.Key].AddRange(chunkUpdateBacklog.Value);
                    }
                    else
                    {
                        // If no backlog exists for this chunk yet, take over the one from the chunk update
                        _chunkCreationBacklog[chunkUpdateBacklog.Key] = chunkUpdateBacklog.Value;
                    }    
                }

                tasksToRemove.Add(task);
            }
        }

        _chunkGenerationTasks.RemoveAll(x => tasksToRemove.Contains(x));
    }

    private void ProcessChunkCreationQueue()
    {
        //var sw = new Stopwatch();
        //sw.Start();

        int numChunksToCreate = MaxNumChunksCreatedPerFrame;
        while(numChunksToCreate > 0 && _chunkCreationQueue.TryDequeue(out var chunkPos))
        {
            numChunksToCreate--;

            var voxels = _chunkCreationMap[chunkPos];
            foreach(var voxel in voxels)
            {        
                var globalPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(voxel.LocalVoxelPos, chunkPos);
                _voxelWorld.SetVoxel(globalPos, voxel.Type);
            }

            _chunkCreationMap.Remove(chunkPos);
        }

        //sw.Stop();
        //UnityEngine.Debug.Log($"Created chunk in {sw.ElapsedMilliseconds}ms");
    }

    private bool AnyChunksPending() => 
        _chunkGenerationQueue.Count > 0 
        || _chunkGenerationTasks.Count > 0
        || _chunkCreationMap.Count > 0
        || _chunkCreationQueue.Count > 0;

    private void UnloadChunk(Vector3Int chunkPos)
    {

    }

    private int GetTerrainHeight(Vector3Int globalVoxelPos)
        => (int)(Mathf.PerlinNoise((float)globalVoxelPos.x / 40.0f, (float)globalVoxelPos.z / 40.0f) * 32) - 5;

    

    private void GenerateWorld()
    {
        var sw = new Stopwatch();
        sw.Start();

        _voxelWorld.Clear();
        
        //GenerateCubeRoom(3);
        //GenerateCuboidByCorners(new Vector3Int(-16, 0, -16), new Vector3Int(16, -16, 16), BlockDataRepository.GetBlockTypeId("Dirt"));
        
        /*
        for(int x = 0; x < 17; ++x)
        {
            GenerateCuboidByCorners(new Vector3Int(x, 0, 0), new Vector3Int(x, x, 4), BlockDataRepository.GetBlockTypeId("Dirt"));
        }
        */
        
        //GenerateCube(new Vector3Int(0, 0, 0), 32, BlockDataRepository.GetBlockTypeId("Dirt"));

        //VoxelWorld.SetVoxel(new Vector3Int(-2, 0, 0), BlockDataRepository.GetBlockTypeId("Torch"));
        
        //VoxelWorld.AddLight(new Vector3Int(0, 1, 0), new Color32(255, 78, 203, 255), 20);
        //VoxelWorld.AddLight(new Vector3Int(30, 2, 0), new Color32(50, 255, 50, 255), 20);
        //VoxelWorld.AddLight(new Vector3Int(15, 2, -10), new Color32(255, 255, 0, 255), 20);

        GenerateTerrain(256);

/*
        GenerateCave(
            new Vector3Int(0, 0, 0),
            new Vector3Int(
                32,//UnityEngine.Random.Range(32, 128),
                32,//UnityEngine.Random.Range(32, 128), 
                32//UnityEngine.Random.Range(32, 128)
            ),
            _iterations,
            _birthNeighbors,
            _deathNeighbors,
            _emptyChance
        );       
*/
        //GenerateTorches(10);

        PlacePlayer();

        sw.Stop();
        UnityEngine.Debug.Log($"Generated world in {sw.Elapsed.TotalSeconds} sec");
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

    private void GenerateTorches(int numTorches)
    {
        for (int i = 0; i < numTorches; ++i)
        {
            var worldPos = _voxelWorld.GetRandomSolidSurfaceVoxel() + Vector3Int.up;
            _voxelWorld.SetVoxel(worldPos, BlockDataRepository.GetBlockTypeId("Torch"));
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
                        _voxelWorld.SetVoxel(x, y, z, BlockDataRepository.GetBlockTypeId("Cobblestone"));
                    }
                    else if(isWater)
                    {

                        _voxelWorld.SetVoxel(x, y, z, BlockDataRepository.GetBlockTypeId("Water"));
                    }
                    else
                    {
                        if(y < height)
                        {
                            _voxelWorld.SetVoxel(x, y, z, BlockDataRepository.GetBlockTypeId("Dirt"));
                        }
                        else
                        {
                            _voxelWorld.SetVoxel(x, y, z, BlockDataRepository.GetBlockTypeId("Grass"));
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

    bool _currentWorldUpdateStarted = false;

    public int _chunkGenerationRadiusSqr;

    private WorldUpdateScheduler _updateScheduler;

    private VoxelWorld _voxelWorld;

    private PlayerController _player;

    private Vector3Int _lastChunkGenerationCenter;

    private HashSet<Vector3Int> _currentlyLoadedChunks = new HashSet<Vector3Int>();

    // Chunks to be generated
    private PriorityQueue<Vector3Int, float> _chunkGenerationQueue = new PriorityQueue<Vector3Int, float>();

    // The voxels in the chunks to be created in the VoxelWorld after being generated
    private Dictionary<Vector3Int, List<VoxelCreationAction>> _chunkCreationMap = new Dictionary<Vector3Int, List<VoxelCreationAction>>();

    // The order of the chunks to be created in the VoxelWorld after being generated
    private PriorityQueue<Vector3Int, float> _chunkCreationQueue = new PriorityQueue<Vector3Int, float>();

    // Holds queued voxel creation actions that are outside of the player radius and will be applied, once the chunks they are in are loaded/generated
    private Dictionary<Vector3Int, List<VoxelCreationAction>> _chunkCreationBacklog = new Dictionary<Vector3Int, List<VoxelCreationAction>>();

    // Chunks to be unloaded
    private PriorityQueue<Vector3Int, float> _chunkUnloadingQueue = new PriorityQueue<Vector3Int, float>();

    private List<Task<ChunkUpdate>> _chunkGenerationTasks = new List<Task<ChunkUpdate>>();

    private ushort _dirtType;

    private ushort _grassType;

    private ushort _torchType;

    private ushort _logType;
    
    private ushort _leavesType;    
}
