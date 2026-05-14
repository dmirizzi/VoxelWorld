using System;
using UnityEngine;

public class ChunkGenerator
{
    private const int SeaLevel = -30;
    private const float ForestTreeDensity = 0.35f;  // peak per-tile tree chance inside a dense forest cluster
    private const float CrystalChance     = 0.003f;
    private const float TorchChance       = 0.005f;
    private const int DirtLayerDepth = 4;

    public ChunkGenerator(int seed = 123456789)
    {
        _dirtType        = BlockDataRepository.GetBlockTypeId("Dirt");
        _grassType       = BlockDataRepository.GetBlockTypeId("Grass");
        _logType         = BlockDataRepository.GetBlockTypeId("Log");
        _leavesType      = BlockDataRepository.GetBlockTypeId("Leaves");
        _waterType       = BlockDataRepository.GetBlockTypeId("Water");
        _cobblestoneType = BlockDataRepository.GetBlockTypeId("Cobblestone");
        _torchType       = BlockDataRepository.GetBlockTypeId("Torch");
        _yellowLightType = BlockDataRepository.GetBlockTypeId("YellowLightblock");
        _redLightType    = BlockDataRepository.GetBlockTypeId("RedLightblock");
        _blueLightType   = BlockDataRepository.GetBlockTypeId("BlueLightblock");

        _seed = seed;
        var rng = new System.Random(seed);
        _noiseOffsetX = (float)(rng.NextDouble() * 10000.0);
        _noiseOffsetZ = (float)(rng.NextDouble() * 10000.0);
    }

    public ChunkUpdate GenerateChunk(Vector3Int chunkPos)
    {
        var builder = new ChunkUpdateBuilder(chunkPos);
        var chunkBasePos = VoxelPosHelper.ChunkPosToGlobalChunkBaseVoxelPos(chunkPos);

        for (int z = 0; z < VoxelInfo.ChunkSize; ++z)
        {
            for (int x = 0; x < VoxelInfo.ChunkSize; ++x)
            {
                int globalX = chunkBasePos.x + x;
                int globalZ = chunkBasePos.z + z;
                int terrainHeight = GetTerrainHeight(globalX, globalZ);
                bool underwater = terrainHeight < SeaLevel;

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
                        // Underwater terrain surface stays as dirt; above-water gets grass
                        builder.QueueVoxelInChunk(x, y, z, underwater ? _dirtType : _grassType);
                    }
                    else if (globalY > terrainHeight && globalY <= SeaLevel)
                    {
                        builder.QueueVoxelInChunk(x, y, z, _waterType);
                    }
                }

                // Surface features — emit only from the chunk that contains terrainHeight
                bool surfaceInThisChunk = terrainHeight >= chunkBasePos.y &&
                                          terrainHeight < chunkBasePos.y + VoxelInfo.ChunkSize;
                if (!underwater && surfaceInThisChunk)
                {
                    int localY = terrainHeight - chunkBasePos.y;
                    var surfacePos = new Vector3Int(x, localY, z);

                    if (ShouldPlaceFeature(globalX, globalZ, GetTreeChance(globalX, globalZ)))
                    {
                        var treeRng = new System.Random(HashPos(globalX, globalZ));
                        PlaceTree(builder, surfacePos, treeRng);
                    }
                    else if (ShouldPlaceFeature(globalX ^ 0xDEAD, globalZ ^ 0xBEEF, CrystalChance))
                    {
                        var crystalRng = new System.Random(HashPos(globalX ^ 0xDEAD, globalZ ^ 0xBEEF));
                        PlaceCrystalCluster(builder, surfacePos, crystalRng);
                    }
                    else if (ShouldPlaceFeature(globalX ^ 0xCAFE, globalZ ^ 0xF00D, TorchChance))
                    {
                        builder.QueueVoxel(surfacePos + Vector3Int.up, _torchType);
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

        // Continental scale: very broad (~600 voxel) highland/lowland regions
        float continental = Mathf.PerlinNoise(nx / 600f, nz / 600f);
        float continentalBias = (continental * continental - 0.25f) * 55f;

        // Regional roughness: plains vs rugged hills at medium scale
        float roughness = Mathf.PerlinNoise(nx / 280f + 100f, nz / 280f + 100f);

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
        // Offset to decorrelate forest placement from terrain shape
        float nx = globalX + _noiseOffsetX + 4321f;
        float nz = globalZ + _noiseOffsetZ + 8765f;

        // Large-scale forest regions (~200 voxels): decides if we're in forest territory
        float region  = Mathf.PerlinNoise(nx / 200f, nz / 200f);
        // Medium-scale cluster variation (~65 voxels): shapes individual clusters
        float cluster = Mathf.PerlinNoise(nx / 65f,  nz / 65f);

        float regionFactor  = Mathf.Max(0f, region  - 0.44f) * (1f / 0.56f);
        float clusterFactor = Mathf.Max(0f, cluster - 0.38f) * (1f / 0.62f);

        // Background: rare scattered trees everywhere (~0.3%)
        // Dense forest cores: up to ~48% chance per tile
        return 0.003f + regionFactor * clusterFactor * ForestTreeDensity;
    }

    // Ellipsoid tree crown — more natural than a cube, still cheap to compute
    private void PlaceTree(ChunkUpdateBuilder builder, Vector3Int localRootPos, System.Random rng)
    {
        int trunkHeight = rng.Next(4, 8);
        int crownR      = rng.Next(2, 4); // horizontal radius

        for (int ty = 1; ty <= trunkHeight; ++ty)
            builder.QueueVoxel(localRootPos + Vector3Int.up * ty, _logType);

        // Crown center overlaps the trunk top so leaves wrap around the log
        Vector3Int crownCenter = localRootPos + Vector3Int.up * (trunkHeight + crownR - 1);
        int rx = crownR;
        int ry = Mathf.Max(1, crownR - 1); // slightly flatter vertically
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

    // A central spike plus 1-2 shorter satellite spikes — glowing crystal formation
    private void PlaceCrystalCluster(ChunkUpdateBuilder builder, Vector3Int localRootPos, System.Random rng)
    {
        ushort[] palette = { _yellowLightType, _redLightType, _blueLightType };
        ushort mainColor = palette[rng.Next(palette.Length)];

        int mainHeight = rng.Next(2, 5);
        for (int ty = 1; ty <= mainHeight; ++ty)
            builder.QueueVoxel(localRootPos + Vector3Int.up * ty, mainColor);

        int satellites = rng.Next(1, 3);
        for (int s = 0; s < satellites; ++s)
        {
            int ox = rng.Next(-2, 3);
            int oz = rng.Next(-2, 3);
            ushort satColor = palette[rng.Next(palette.Length)];
            int satHeight = rng.Next(1, mainHeight);
            for (int ty = 1; ty <= satHeight; ++ty)
                builder.QueueVoxel(localRootPos + new Vector3Int(ox, ty, oz), satColor);
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
            int h = 17;
            h = h * 31 + _seed;
            h = h * 31 + x;
            h = h * 31 + z;
            return h;
        }
    }

    private readonly int   _seed;
    private readonly float _noiseOffsetX;
    private readonly float _noiseOffsetZ;

    private readonly ushort _torchType;
    private readonly ushort _dirtType;
    private readonly ushort _grassType;
    private readonly ushort _logType;
    private readonly ushort _leavesType;
    private readonly ushort _waterType;
    private readonly ushort _cobblestoneType;
    private readonly ushort _yellowLightType;
    private readonly ushort _redLightType;
    private readonly ushort _blueLightType;
}
