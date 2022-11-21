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

    public void AddMesh(VoxelMesh mesh, Vector3 localBasePos, Vector3 dir)
    {
        int vertexBaseIdx = Vertices.Count;

        mesh.PointTowards(dir);
        mesh.Translate(localBasePos + new Vector3(0.5f, 0f, 0.5f));

        Vertices.AddRange(mesh.Vertices);
        Normals.AddRange(mesh.Normals);
        UVCoordinates.AddRange(mesh.UVs);
        Triangles.AddRange(mesh.Triangles.Select( idx => idx + vertexBaseIdx));
    }

    public void AddBox(Vector3 centerPos, Vector3 size, Vector2[] uvCoordinates, Vector3 direction)
    {
        var cornerVertices = new Vector3[]
        {
            new Vector3(-size.x, -size.y, -size.z),
            new Vector3(+size.x, -size.y, -size.z),
            new Vector3(+size.x, -size.y, +size.z),
            new Vector3(-size.x, -size.y, +size.z),
            new Vector3(-size.x, +size.y, -size.z),
            new Vector3(+size.x, +size.y, -size.z),
            new Vector3(+size.x, +size.y, +size.z),
            new Vector3(-size.x, +size.y, +size.z)
        };

        VoxelBuildHelper.PointVerticesTowards(cornerVertices, direction);

        for(int i = 0; i < cornerVertices.Length; ++i)
        {
            cornerVertices[i] += centerPos;
        }

        // Top
        AddQuad(
            new Vector3[] { cornerVertices[4], cornerVertices[7], cornerVertices[6], cornerVertices[5] },
            new Vector2[] { uvCoordinates[2], uvCoordinates[0], uvCoordinates[1], uvCoordinates[3] }
        );

        // Bottom
        AddQuad(
            new Vector3[] { cornerVertices[0], cornerVertices[1], cornerVertices[2], cornerVertices[3] },
            new Vector2[] { uvCoordinates[0], uvCoordinates[1], uvCoordinates[3], uvCoordinates[2] }            
        );

        // Front
        AddQuad(
            new Vector3[] { cornerVertices[0], cornerVertices[4], cornerVertices[5], cornerVertices[1] },
            new Vector2[] { uvCoordinates[2], uvCoordinates[0], uvCoordinates[1], uvCoordinates[3] }
        );

        // Back
        AddQuad(
            new Vector3[] { cornerVertices[3], cornerVertices[2], cornerVertices[6], cornerVertices[7] },
            new Vector2[] { uvCoordinates[3], uvCoordinates[2], uvCoordinates[0], uvCoordinates[1] }
        );

        // Left
        AddQuad(
            new Vector3[] { cornerVertices[7], cornerVertices[4], cornerVertices[0], cornerVertices[3] },
            new Vector2[] { uvCoordinates[0], uvCoordinates[1], uvCoordinates[2], uvCoordinates[3] }
        );

        // Right
        AddQuad(
            new Vector3[] { cornerVertices[5], cornerVertices[6], cornerVertices[2], cornerVertices[1] },
            new Vector2[] { uvCoordinates[0], uvCoordinates[1], uvCoordinates[3], uvCoordinates[2] }
        );
    }

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