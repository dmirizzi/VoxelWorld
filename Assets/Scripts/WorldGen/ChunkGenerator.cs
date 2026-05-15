using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ChunkGenerator
{
    private const int SeaLevel = -30;
    private const int DirtLayerDepth = 24;

    public ChunkGenerator(int seed = 123456789)
    {
        _dirtType  = BlockDataRepository.GetBlockTypeId("Dirt");
        _grassType = BlockDataRepository.GetBlockTypeId("Grass");
        _waterType = BlockDataRepository.GetBlockTypeId("Water");
        _cobblestoneType = BlockDataRepository.GetBlockTypeId("Cobblestone");

        var rng = new System.Random(seed);
        _noiseOffsetX = (float)(rng.NextDouble() * 10000.0);
        _noiseOffsetZ = (float)(rng.NextDouble() * 10000.0);

        var features = WorldFeatureGeneratorRegistry.Load(seed);

        _exclusiveSurfaceGroups = features
            .Where(f => f.ExclusionGroup.HasValue && IsSurfaceContext(f.Context))
            .GroupBy(f => f.ExclusionGroup!.Value)
            .Select(g => g.OrderBy(f => f.Priority).ToList())
            .ToList();

        _independentSurfaceFeatures = features
            .Where(f => !f.ExclusionGroup.HasValue && IsSurfaceContext(f.Context))
            .ToList();
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
                        builder.QueueVoxelInChunk(x, y, z, underwater ? _dirtType : _grassType);
                    }
                    else if (globalY > terrainHeight && globalY <= SeaLevel)
                    {
                        builder.QueueVoxelInChunk(x, y, z, _waterType);
                    }
                }

                bool surfaceInThisChunk = terrainHeight >= chunkBasePos.y &&
                                          terrainHeight <  chunkBasePos.y + VoxelInfo.ChunkSize;
                if (!underwater && surfaceInThisChunk)
                {
                    int localY     = terrainHeight - chunkBasePos.y;
                    var surfacePos = new Vector3Int(x, localY, z);

                    foreach (var group in _exclusiveSurfaceGroups)
                    {
                        foreach (var feature in group)
                        {
                            if (!feature.ShouldPlace(globalX, globalZ, terrainHeight)) continue;
                            feature.Place(MakePlacementCtx(builder, surfacePos, globalX, globalZ, feature));
                            break;
                        }
                    }

                    foreach (var feature in _independentSurfaceFeatures)
                    {
                        if (feature.ShouldPlace(globalX, globalZ, terrainHeight))
                            feature.Place(MakePlacementCtx(builder, surfacePos, globalX, globalZ, feature));
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

        float continental     = Mathf.PerlinNoise(nx / 600f, nz / 600f);
        float continentalBias = (continental * continental - 0.25f) * 55f;
        float roughness       = Mathf.PerlinNoise(nx / 280f + 100f, nz / 280f + 100f);

        float h = continentalBias;
        h += Mathf.PerlinNoise(nx / 250f, nz / 250f) * 60f;
        h += Mathf.PerlinNoise(nx /  80f, nz /  80f) * (12f + roughness * 24f);
        h += Mathf.PerlinNoise(nx /  30f, nz /  30f) *  8f;
        h += Mathf.PerlinNoise(nx /  10f, nz /  10f) *  2f;
        h -= 50f;
        return Mathf.RoundToInt(h);
    }

    private static FeaturePlacementContext MakePlacementCtx(ChunkUpdateBuilder builder, Vector3Int localPos,
        int globalX, int globalZ, IWorldFeatureGenerator feature)
    {
        return new FeaturePlacementContext
        {
            Builder = builder,
            LocalPlacementVoxelPos = localPos,
            Rng = feature.GetPlacementRng(globalX, globalZ),
        };
    }

    private static bool IsSurfaceContext(FeatureContext ctx) =>
        ctx == FeatureContext.OnSurface || ctx == FeatureContext.Anywhere;

    private readonly float _noiseOffsetX;
    private readonly float _noiseOffsetZ;

    private readonly ushort _dirtType;
    private readonly ushort _grassType;
    private readonly ushort _waterType;
    private readonly ushort _cobblestoneType;

    private readonly List<List<IWorldFeatureGenerator>> _exclusiveSurfaceGroups;
    private readonly List<IWorldFeatureGenerator> _independentSurfaceFeatures;
}
