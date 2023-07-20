using System;
using System.Collections.Generic;
using UnityEngine;

public class GizmosDispatcher : MonoBehaviour
{

    public void AddGizmoToChunk(Func<GameObject> gameObjectFactory, Vector3Int chunkPos)
    {
        lock(_lockObject)
        {
            _chunkGizmoCreationQueue.Enqueue((gameObjectFactory, chunkPos));
        }
    }

    public void RemoveAllWithTag(string tag)
    {
        _deletionByTagQueue.Enqueue(tag);
    }

    void Awake()
    {
        WorldDbg.SetDispatcher(this);
    }

    void Start()
    {
        WorldDbg.SetDispatcher(this);

        _world = GameObject.FindObjectOfType<VoxelWorld>();
    }

    void Update()
    {
        lock(_lockObject)
        {
            try
            {
                while(_chunkGizmoCreationQueue.TryDequeue(out var creation))
                {
                    var parent = _world.GetChunk(creation.ChunkPos).ChunkGameObject.transform;

                    var newGameObj = creation.Factory();
                    newGameObj.transform.parent = GetOrCreateTagParent(parent, newGameObj.tag);
                }

                while(_deletionByTagQueue.TryDequeue(out var tag))
                {
                    var objs = GameObject.FindGameObjectsWithTag(tag);
                    foreach(var obj in objs)
                    {
                        Destroy(obj);
                    }
                }
            }
            catch(Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    private Transform GetOrCreateTagParent(Transform parent, string tag)
    {
        var tagParent = parent.Find(tag);
        if(tagParent != null)
        {
            return tagParent.transform;
        }
    
        var newTagParent = new GameObject($"Gizmo_{tag}");
        newTagParent.transform.parent = parent;
        newTagParent.SetActive(false);

        return newTagParent.transform;
    }

    private Queue<(Func<GameObject> Factory, Vector3Int ChunkPos)> _chunkGizmoCreationQueue = new Queue<(Func<GameObject> Factory, Vector3Int ChunkPos)>();

    private Queue<Func<GameObject>> _gizmoCreationQueue = new Queue<Func<GameObject>>();

    private Queue<string> _deletionByTagQueue = new Queue<string>();

    private object _lockObject = new object();

    private VoxelWorld _world;
}
