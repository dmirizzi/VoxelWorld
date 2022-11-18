using System;
using UnityEngine;

// This class basically just copies the mesh data from a unity mesh
// so it can be used on a worker thread
public class VoxelMesh
{
    public Vector3[] Vertices { get; }
    
    public Vector3[] Normals { get; }

    public Vector2[] UVs { get; }

    public int[] Triangles { get; }


    public VoxelMesh(Mesh mesh)
    {
        Vertices = new Vector3[mesh.vertices.Length];
        Array.Copy(mesh.vertices, Vertices, mesh.vertices.Length);

        Normals = new Vector3[mesh.normals.Length];
        Array.Copy(mesh.normals, Normals, mesh.normals.Length);

        UVs = new Vector2[mesh.uv.Length];
        Array.Copy(mesh.uv, UVs, mesh.uv.Length);

        Triangles = new int[mesh.triangles.Length];
        Array.Copy(mesh.triangles, Triangles, mesh.triangles.Length);

    }
}