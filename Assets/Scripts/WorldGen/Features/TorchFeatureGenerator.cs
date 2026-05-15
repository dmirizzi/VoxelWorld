using UnityEngine;

public class TorchFeatureGenerator : IWorldGenFeatureGenerator
{
    private const float TorchChance = 0.005f;

    public FeatureContext Context  => FeatureContext.OnSurface;
    public int? ExclusionGroup => 0;
    public int Priority  => 1;

    public TorchFeatureGenerator(int seed)
    {
        _seed = seed;
        _torchType = BlockDataRepository.GetBlockTypeId("Torch");
    }

    public bool ShouldPlace(int globalX, int globalZ, int terrainHeight)
        => new System.Random(WorldGenHash.Pos(_seed, globalX ^ 0xCAFE, globalZ ^ 0xF00D)).NextDouble() < TorchChance;

    public void Place(FeaturePlacementContext ctx)
        => ctx.Builder.QueueVoxel(ctx.LocalPlacementVoxelPos + Vector3Int.up, _torchType);

    private readonly int _seed;
    private readonly ushort _torchType;
}
