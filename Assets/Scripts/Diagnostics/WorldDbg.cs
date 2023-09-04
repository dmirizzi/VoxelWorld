using System;
using UnityEngine;

public static class WorldDbg
{
    public static GizmosDispatcher SetDispatcher(GizmosDispatcher dispatcher) => _dispatcher = dispatcher;

    public static void AddVoxelGizmo(Vector3Int chunkPos, string tag, Vector3 worldPos, float size, Color color)
    {
        try
        {
            if(_dispatcher != null)
            {
                _dispatcher.AddGizmoToChunk( () => 
                {
                    var newObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    newObj.tag = tag;

                    newObj.transform.position = VoxelPosHelper.WorldPosToGlobalVoxelPos(worldPos);
                    newObj.transform.localScale = new Vector3(size, size, size);

                    var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    mat.color = color;
                    newObj.GetComponent<Renderer>().material = mat;

                    return newObj;
                }, chunkPos);
            }
        }
        catch(Exception e)
        {
            Debug.LogException(e);
        }
    }

    public static void RemoveGizmosWithTag(string tag) 
    {
        if(_dispatcher != null)
        {
            _dispatcher.RemoveAllWithTag(tag);
        }
    }

    private static GizmosDispatcher _dispatcher;
}