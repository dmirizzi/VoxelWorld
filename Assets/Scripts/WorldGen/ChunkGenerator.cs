using System;
using UnityEngine;

public class ChunkGenerator
{
    private const int SeaLevel = -30;
    private const float ForestTreeDensity = 0.35f;  // peak per-tile tree chance inside a dense forest cluster
    private const float TorchChance   = 0.005f;
    private const float CaveChance    = 0.0003f;
    private const int DirtLayerDepth  = 24;

    public ChunkGenerator(int seed = 123456789)
    {
        _dirtType        = BlockDataRepository.GetBlockTypeId("Dirt");
        _grassType       = BlockDataRepository.GetBlockTypeId("Grass");
        _logType         = BlockDataRepository.GetBlockTypeId("Log");
        _leavesType      = BlockDataRepository.GetBlockTypeId("Leaves");
        _waterType       = BlockDataRepository.GetBlockTypeId("Water");
        _cobblestoneType = BlockDataRepository.GetBlockTypeId("Cobblestone");
        _torchType       = BlockDataRepository.GetBlockTypeId("Torch");

        _seed = seed;
        var rng = new System.Random(seed);
        _noiseOffsetX  = (float)(rng.NextDouble() * 10000.0);
        _noiseOffsetZ  = (float)(rng.NextDouble() * 10000.0);
        _caveGenerator = new WormCaveGenerator(seed, WormCaveParams.Default);
    }

    public ChunkUpdate GenerateChunk(Vector3Int chunkPos)
    {
        var builder      = new ChunkUpdateBuilder(chunkPos);
        var chunkBasePos = VoxelPosHelper.ChunkPosToGlobalChunkBaseVoxelPos(chunkPos);

        for (int z = 0; z < VoxelInfo.ChunkSize; ++z)
        {
            for (int x = 0; x < VoxelInfo.ChunkSize; ++x)
            {
                int globalX      = chunkBasePos.x + x;
                int globalZ      = chunkBasePos.z + z;
                int terrainHeight = GetTerrainHeight(globalX, globalZ);
                bool underwater  = terrainHeight < SeaLevel;

                for (int y = 0; y < VoxelInfo.ChunkSize; ++y)
                {
                    int globalY = chunkBasePos.y + y;

                    if (globalY < terrainHeight)
                    {
                        int depth = terrainHeight - globalY;
                        builder.QueueVoxelInChunk(x, y, z,
                            depth <= DirtLayerDepth ? _dirtType : _cobblestoneType);
                    }
                    else if (globalY == terrainHeight)
                    {
                        builder.QueueVoxelInChunk(x, y, z, underwater ? _dirtType : _grassType);
                    }
                    else if (globalY > terrainHeight && globalY <= SeaLevel)
                    {
                        builder.QueueVoxelInChunk(x, y, z, _waterType);
                    }
                }

                // Surface features — emit only from the chunk that contains terrainHeight
                bool surfaceInThisChunk = terrainHeight >= chunkBasePos.y &&
                                          terrainHeight <  chunkBasePos.y + VoxelInfo.ChunkSize;
                if (!underwater && surfaceInThisChunk)
                {
                    int localY      = terrainHeight - chunkBasePos.y;
                    var surfacePos  = new Vector3Int(x, localY, z);

                    if (ShouldPlaceFeature(globalX, globalZ, GetTreeChance(globalX, globalZ)))
                    {
                        var treeRng = new System.Random(HashPos(globalX, globalZ));
                        PlaceTree(builder, surfacePos, treeRng);
                    }
                    else if (ShouldPlaceFeature(globalX ^ 0xCAFE, globalZ ^ 0xF00D, TorchChance))
                    {
                        builder.QueueVoxel(surfacePos + Vector3Int.up, _torchType);
                    }

                    if (ShouldPlaceFeature(globalX ^ 0xC0DE, globalZ ^ 0xC0DE, CaveChance))
                    {
                        var caveRng = new System.Random(HashPos(globalX ^ 0xBEEF, globalZ ^ 0xDEAD));
                        _caveGenerator.GenerateCave(builder, chunkBasePos, globalX, globalZ, terrainHeight, caveRng);
                    }
                }
            }
        }

        return builder.GetChunkUpdate();
    }

    private int GetTerrainHeight(int globalX, int globalZ)
    {
        float nx = globalX + _noiseOffsetX;
        float nz = globalZ + _noiseOffsetZ;

        float continental    = Mathf.PerlinNoise(nx / 600f, nz / 600f);
        float continentalBias = (continental * continental - 0.25f) * 55f;
        float roughness      = Mathf.PerlinNoise(nx / 280f + 100f, nz / 280f + 100f);

        float h = continentalBias;
        h += Mathf.PerlinNoise(nx / 250f, nz / 250f) * 60f;
        h += Mathf.PerlinNoise(nx /  80f, nz /  80f) * (12f + roughness * 24f);
        h += Mathf.PerlinNoise(nx /  30f, nz /  30f) *  8f;
        h += Mathf.PerlinNoise(nx /  10f, nz /  10f) *  2f;
        h -= 50f;
        return Mathf.RoundToInt(h);
    }

    // Returns per-position tree spawn probability based on two-layer forest noise.
    // The product of a large-region noise and a local-cluster noise only exceeds zero
    // where both are simultaneously elevated, producing irregular organic cluster shapes.
    private float GetTreeChance(int globalX, int globalZ)
    {
        float nx = globalX + _noiseOffsetX + 4321f;
        float nz = globalZ + _noiseOffsetZ + 8765f;

        float region  = Mathf.PerlinNoise(nx / 200f, nz / 200f);
        float cluster = Mathf.PerlinNoise(nx /  65f, nz /  65f);

        float regionFactor  = Mathf.Max(0f, region  - 0.44f) * (1f / 0.56f);
        float clusterFactor = Mathf.Max(0f, cluster - 0.38f) * (1f / 0.62f);

        return 0.003f + regionFactor * clusterFactor * ForestTreeDensity;
    }

    // Ellipsoid tree crown — more natural than a cube, still cheap to compute
    private void PlaceTree(ChunkUpdateBuilder builder, Vector3Int localRootPos, System.Random rng)
    {
        int trunkHeight = rng.Next(4, 8);
        int crownR      = rng.Next(2, 4);

        for (int ty = 1; ty <= trunkHeight; ++ty)
            builder.QueueVoxel(localRootPos + Vector3Int.up * ty, _logType);

        Vector3Int crownCenter = localRootPos + Vector3Int.up * (trunkHeight + crownR - 1);
        int rx = crownR;
        int ry = Mathf.Max(1, crownR - 1);
        int rz = crownR;

        for (int ty = -ry; ty <= ry; ++ty)
        for (int tz = -rz; tz <= rz; ++tz)
        for (int tx = -rx; tx <= rx; ++tx)
        {
            float ex = (float)tx / rx;
            float ey = (float)ty / ry;
            float ez = (float)tz / rz;
            if (ex * ex + ey * ey + ez * ez <= 1.0f)
                builder.QueueVoxel(crownCenter + new Vector3Int(tx, ty, tz), _leavesType);
        }
    }

    private bool ShouldPlaceFeature(int x, int z, float chance)
    {
        return new System.Random(HashPos(x, z)).NextDouble() < chance;
    }

    private int HashPos(int x, int z)
    {
        unchecked
        {
            uint h = (uint)_seed;
            h ^= (uint)x; h *= 0x9e3779b9u; h ^= h >> 16;
            h ^= (uint)z; h *= 0x85ebca6bu; h ^= h >> 13;
                           h *= 0xc2b2ae35u; h ^= h >> 16;
            return (int)h;
        }
    }

    private readonly int   _seed;
    private readonly float _noiseOffsetX;
    private readonly float _noiseOffsetZ;

    private readonly WormCaveGenerator _caveGenerator;

    private readonly ushort _torchType;
    private readonly ushort _dirtType;
    private readonly ushort _grassType;
    private readonly ushort _logType;
    private readonly ushort _leavesType;
    private readonly ushort _waterType;
    private readonly ushort _cobblestoneType;
}
