using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class TreeFeatureGenerator : WorldFeatureGeneratorBase
{
    private const float DefaultForestTreeDensity = 0.35f;


    protected override void OnConfigure(Dictionary<string, string> properties)
    {
        _forestTreeDensity = properties.TryGetValue("ForestTreeDensity", out var v)
            ? float.Parse(v, CultureInfo.InvariantCulture)
            : DefaultForestTreeDensity;

        var rng = new System.Random(Seed);
        _noiseOffsetX = (float)(rng.NextDouble() * 10000.0);
        _noiseOffsetZ = (float)(rng.NextDouble() * 10000.0);
        _logType    = BlockDataRepository.GetBlockTypeId("Log");
        _leavesType = BlockDataRepository.GetBlockTypeId("Leaves");
    }

    public override bool ShouldPlace(int globalX, int globalZ, int terrainHeight)
        => new System.Random(WorldGenHash.Pos(Seed, globalX, globalZ)).NextDouble() < GetTreeChance(globalX, globalZ);

    public override void Place(FeaturePlacementContext ctx)
        => PlaceTree(ctx.Builder, ctx.LocalPlacementVoxelPos, ctx.Rng);

    private float GetTreeChance(int globalX, int globalZ)
    {
        float nx = globalX + _noiseOffsetX + 4321f;
        float nz = globalZ + _noiseOffsetZ + 8765f;

        float region  = Mathf.PerlinNoise(nx / 200f, nz / 200f);
        float cluster = Mathf.PerlinNoise(nx /  65f, nz /  65f);

        float regionFactor  = Mathf.Max(0f, region  - 0.44f) * (1f / 0.56f);
        float clusterFactor = Mathf.Max(0f, cluster - 0.38f) * (1f / 0.62f);

        return PlacementChance + regionFactor * clusterFactor * _forestTreeDensity;
    }

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
    private float  _forestTreeDensity;
    private float  _noiseOffsetX;
    private float  _noiseOffsetZ;
    private ushort _logType;
    private ushort _leavesType;
}
