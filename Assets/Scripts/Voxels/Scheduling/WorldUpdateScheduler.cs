/*

    - Chunk Mesh Rebuild
        - Mesh Build
        - Game Object Creation

    - Light Map Update

    - Chunk Light Mapping Update

    - Scheduling criteria
        - Which jobs can be executed simultaneously for same chunk
        - Find jobs for unlocked chunks
            - 
        - Max allowed num threads
        - Proximity of processed chunks to player (closer first)

    - Job Type Sequence No.
        1. Chunk Rebuild
        2. Light Map Update
        3. Chunk Light Mapping Update

    - AddJob
        - Insert into joblist by priority -> priority = Distance to Player + Job Type Sequence No.
        - Potential Optimization: Alternate closeby and far away chunks to reduce unnecessary locked chunk checking

    - AddHighPriorityJob -> Job that will be scheduled immediately (i.e. player placed/removed block)


*/

/*
    [ ] Move ChunkBuilders to ChunkUpdateScheduler (?)
    [ ] Update Stage per Chunk / Sort of Chunk Processing Pipeline
        Stage1 - Generate Chunks / Build Chunks
        Stage2 - Process Light Map Updates
            - Track affected chunks per light map update
        Stage3 - Any Chunk that is built & has no more pending light map updates -> Light Mapping Update
    [ ] Add Chunk GameObjects at the end when its fully built & lit
        and remove old chunk (if replaced) right before that (i.e. in the same frame)

*/

using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

class WorldUpdateScheduler : MonoBehaviour
{
    public int MaxNumSimultaneousJobs = 8;

    public void Awake()
    {
        _player = FindObjectOfType<PlayerController>();
        _world = FindObjectOfType<VoxelWorld>();
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
        /*

        int i = 0;
        foreach(var item in _queuedJobs)
        {
            //GUI.Label(new Rect(10, 100 + i * 20, 300, 20), $"({item.Priority.JobTypePriority}|{item.Priority.DistanceToPlayer}) {item.Value.GetType()} @ {item.Value.ChunkPos}");
            GUI.Label(new Rect(10, 100 + i * 20, 300, 20), $"{item.GetType()} @ {item.ChunkPos} | {item.GetHashCode()}");
            i++;
        }

        GUI.Label(new Rect(10, 140 + i * 20, 1500, 20), $"Active Jobs ({_activeJobs.Count}):");
        foreach(var item in _activeJobs)
        {
            GUI.Label(new Rect(10, 100 + i * 20, 300, 20), $"{item.Job.GetType()} @ {item.Job.ChunkPos}");
            i++;
        }*/
    }

    public void StartBatch() => _batching = true;

    public void FinishBatch() => _batching = false;

    public void AddChunkRebuildJob(Vector3Int chunkPos)
    {
        if(!_world.ChunkExists(chunkPos)) return;
        AddJob(new ChunkRebuildJob(chunkPos));
    }

    public void AddBlockLightUpateJob(Vector3Int chunkPos, Vector3Int lightPos, Color32 lightColor, bool addLight)
    {
        if(!_world.ChunkExists(chunkPos)) return;
        AddJob(new BlockLightUpdateJob(chunkPos, lightPos, lightColor, addLight));
    }

    public void AddChunkLightMappingUpdateJob(Vector3Int chunkPos)
    {
        if(!_world.ChunkExists(chunkPos)) return;
         AddJob(new ChunkLightMappingUpdateJob(chunkPos));
    }

    public void AddJob(IWorldUpdateJob job)
    {        
        _jobQueue.EnqueueUnique(
            job,
            new JobPriority{
                JobTypePriority = job.UpdateStage,
                DistanceToPlayer = VoxelPosHelper.GetChunkSqrDistanceToWorldPos(_player.transform.position, job.ChunkPos)
            }
        );
    }

    private void ScheduleJobs()
    {
        // Schedule jobs that do not overlap in which chunks they affect
        while(_activeJobs.Count < MaxNumSimultaneousJobs 
            && _jobQueue.DequeueNext(x => CanReserveChunks(x.AffectedChunks), out var job))
        {
            if(!job.PreExecuteSync(_world))
            {
                // Pre execution failed -> job is not executable -> skip
                continue;
            }

            ReserveChunks(job.AffectedChunks);

            _activeJobs.AddLast((job, job.ExecuteAsync()));
        }
    }

    private bool CanReserveChunks(HashSet<Vector3Int> chunksToBeReserved)
    {
        return !_reservedChunks.Overlaps(chunksToBeReserved);
    }

    private void ReserveChunks(HashSet<Vector3Int> chunksToBeReserved)
    {
        foreach(var chunk in chunksToBeReserved)
        {
            _reservedChunks.Add(chunk);
        }
    }

    private void HandleActiveJobs()
    {
        var node = _activeJobs.First;
        while(node != null)
        {
            var next = node.Next;
            if (node.Value.JobTask.IsCompleted)
            {
                node.Value.Job.PostExecuteSync(_world);

                _activeJobs.Remove(node);
                foreach(var chunk in node.Value.Job.AffectedChunks)
                {
                    _reservedChunks.Remove(chunk);
                }
            }
            node = next;
        }
    }

    private int? _currentUpdateStage;

    private bool _batching;

    private PlayerController _player;

    private VoxelWorld _world;

    private HashSet<Vector3Int> _reservedChunks = new HashSet<Vector3Int>();

    private PriorityQueue<IWorldUpdateJob, JobPriority> _jobQueue = new PriorityQueue<IWorldUpdateJob, JobPriority>();

    private LinkedList<(IWorldUpdateJob Job, Task JobTask)> _activeJobs = new LinkedList<(IWorldUpdateJob, Task)>();
}