using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static LightMap;

public class WorldUpdateScheduler : MonoBehaviour
{
    public int MaxNumSimultaneousJobs = 10;

    public event Action BatchFinished;

    public void Awake()
    {
        _player = FindObjectOfType<PlayerController>();
        _world = FindObjectOfType<VoxelWorld>();
        _worldGenerator = FindObjectOfType<WorldGenerator>();
    }

    public void Update()
    {
        if(!_batching)
        {
            ScheduleJobs();
            HandleActiveJobs();
            
            // Process any new jobs that were schduled by the post execute of finished jobs immediately, instead of waiting for the next frame update
            ScheduleJobs();
        }
    }

    public void OnGUI()
    {
        GUI.Label(new Rect(10, 80, 1500, 20), $"Queued Jobs: {_jobQueue.Count} | Active Jobs: {_activeJobs.Count} | ReservedChunks: {_reservedChunks.Count}");

        int i = 0;
        int currentPrio = 0;
        Type currentType = null;
        int num = 0;

        foreach(var item in _jobQueue.GetList())
        {
            if(item.Priority.JobTypePriority != currentPrio || currentType != item.Value.GetType())
            {
                num = 0;
                currentPrio = item.Priority.JobTypePriority;
                currentType = item.Value.GetType();
            }
            if(num < 3)
            {
                GUI.Label(new Rect(10, 100 + i * 20, 500, 20), $"{item.Priority}: {item.Value.ToString()} [{item.Value.GetType()}]");
                i++;
                num++;
            }
        }
    }

    public void StartBatch()
    {
        _batching = true;
        _batchTimer.Restart();
        _currentBatchStage = -1;
        _stageTimings.Clear();
        _stageJobNames.Clear();
    }

    public void FinishBatch() => _batching = false;

    public void AddChunkMeshRebuildJob(Vector3Int chunkPos)
    {
        if(!_world.ChunkExists(chunkPos)) return;
        AddJob(new ChunkMeshRebuildJob(chunkPos));
    }

    public void AddSunlightUpdateJob() => AddJob(new SunlightUpdateJob());

    public void AddSunlightColumnJob(Chunk topChunk, List<LightNode> sharedSpillover) => AddJob(new SunlightColumnJob(topChunk, sharedSpillover));
    
    public void AddSunlightHorizontalSpillJob(List<LightNode> sharedSpillover) => AddJob(new SunlightHorizontalSpillJob(sharedSpillover));

    public void AddChunkLightFillUpdateJob(Vector3Int chunkPos)
    {
        if(!_world.ChunkExists(chunkPos)) return;
        AddJob(new ChunkLightFillUpdateJob(chunkPos));
    }

    public void AddBlockLightUpdateJob(Vector3Int chunkPos, Vector3Int lightPos, Color32 lightColor, bool addLight, bool sunlight)
    {
        if(!_world.ChunkExists(chunkPos)) return;
        AddJob(new BlockLightUpdateJob(chunkPos, lightPos, lightColor, addLight, sunlight));
    }

    public void AddChunkLightMappingUpdateJob(Vector3Int chunkPos)
    {
        if(!_world.ChunkExists(chunkPos)) return;
        AddJob(new ChunkLightMappingUpdateJob(chunkPos));
    }

    public void AddChunkGenerationJob(Vector3Int chunkPos)
    {
        AddJob(new ChunkGenerationJob(chunkPos));
    }

    public void AddChunkVoxelCreationJob(Vector3Int chunkPos, ushort[,,] voxelData, bool hasVoxelData)
    {
        AddJob(new ChunkVoxelCreationJob(chunkPos, voxelData, hasVoxelData));
    }

    public void AddBackloggedVoxelCreationJob() => AddJob(new BackloggedVoxelCreationJob());

    public void AddJob(IWorldUpdateJob worldUpdateJob)
    {        
        _jobQueue.EnqueueUnique(
            worldUpdateJob,
            new JobPriority
            {
                JobTypePriority = worldUpdateJob.UpdateStage,
                DistanceToPlayer = VoxelPosHelper.GetChunkSqrDistanceToWorldPos(_player.transform.position, worldUpdateJob.ChunkPos)
            }
        );
    }

    private void ScheduleJobs()
    {
        while(
            _activeJobs.Count < MaxNumSimultaneousJobs && 
            NextJobIsNotHigherStageThanActiveJobs()
            && _jobQueue.DequeueNext(x => CanReserveChunks(x.AffectedChunks), out var job))
        {
            if (job.UpdateStage != _currentBatchStage)
            {
                var now = _batchTimer.Elapsed.TotalMilliseconds;
                if (_currentBatchStage >= 0)
                    _stageTimings[_currentBatchStage] = (_stageTimings[_currentBatchStage].start, now);
                _currentBatchStage = job.UpdateStage;
                _stageTimings[_currentBatchStage] = (now, -1);
            }

            if (!_stageJobNames.TryGetValue(job.UpdateStage, out var names))
                _stageJobNames[job.UpdateStage] = names = new HashSet<string>();
            names.Add(job.GetType().Name);

            var token = Profiler.StartProfiling($"Jobs/{job.GetType()}/PreExecute");
            var preExecute = job.PreExecuteSync(_world, _worldGenerator);
            Profiler.StopProfiling(token);

            if(!preExecute)
            {
                // Pre execution failed -> job is not executable -> skip
                continue;
            }

            ReserveChunks(job.AffectedChunks);

            _activeJobs.AddLast(new ActiveJob
            {
                Job = job,
                JobTask = ProfileAsync(job.ExecuteAsync(), job.GetType())
            });
        }
    }

    private async Task ProfileAsync(Task asyncJobTask, Type jobType)
    {

        var token = Profiler.StartProfiling($"Jobs/{jobType}/ExecuteAsync");

        try
        {
            await asyncJobTask;
        }
        finally
        {
            Profiler.StopProfiling(token);
        }
    }

    private void HandleActiveJobs()
    {
        var jobNode = _activeJobs.First;
        while(jobNode != null)
        {
            var next = jobNode.Next;
            if (jobNode.Value.JobTask.IsCompleted)
            {
                if(jobNode.Value.JobTask.Exception != null)
                {
                    UnityEngine.Debug.LogException(jobNode.Value.JobTask.Exception);
                }

                var token = Profiler.StartProfiling($"Jobs/{jobNode.Value.Job.GetType()}/PostExecuteSync");
                jobNode.Value.Job.PostExecuteSync(_world, _worldGenerator, this);
                Profiler.StopProfiling(token);

                _activeJobs.Remove(jobNode);

                ReleaseReservedChunks(jobNode.Value.Job.AffectedChunks);

                if(_activeJobs.Count == 0 && _jobQueue.Count == 0)
                {
                    _batchTimer.Stop();

                    if (_currentBatchStage >= 0)
                        _stageTimings[_currentBatchStage] = (_stageTimings[_currentBatchStage].start, _batchTimer.Elapsed.TotalMilliseconds);

                    LogBatchUpdateTimings();
                    Profiler.LogProfilingResults();

                    BatchFinished?.Invoke();
                }
            }
            jobNode = next;
        }
    }    

    private void LogBatchUpdateTimings()
    {
        var sb = new StringBuilder($"Batch finished in {_batchTimer.Elapsed.TotalMilliseconds:F1}ms");
        foreach (var kvp in _stageTimings)
        {
            var jobNames = _stageJobNames.TryGetValue(kvp.Key, out var names) ? string.Join(", ", names) : "";
            sb.Append($"\n  Stage {kvp.Key} ({(kvp.Value.end - kvp.Value.start):F1}ms): {jobNames}");
        }
        UnityEngine.Debug.Log(sb.ToString());
    }

    private bool NextJobIsNotHigherStageThanActiveJobs()
    {
        if(_activeJobs.Count == 0)
        {
            // No active jobs so we are clear to go to the next update stage
            return true;
        }

        if(_jobQueue.TryPeekNextPriority(out var priority))
        {
            // Only allow new job if it is at the same or lower stage than currently active jobs
            var activeJobsStage = _activeJobs.First.Value.Job.UpdateStage;
            return priority.JobTypePriority <= activeJobsStage;
        }

        // No more jobs queued
        return true;
    }    

    private bool CanReserveChunks(HashSet<Vector3Int> chunksToBeReserved)
    {
        return !_reservedChunks.Overlaps(chunksToBeReserved);
    }

    private void ReserveChunks(HashSet<Vector3Int> chunksToBeReserved)
    {
        _reservedChunks.UnionWith(chunksToBeReserved);
    }

    private void ReleaseReservedChunks(IEnumerable<Vector3Int> chunks)
    {
        _reservedChunks.ExceptWith(chunks);
    }

    private int _currentBatchStage = -1;

    private SortedDictionary<int, (double start, double end)> _stageTimings = new SortedDictionary<int, (double start, double end)>();

    private SortedDictionary<int, HashSet<string>> _stageJobNames = new SortedDictionary<int, HashSet<string>>();

    private bool _batching;

    private PlayerController _player;

    private VoxelWorld _world;

    private WorldGenerator _worldGenerator;

    private HashSet<Vector3Int> _reservedChunks = new HashSet<Vector3Int>();

    private PriorityQueue<IWorldUpdateJob, JobPriority> _jobQueue = new PriorityQueue<IWorldUpdateJob, JobPriority>();

    private LinkedList<ActiveJob> _activeJobs = new LinkedList<ActiveJob>();

    private Stopwatch _batchTimer = new Stopwatch();

    private struct ActiveJob
    {
        public IWorldUpdateJob Job { get; set; }

        public Task JobTask { get; set; }
    }
}