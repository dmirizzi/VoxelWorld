using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class ChunkBuilder
{
    private VoxelWorld _world;

    private Chunk _chunk;

    private readonly Material _textureAtlasMaterial;

    private readonly Material _textureAtlasTransparentMaterial;

    private ChunkMesh _solidMesh;

    private ChunkMesh _transparentMesh;

    private Vector3Int _chunkVoxelPos;

    private GameObject _solidChunk;

    public Vector3Int ChunkPos { get; private set; }

    public ChunkBuilder(VoxelWorld world, Vector3Int chunkPos, Chunk chunk, Material textureAtlasMaterial, Material textureAtlasTransparentMaterial)
    {
        _world = world;
        ChunkPos = chunkPos;
        _chunkVoxelPos = VoxelPosHelper.ChunkToBaseVoxelPos(chunkPos);
        _chunk = chunk;
        _textureAtlasMaterial = textureAtlasMaterial;
        _textureAtlasTransparentMaterial = textureAtlasTransparentMaterial;
        _solidMesh = new ChunkMesh();
        _transparentMesh = new ChunkMesh();
    }

    public GameObject[] GetChunkGameObjects()
    {
        _solidChunk = new GameObject($"SolidVoxelMesh");
        _solidChunk.AddComponent<MeshRenderer>();
        var solidMeshFilter = _solidChunk.AddComponent<MeshFilter>().mesh;

        solidMeshFilter.Clear();
        solidMeshFilter.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        solidMeshFilter.vertices = _solidMesh.Vertices.ToArray();
        solidMeshFilter.colors32 = GetLightVertexColors();
        solidMeshFilter.triangles = _solidMesh.Triangles.ToArray();
        solidMeshFilter.normals = _solidMesh.Normals.ToArray();
        solidMeshFilter.uv = _solidMesh.UVCoordinates.ToArray();
        solidMeshFilter.Optimize();

        _solidChunk.GetComponent<Renderer>().material = _textureAtlasMaterial;
        GenerateMeshCollider(_solidChunk);
        _solidChunk.layer = LayerMask.NameToLayer("Voxels");

        var transparentChunk = new GameObject($"TransparentVoxelMesh");
        transparentChunk.AddComponent<MeshRenderer>();
        var transparentMeshFilter = transparentChunk.AddComponent<MeshFilter>().mesh;

        transparentMeshFilter.Clear();
        transparentMeshFilter.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        transparentMeshFilter.vertices = _transparentMesh.Vertices.ToArray();
        transparentMeshFilter.triangles = _transparentMesh.Triangles.ToArray();
        transparentMeshFilter.normals = _transparentMesh.Normals.ToArray();
        transparentMeshFilter.uv = _transparentMesh.UVCoordinates.ToArray();
        transparentMeshFilter.Optimize();

        transparentChunk.GetComponent<Renderer>().material = _textureAtlasTransparentMaterial;
        GenerateMeshCollider(transparentChunk);
        transparentChunk.layer = LayerMask.NameToLayer("Voxels");

        return new GameObject[] {
            _solidChunk,
            transparentChunk
        };
    }


    public Task Build()
    {
        return Task.Run( () => 
        {         
            for(int y = 0; y < VoxelInfo.ChunkSize; ++y)
            {
                for(int x = 0; x < VoxelInfo.ChunkSize; ++x)
                {
                    for(int z = 0; z < VoxelInfo.ChunkSize; ++z)
                    {
                        var voxelType = _chunk.GetVoxel(x, y, z);
                        if(voxelType == 0) continue;

                        var localVoxelPos =  new Vector3Int(x, y, z);
                        var globalVoxelPos = _chunkVoxelPos + localVoxelPos;

                        var renderType = BlockDataRepository.GetBlockData(voxelType).RenderType;
                        if(renderType == BlockRenderType.CustomMesh)
                        {
                            var blockType = BlockTypeRegistry.GetBlockType(voxelType);
                            blockType.OnChunkVoxelMeshBuild(
                                _world,
                                _chunk,
                                voxelType,
                                globalVoxelPos,
                                localVoxelPos,
                                VoxelInfo.IsTransparent(voxelType) ? _transparentMesh : _solidMesh
                            );
                        }
                        else if(renderType == BlockRenderType.Voxel)
                        {
                            AddVoxelVertices(
                                voxelType,
                                globalVoxelPos,
                                localVoxelPos,
                                VoxelInfo.IsTransparent(voxelType) ? _transparentMesh : _solidMesh
                            );
                        }
                    }
                }
            }
        });
    }

    public void UpdateLightVertexColors()
    {
        _solidChunk.GetComponent<MeshFilter>().mesh.colors32 = GetLightVertexColors();
    }

    private Color32[] GetLightVertexColors()
    {
        var colors = new Color32[_solidMesh.Vertices.Count];
/*
        // Blocky lighting model
        for(int ti = 0; ti < _solidMesh.Triangles.Count; ti += 3)
        {
            var vi1 = _solidMesh.Triangles[ti];
            var vi2 = _solidMesh.Triangles[ti + 1];
            var vi3 = _solidMesh.Triangles[ti + 2];

            var v1 = _solidMesh.Vertices[vi1];
            var v2 = _solidMesh.Vertices[vi2];
            var v3 = _solidMesh.Vertices[vi3];

            var normal = _solidMesh.Normals[vi1];

            var lightSamplingPoint = (v1 + v2 + v3) / 3f + normal * 0.5f;

            var localVoxelPos = VoxelPosHelper.GetVoxelPosFromWorldPos(lightSamplingPoint);
            var globalVoxelPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(localVoxelPos, _chunk.ChunkPos);

            var lightVal = _world.GetVoxelLightColor(globalVoxelPos);
            colors[vi1] = lightVal;
            colors[vi2] = lightVal;
            colors[vi3] = lightVal;
        }
*/
        // Smooth lighting model
        for(int vi = 0; vi < _solidMesh.Vertices.Count; ++vi)
        {
            var vp = _solidMesh.Vertices[vi];

            var globalVoxelPos = VoxelPosHelper.ChunkLocalVoxelPosToGlobal(VoxelPosHelper.GetVoxelPosFromWorldPos(vp), _chunk.ChunkPos);
            int r = 0, g = 0, b = 0;
            int numVoxels = 0;
            for(int x = -1; x < 1; ++x)
            {
                for(int z = -1; z < 1; ++z)
                {   
                    for(int y = -1; y < 1; ++y)
                    {   
                        var neighborPos = globalVoxelPos + new Vector3Int(x, y, z);

                        // Enable this check to avoid darker shaded corners/edges (which kind of looks like SSAO)
                        //if(_world.GetVoxel(neighborPos) != 0) continue;

                        var voxelColor = _world.GetVoxelLightColor(neighborPos);
                        r += voxelColor.r;
                        g += voxelColor.g;
                        b += voxelColor.b;
                        numVoxels++;
                    }
                }
            }

            if(numVoxels > 0)
            {
                r /= numVoxels;
                g /= numVoxels;
                b /= numVoxels;
            }
            colors[vi] = new Color32((byte)r, (byte)g, (byte)b, 255);
        }

        return colors;
    }

    private void AddVoxelVertices(
        ushort voxelType, 
        Vector3Int globalVoxelPos,
        Vector3Int localVoxelPos,
        ChunkMesh chunkMesh)
    {
        var heightOffset = _world.GetVoxel(globalVoxelPos + Vector3Int.up) != voxelType ?
            VoxelInfo.GetVoxelHeightOffset(voxelType)
            : 0.0f;

        var voxelCornerVertices = new Vector3[]
        {
                localVoxelPos,
                localVoxelPos + new Vector3(VoxelInfo.VoxelSize, 0, 0),
                localVoxelPos + new Vector3(VoxelInfo.VoxelSize, 0, VoxelInfo.VoxelSize),
                localVoxelPos + new Vector3(0, 0, VoxelInfo.VoxelSize),

                localVoxelPos + new Vector3(0, VoxelInfo.VoxelSize - heightOffset, 0),
                localVoxelPos + new Vector3(VoxelInfo.VoxelSize, VoxelInfo.VoxelSize - heightOffset, 0),
                localVoxelPos + new Vector3(VoxelInfo.VoxelSize, VoxelInfo.VoxelSize - heightOffset, VoxelInfo.VoxelSize),
                localVoxelPos + new Vector3(0, VoxelInfo.VoxelSize - heightOffset, VoxelInfo.VoxelSize)
        };

        var voxelVertices = new List<Vector3>();
        for(int i = 0; i < VoxelInfo.VoxelFaceData.Length; ++i)
        {
            var faceData = VoxelInfo.VoxelFaceData[i];
            if(VoxelBuildHelper.IsVoxelSideVisible(_world, voxelType, globalVoxelPos, faceData.Direction))
            {
                voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[0]]);
                voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[1]]);
                voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[2]]);
                voxelVertices.Add(voxelCornerVertices[faceData.VertexIndices[3]]);

                chunkMesh.Normals.AddRange(faceData.Normals);

                var uv = VoxelBuildHelper.GetUVsForVoxelType(voxelType, faceData.VoxelFace);
                chunkMesh.UVCoordinates.Add(uv[faceData.UVIndices[0]]);
                chunkMesh.UVCoordinates.Add(uv[faceData.UVIndices[1]]);
                chunkMesh.UVCoordinates.Add(uv[faceData.UVIndices[2]]);
                chunkMesh.UVCoordinates.Add(uv[faceData.UVIndices[3]]);
            }
        }

        int vertexBaseIdx = chunkMesh.Vertices.Count;
        chunkMesh.Vertices.AddRange(voxelVertices);

        for(int i = 0; i < voxelVertices.Count; i += 4)
        {
            chunkMesh.Triangles.Add(i + vertexBaseIdx);
            chunkMesh.Triangles.Add(i + 1 + vertexBaseIdx);
            chunkMesh.Triangles.Add(i + 2 + vertexBaseIdx);
            chunkMesh.Triangles.Add(i + 2 + vertexBaseIdx);
            chunkMesh.Triangles.Add(i + 3 + vertexBaseIdx);
            chunkMesh.Triangles.Add(i + vertexBaseIdx);
        }
    }

    private void GenerateMeshCollider(GameObject chunkObject)
    {
        var collider = chunkObject.AddComponent<MeshCollider>();
    }
}
