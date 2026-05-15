using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class WorldGenerator : MonoBehaviour
{
    public int ChunkGenerationRadius = 4;
    public int MinVerticalRadius = 3;
    public int VerticalMovementWindowSize = 10;
    public float GenerationCooldown = 1.5f;
    public int ChunkGeneratioUpRadius = 4;
    public int ChunkGenerationDownRadius = 4;

    public int WorldSeed = 123456789;

    public bool WorldGenerated { get; private set; }

    public ChunkGenerator ChunkGenerator { get; private set; }

    public event Action OnWorldReady;

    public void RegisterTrackedObject(Transform trackedObject) => _trackedObject = trackedObject;

    public void AddBackloggedVoxels(Vector3Int chunkPos, List<VoxelCreationAction> voxels)
    {
        if(_chunkCreationBacklog.ContainsKey(chunkPos))
        {
            _chunkCreationBacklog[chunkPos].AddRange(voxels);
        }
        else
        {
            _chunkCreationBacklog[chunkPos] = voxels;
        }
    }

    public List<(Vector3Int ChunkPos, List<VoxelCreationAction> Voxels)> PopAllBackloggedChunksWithinGenerationRadius()
    {
        var chunks = new List<(Vector3Int ChunkPos, List<VoxelCreationAction> Voxels)>();

        foreach(var chunkBacklog in _chunkCreationBacklog)
        {
            var chunkPos = chunkBacklog.Key;
            var sqrDistToPlayer = VoxelPosHelper.GetChunkSqrDistanceToWorldPos(_trackedObject.position, chunkPos);
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
                WorldGenerated = true;
                OnWorldReady?.Invoke();
            }
        };
    }

    void Update()
    {
        var currentPlayerChunkPos = VoxelPosHelper.WorldPosToChunkPos(_trackedObject.position);

        if(_verticalMovementBuffer.Count == 0 || currentPlayerChunkPos.y != _lastTrackedChunkY)
        {
            _verticalMovementBuffer.Enqueue(currentPlayerChunkPos.y);
            if(_verticalMovementBuffer.Count > VerticalMovementWindowSize)
                _verticalMovementBuffer.Dequeue();
            _lastTrackedChunkY = currentPlayerChunkPos.y;
        }

        bool cooldownElapsed = !_initialBatchScheduled || (Time.time - _lastGenerationTime >= GenerationCooldown);
        if(!cooldownElapsed) return;

        ComputeVerticalRadii(currentPlayerChunkPos.y);

        if(!HasNewChunksToGenerate(currentPlayerChunkPos, ChunkGenerationDownRadius, ChunkGeneratioUpRadius)) return;

        _lastGenerationTime = Time.time;
        _initialBatchScheduled = true;

        _updateScheduler.StartBatch();
        GenerateChunksAroundCenter(currentPlayerChunkPos, ChunkGenerationDownRadius, ChunkGeneratioUpRadius);
        _updateScheduler.AddBackloggedVoxelCreationJob();
        _updateScheduler.AddSunlightUpdateJob();
        _updateScheduler.FinishBatch();
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

    private bool IsChunkLoaded(Vector3Int chunkPos) => _currentlyLoadedChunks.Contains(chunkPos);

    private void ComputeVerticalRadii(int currentChunkY)
    {
        var bufferMin = int.MaxValue;
        var bufferMax = int.MinValue;
        foreach(var y in _verticalMovementBuffer)
        {
            if(y < bufferMin) bufferMin = y;
            if(y > bufferMax) bufferMax = y;
        }

        ChunkGenerationDownRadius = Mathf.Clamp(currentChunkY - bufferMin, MinVerticalRadius, ChunkGenerationRadius);
        ChunkGeneratioUpRadius = Mathf.Clamp(bufferMax - currentChunkY, MinVerticalRadius, ChunkGenerationRadius);
    }

    private bool HasNewChunksToGenerate(Vector3Int centerChunkPos, int downRadius, int upRadius)
    {
        var radiusSqr = ChunkGenerationRadius * ChunkGenerationRadius;
        for(int z = -ChunkGenerationRadius; z <= ChunkGenerationRadius; ++z)
        {
            for(int y = -downRadius; y <= upRadius; ++y)
            {
                for(int x = -ChunkGenerationRadius; x <= ChunkGenerationRadius; ++x)
                {
                    if(x * x + z * z > radiusSqr) continue;
                    if(!IsChunkLoaded(centerChunkPos + new Vector3Int(x, y, z))) return true;
                }
            }
        }
        return false;
    }

    private void GenerateChunksAroundCenter(Vector3Int centerChunkPos, int downRadius, int upRadius)
    {
        var radiusSqr = ChunkGenerationRadius * ChunkGenerationRadius;
        for(int z = -ChunkGenerationRadius; z <= ChunkGenerationRadius; ++z)
        {
            for(int y = -downRadius; y <= upRadius; ++y)
            {
                for(int x = -ChunkGenerationRadius; x <= ChunkGenerationRadius; ++x)
                {
                    if(x * x + z * z > radiusSqr) continue;

                    var chunkPos = centerChunkPos + new Vector3Int(x, y, z);
                    if(IsChunkLoaded(chunkPos)) continue;

                    _updateScheduler.AddChunkGenerationJob(chunkPos);
                    _currentlyLoadedChunks.Add(chunkPos);
                }
            }
        }
    }

    private bool _initialBatchScheduled;
    private float _lastGenerationTime;
    private int _chunkGenerationRadiusSqr;
    private Queue<int> _verticalMovementBuffer = new Queue<int>();
    private int _lastTrackedChunkY;

    private WorldUpdateScheduler _updateScheduler;
    private Transform _trackedObject;
    private HashSet<Vector3Int> _currentlyLoadedChunks = new HashSet<Vector3Int>();
    private Dictionary<Vector3Int, List<VoxelCreationAction>> _chunkCreationBacklog = new Dictionary<Vector3Int, List<VoxelCreationAction>>();

    private Camera _camera;
    private int _savedVSyncCount;
    private int _savedTargetFrameRate;
    private int _savedMaxJobs;
}
