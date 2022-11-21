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

    public VoxelMesh(VoxelMesh rhs)
    {
        Vertices = new Vector3[rhs.Vertices.Length];
        Array.Copy(rhs.Vertices, Vertices, rhs.Vertices.Length);
        Normals = new Vector3[rhs.Normals.Length];
        Array.Copy(rhs.Normals, Normals, rhs.Normals.Length);
        UVs = new Vector2[rhs.UVs.Length];
        Array.Copy(rhs.UVs, UVs, rhs.UVs.Length);
        Triangles = new int[rhs.Triangles.Length];
        Array.Copy(rhs.Triangles, Triangles, rhs.Triangles.Length);
    }

    public VoxelMesh Clone()
    {
        return new VoxelMesh(this);
    }

    public void PointTowards(Vector3 direction)
    {
        var rotation = Quaternion.FromToRotation(Vector3.forward, direction);
        rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);

        for(int i = 0; i < Vertices.Length; ++i)
        {
            Vertices[i] = rotation * Vertices[i];
        }
    }

    public void RotateAround(Vector3 pivotPoint, Vector3 axis, float angle)
    {
        var rotation = Quaternion.AngleAxis(angle, axis);

        for(int i = 0; i < Vertices.Length; ++i)
        {
            Vertices[i] = rotation * (Vertices[i] - pivotPoint) + pivotPoint;
        }
    }

    public void Translate(Vector3 translation)
    {
        for(int i = 0; i < Vertices.Length; ++i)
        {
            Vertices[i] = Vertices[i] + translation;
        }
    }

    public Bounds CalculateBounds()
    {
        float minX = float.MaxValue,
              minY = float.MaxValue,
              minZ = float.MaxValue;

        float maxX = float.MinValue,
              maxY = float.MinValue,
              maxZ = float.MinValue;

        for(int i = 0; i < Vertices.Length; ++i)
        {
            if(Vertices[i].x < minX) minX = Vertices[i].x;
            if(Vertices[i].y < minY) minY = Vertices[i].y;
            if(Vertices[i].z < minZ) minZ = Vertices[i].z;

            if(Vertices[i].x > maxX) maxX = Vertices[i].x;
            if(Vertices[i].y > maxY) maxY = Vertices[i].y;
            if(Vertices[i].z > maxZ) maxZ = Vertices[i].z;
        }

        var min = new Vector3(minX, minY, minZ);
        var max = new Vector3(maxX, maxY, maxZ);
        var center = (min + max) / 2;

        return new Bounds(center, (max - min));
    }
}