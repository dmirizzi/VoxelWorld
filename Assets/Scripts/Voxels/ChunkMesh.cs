using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ChunkMesh
{
    public ChunkMesh()
    {
        _vertices = new List<Vector3>();
        _normals = new List<Vector3>();
        _uvCoordinates = new List<Vector2>();
        _triangles = new List<int>();
    }

    public IReadOnlyList<Vector3> Vertices => _vertices;

    public IReadOnlyList<Vector3> Normals => _normals;
    
    public IReadOnlyList<Vector2> UVCoordinates => _uvCoordinates;

    public IReadOnlyList<int> Triangles => _triangles;

    public void AddMesh(VoxelMesh mesh, Vector3 localBasePos, Vector3 dir)
    {
        int vertexBaseIdx = _vertices.Count;

        mesh.PointTowards(dir);
        mesh.Translate(localBasePos + new Vector3(0.5f, 0f, 0.5f));

        _vertices.AddRange(mesh.Vertices);
        _normals.AddRange(mesh.Normals);
        _uvCoordinates.AddRange(mesh.UVs);
        _triangles.AddRange(mesh.Triangles.Select( idx => idx + vertexBaseIdx));
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

        _vertices.Add(vertices[0]);
        _vertices.Add(vertices[1]);
        _vertices.Add(vertices[2]);
        _vertices.Add(vertices[3]);

        _normals.AddRange(Enumerable.Repeat(normal, 4));

        _uvCoordinates.Add(uvCoordinates[0]);
        _uvCoordinates.Add(uvCoordinates[1]);
        _uvCoordinates.Add(uvCoordinates[2]);
        _uvCoordinates.Add(uvCoordinates[3]);

        _triangles.Add(vertexBaseIdx);
        _triangles.Add(vertexBaseIdx + 1);
        _triangles.Add(vertexBaseIdx + 2);
        _triangles.Add(vertexBaseIdx + 2);
        _triangles.Add(vertexBaseIdx + 3);
        _triangles.Add(vertexBaseIdx);
    }

    public void AddTriangle(Vector3[] vertices, Vector2[] uvCoordinates, Vector3 normal)
    {
        int vertexBaseIdx = Vertices.Count;

        _vertices.Add(vertices[0]);
        _vertices.Add(vertices[1]);
        _vertices.Add(vertices[2]);

        _normals.AddRange(Enumerable.Repeat(normal, 3));

        _uvCoordinates.Add(uvCoordinates[0]);
        _uvCoordinates.Add(uvCoordinates[1]);
        _uvCoordinates.Add(uvCoordinates[2]);

        _triangles.Add(vertexBaseIdx);
        _triangles.Add(vertexBaseIdx + 1);
        _triangles.Add(vertexBaseIdx + 2);
    }

    public void AddVertices(IEnumerable<Vector3> vertices) => _vertices.AddRange(vertices);

    public void AddNormals(IEnumerable<Vector3> normals) => _normals.AddRange(normals);

    public void AddTriangleIndex(int index) => _triangles.Add(index);

    public void AddUVCoordinate(Vector2 uvCoordinate) => _uvCoordinates.Add(uvCoordinate);

    public Vector3[] GetVerticesArray() => _vertices.ToArray();

    public Vector3[] GetNormalsArray() => _normals.ToArray();

    public Vector2[] GetUVArray() => _uvCoordinates.ToArray();

    public int[] GetTrianglesArray() => _triangles.ToArray();

    private List<Vector3> _vertices;

    private List<Vector3> _normals;
    
    private List<Vector2> _uvCoordinates;

    private List<int> _triangles;

}