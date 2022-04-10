using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ChunkMesh
{
    public ChunkMesh()
    {
        Vertices = new List<Vector3>();
        Normals = new List<Vector3>();
        UVCoordinates = new List<Vector2>();
        Triangles = new List<int>();
    }

    public List<Vector3> Vertices { get; }

    public List<Vector3> Normals { get; }

    public List<Vector2> UVCoordinates { get; }

    public List<int> Triangles { get; }

    public void AddQuad(Vector3[] vertices, Vector2[] uvCoordinates)
    {
        var normal = Vector3.Cross(
            vertices[1] - vertices[0],
            vertices[2] - vertices[1]
        );

        AddQuad(vertices, uvCoordinates, normal);
    }

    public void AddTriangle(Vector3[] vertices, Vector2[] uvCoordinates)
    {
        var normal = Vector3.Cross(
            vertices[1] - vertices[0],
            vertices[2] - vertices[1]
        );

        AddTriangle(vertices, uvCoordinates, normal);
    }

    public void AddQuad(Vector3[] vertices, Vector2[] uvCoordinates, Vector3 normal)
    {
        int vertexBaseIdx = Vertices.Count;

        Vertices.Add(vertices[0]);
        Vertices.Add(vertices[1]);
        Vertices.Add(vertices[2]);
        Vertices.Add(vertices[3]);

        Normals.AddRange(Enumerable.Repeat(normal, 4));

        UVCoordinates.Add(uvCoordinates[0]);
        UVCoordinates.Add(uvCoordinates[1]);
        UVCoordinates.Add(uvCoordinates[2]);
        UVCoordinates.Add(uvCoordinates[3]);

        Triangles.Add(vertexBaseIdx);
        Triangles.Add(vertexBaseIdx + 1);
        Triangles.Add(vertexBaseIdx + 2);
        Triangles.Add(vertexBaseIdx + 2);
        Triangles.Add(vertexBaseIdx + 3);
        Triangles.Add(vertexBaseIdx);
    }

    public void AddTriangle(Vector3[] vertices, Vector2[] uvCoordinates, Vector3 normal)
    {
        int vertexBaseIdx = Vertices.Count;

        Vertices.Add(vertices[0]);
        Vertices.Add(vertices[1]);
        Vertices.Add(vertices[2]);

        Normals.AddRange(Enumerable.Repeat(normal, 3));

        UVCoordinates.Add(uvCoordinates[0]);
        UVCoordinates.Add(uvCoordinates[1]);
        UVCoordinates.Add(uvCoordinates[2]);

        Triangles.Add(vertexBaseIdx);
        Triangles.Add(vertexBaseIdx + 1);
        Triangles.Add(vertexBaseIdx + 2);
    }
}