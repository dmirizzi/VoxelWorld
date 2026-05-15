using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class WorldGenerator : MonoBehaviour
{
    public int ChunkGenerationRadius = 4;

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

    public List<VoxelCreationAction> PopBackloggedChunk(Vector3Int chunkPos)
    {
        if(!_chunkCreationBacklog.ContainsKey(chunkPos))
        {
            return null;
        }

        var voxels = _chunkCreationBacklog[chunkPos];
        _chunkCreationBacklog.Remove(chunkPos);
        return voxels;
    }

    void Awake()
    {
        VoxelBuildHelper.BuildVoxelUVCache();

        ChunkGenerator = new ChunkGenerator(WorldSeed);
        _chunkGenerationRadiusSqr = ChunkGenerationRadius * ChunkGenerationRadius;
        _updateScheduler = FindObjectOfType<WorldUpdateScheduler>();
        _voxelWorld = FindObjectOfType<VoxelWorld>();
        _player = FindObjectOfType<PlayerController>();

        _savedVSyncCount = QualitySettings.vSyncCount;
        _savedTargetFrameRate = Application.targetFrameRate;
        _savedMaxJobs = _updateScheduler.MaxNumSimultaneousJobs;

        _camera = Camera.main;
        _camera.enabled = false;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        _updateScheduler.MaxNumSimultaneousJobs = int.MaxValue;

        _updateScheduler.BatchFinished += () =>
        {
            if(!WorldGenerated)
            {
                _camera.enabled = true;
                QualitySettings.vSyncCount = _savedVSyncCount;
                Application.targetFrameRate = _savedTargetFrameRate;
                _updateScheduler.MaxNumSimultaneousJobs = _savedMaxJobs;

                //Profiler.WriteProfilingResultsToCSV();
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
        
    }

    void OnGUI()
    {
        if (!WorldGenerated) return;

        var profiling = Profiler.GetProfilingResults().OrderByDescending(x => x.Subject);
        int i = 0;
        foreach(var prof in profiling)
        {
            GUI.Label(new Rect(10, 400 + 20 * i, 600, 30), $"{prof.Subject}: {prof.TotalElapsedMs}ms");
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

    bool _initialChunkBatchGenerated = false;

    private int _chunkGenerationRadiusSqr;

    private WorldUpdateScheduler _updateScheduler;

    private VoxelWorld _voxelWorld;

    private PlayerController _player;

    private Vector3Int _lastChunkGenerationCenter;

    private HashSet<Vector3Int> _currentlyLoadedChunks = new HashSet<Vector3Int>();

    // Holds queued voxel creation actions that are outside of the player radius and will be applied, once the chunks they are in are loaded/generated
    private Dictionary<Vector3Int, List<VoxelCreationAction>> _chunkCreationBacklog = new Dictionary<Vector3Int, List<VoxelCreationAction>>();

    private Camera _camera;
    private int _savedVSyncCount;
    private int _savedTargetFrameRate;
    private int _savedMaxJobs;
}
