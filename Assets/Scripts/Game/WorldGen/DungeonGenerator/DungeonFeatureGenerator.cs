using System.Collections.Generic;
using UnityEngine;

public class DungeonFeatureGenerator : WorldFeatureGeneratorBase
{
    private TerrainHeightSampler _heightSampler;

    protected override void OnConfigure(Dictionary<string, string> properties)
        => _heightSampler = new TerrainHeightSampler(Seed);

    public override void Place(FeaturePlacementContext ctx)
    {
        var chunkBase   = VoxelPosHelper.ChunkPosToGlobalChunkBaseVoxelPos(ctx.Builder.ChunkPos);
        var globalEntry = chunkBase + ctx.LocalPlacementVoxelPos;
        new DungeonGenerator(_heightSampler, DungeonParams.Default).Generate(ctx.Builder, globalEntry, ctx.Rng);
    }
}
