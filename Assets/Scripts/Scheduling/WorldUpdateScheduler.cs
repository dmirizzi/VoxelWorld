using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

class WorldUpdateScheduler : MonoBehaviour
{
    public int MaxNumSimultaneousJobs = 8;

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
            //GUI.Label(new Rect(10, 100 + i * 20, 300, 20), $"({item.Priority.JobTypePriority}|{item.Priority.DistanceToPlayer}) {item.Value.GetType()} @ {item.Value.ChunkPos}");
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

/*
        GUI.Label(new Rect(10, 140 + i * 20, 1500, 20), $"Active Jobs ({_activeJobs.Count}):");
        foreach(var item in _activeJobs)
        {
            GUI.Label(new Rect(10, 100 + i * 20, 300, 20), $"{item.Job.GetType()} @ {item.Job.ChunkPos}");
            i++;
        }*/
    }

    public void StartBatch() => _batching = true;

    public void FinishBatch()
    {
        /*
        foreach(var job in _jobQueue.GetList())
        {
            Debug.Log($"[{job.Priority}] {job.Value.GetType()} @ {job.Value.ChunkPos}");
        }
        */

        _batching = false;
    }

    public void AddChunkRebuildJob(Vector3Int chunkPos)
    {
        if(!_world.ChunkExists(chunkPos)) return;
        AddJob(new ChunkRebuildJob(chunkPos));
    }

    public void AddSunlightUpdateJob()
    {
        AddJob(new SunlightUpdateJob());
    }

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
        if(_world.ChunkExists(chunkPos)) return;
        AddJob(new ChunkGenerationJob(chunkPos));
    }

    public void AddChunkVoxelCreationJob(Vector3Int chunkPos, List<VoxelCreationAction> voxels)
    {
        AddJob(new ChunkVoxelCreationJob(chunkPos, voxels));
    }

    public void AddBackloggedVoxelCreationJob()
    {
        AddJob(new BackloggedVoxelCreationJob());
    }

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
        while(_activeJobs.Count < MaxNumSimultaneousJobs
            && NextJobIsNotHigherStageThanActiveJobs()
            && _jobQueue.DequeueNext(x => CanReserveChunks(x.AffectedChunks), out var job))
        {
            if(!job.PreExecuteSync(_world, _worldGenerator))
            {
                // Pre execution failed -> job is not executable -> skip
                continue;
            }

            ReserveChunks(job.AffectedChunks);

            _activeJobs.AddLast(new ActiveJob
            {
                Job = job,
                JobTask = job.ExecuteAsync()
            });
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
                jobNode.Value.Job.PostExecuteSync(_world, _worldGenerator, this);
    
                //Debug.Log($"Finished job: {jobNode.Value.Job.GetType()} @ {jobNode.Value.Job.ChunkPos}");

                _activeJobs.Remove(jobNode);

                ReleaseReservedChunks(jobNode.Value.Job.AffectedChunks);

                if(_activeJobs.Count == 0 && _jobQueue.Count == 0)
                {
                    // Batch finished
                    BatchFinished?.Invoke();
                }
            }
            jobNode = next;
        }
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

    private int? _currentUpdateStage;

    private bool _batching;

    private PlayerController _player;

    private VoxelWorld _world;

    private WorldGenerator _worldGenerator;

    private HashSet<Vector3Int> _reservedChunks = new HashSet<Vector3Int>();

    private PriorityQueue<IWorldUpdateJob, JobPriority> _jobQueue = new PriorityQueue<IWorldUpdateJob, JobPriority>();

    private LinkedList<ActiveJob> _activeJobs = new LinkedList<ActiveJob>();

    private struct ActiveJob
    {
        public IWorldUpdateJob Job { get; set; }

        public Task JobTask { get; set; }
    }
}