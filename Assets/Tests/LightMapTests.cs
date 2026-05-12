using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class LightMapTests
{
    private GameObject _worldGo;

    [SetUp]
    public void SetUp()
    {
        _worldGo = new GameObject("TestWorld");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_worldGo);
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Test]
    public void SunlightAboveFlatPlane_IsMaxLevel()
    {
        var world = CreateWorld();
        var groundChunk = world.GetOrCreateChunk(new Vector3Int(0, 0, 0));
        FillChunkWithVoxel(groundChunk, 1); // Grass — opaque on all faces
        var skyChunk = world.GetOrCreateChunk(new Vector3Int(0, 1, 0));

        RunSunlight(world, skyChunk);

        AssertSunlightInChunk(skyChunk, 15);
    }

    [Test]
    public void SunlightHollowCube_AboveIsMax_InsideIsZero()
    {
        var world = CreateWorld();
        var skyChunk = world.GetOrCreateChunk(new Vector3Int(0, 1, 0));
        var groundChunk = world.GetOrCreateChunk(new Vector3Int(0, 0, 0));

        // Hollow cube: outer shell [2..13], interior [3..12] on each axis.
        const int outerMin = 2;
        const int outerMax = 13;
        FillHollowBox(groundChunk,
            new Vector3Int(outerMin, outerMin, outerMin),
            new Vector3Int(outerMax, outerMax, outerMax),
            voxelType: 1); // Grass

        RunSunlight(world, skyChunk);

        AssertSunlightInVolume(groundChunk,
            new Vector3Int(outerMin + 1, outerMax + 1, outerMin + 1),
            new Vector3Int(outerMax - 1, VoxelInfo.ChunkSize - 1, outerMax - 1),
            expected: 15, "above hollow cube");

        AssertSunlightInVolume(groundChunk,
            new Vector3Int(outerMin + 1, outerMin + 1, outerMin + 1),
            new Vector3Int(outerMax - 1, outerMax - 1, outerMax - 1),
            expected: 0, "inside hollow cube");
    }

    [Test]
    public void SunlightMultiChunkPlane_SkyChunksAllMaxLevel()
    {
        const int planeSize = 10;
        var world = CreateWorld();

        // Ground and sky planes must be created before running sunlight so all
        // chunk neighbors are wired up before light propagation begins.
        CreateChunkPlane(world, y: 0, sizeX: planeSize, sizeZ: planeSize, voxelType: 1);
        var skyChunks = CreateChunkPlane(world, y: 1, sizeX: planeSize, sizeZ: planeSize);

        RunSunlightForPlane(world, skyChunks);

        AssertSunlightInChunkPlane(skyChunks, expected: 15);
    }

    // -------------------------------------------------------------------------
    // World setup helpers
    // -------------------------------------------------------------------------

    private VoxelWorld CreateWorld() => _worldGo.AddComponent<VoxelWorld>();

    // Creates an sizeX x sizeZ grid of chunks at the given chunk-space y, starting at (0, y, 0).
    // Fills each chunk with voxelType when non-zero; leaves them as air otherwise.
    private Chunk[,] CreateChunkPlane(VoxelWorld world, int y, int sizeX, int sizeZ, ushort voxelType = 0)
    {
        var chunks = new Chunk[sizeX, sizeZ];
        for (int cx = 0; cx < sizeX; cx++)
            for (int cz = 0; cz < sizeZ; cz++)
            {
                chunks[cx, cz] = world.GetOrCreateChunk(new Vector3Int(cx, y, cz));
                if (voxelType != 0)
                    FillChunkWithVoxel(chunks[cx, cz], voxelType);
            }
        return chunks;
    }

    // Runs vertical sunlight for a single XZ column. Spillover is discarded —
    // use RunSunlightForPlane when chunk boundaries matter.
    private void RunSunlight(VoxelWorld world, Chunk topChunk)
    {
        world.GetLightMap().UpdateSunlightColumnVertical(topChunk, new List<LightMap.LightNode>());
    }

    // Runs vertical sunlight for every column in the plane, then propagates
    // the collected spillover so light crosses XZ chunk boundaries correctly.
    private void RunSunlightForPlane(VoxelWorld world, Chunk[,] topChunks)
    {
        var spillover = new List<LightMap.LightNode>();
        int sizeX = topChunks.GetLength(0);
        int sizeZ = topChunks.GetLength(1);
        for (int cx = 0; cx < sizeX; cx++)
            for (int cz = 0; cz < sizeZ; cz++)
                world.GetLightMap().UpdateSunlightColumnVertical(topChunks[cx, cz], spillover);
        world.GetLightMap().PropagateSpilloverNodes(spillover, new HashSet<Vector3Int>());
    }

    private void FillChunkWithVoxel(Chunk chunk, ushort voxelType)
    {
        var buffer = new ushort[VoxelInfo.ChunkSize, VoxelInfo.ChunkSize, VoxelInfo.ChunkSize];
        for (int x = 0; x < VoxelInfo.ChunkSize; x++)
            for (int y = 0; y < VoxelInfo.ChunkSize; y++)
                for (int z = 0; z < VoxelInfo.ChunkSize; z++)
                    buffer[x, y, z] = voxelType;
        chunk.PopulateFromBuffer(buffer);
    }

    // Fills the 1-voxel-thick shell of an axis-aligned box (min/max inclusive).
    private void FillHollowBox(Chunk chunk, Vector3Int min, Vector3Int max, ushort voxelType)
    {
        for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
                for (int z = min.z; z <= max.z; z++)
                {
                    if (x == min.x || x == max.x ||
                        y == min.y || y == max.y ||
                        z == min.z || z == max.z)
                        chunk.SetVoxel(new Vector3Int(x, y, z), voxelType);
                }
    }

    // -------------------------------------------------------------------------
    // Assertion helpers
    // -------------------------------------------------------------------------

    private void AssertSunlightInChunk(Chunk chunk, byte expected) =>
        AssertSunlightInVolume(chunk,
            Vector3Int.zero,
            Vector3Int.one * (VoxelInfo.ChunkSize - 1),
            expected);

    private void AssertSunlightInChunkPlane(Chunk[,] chunks, byte expected)
    {
        int sizeX = chunks.GetLength(0);
        int sizeZ = chunks.GetLength(1);
        for (int cx = 0; cx < sizeX; cx++)
            for (int cz = 0; cz < sizeZ; cz++)
                AssertSunlightInChunk(chunks[cx, cz], expected);
    }

    // Asserts the sunlight channel value for every voxel in [min..max] (inclusive).
    private void AssertSunlightInVolume(Chunk chunk, Vector3Int min, Vector3Int max, byte expected, string context = "")
    {
        string prefix = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
        for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
                for (int z = min.z; z <= max.z; z++)
                {
                    var sunlight = chunk.GetLightChannelValue(new Vector3Int(x, y, z), Chunk.SunlightChannel);
                    Assert.AreEqual(expected, sunlight, $"{prefix}sunlight at ({x},{y},{z})");
                }
    }
}
