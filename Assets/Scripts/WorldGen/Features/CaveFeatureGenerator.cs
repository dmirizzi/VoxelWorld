public class CaveFeatureGenerator : IWorldGenFeatureGenerator
{
    private const float CaveChance = 0.0003f;

    public FeatureContext Context => FeatureContext.OnSurface;
    
    public int? ExclusionGroup => null;
    
    public int Priority       => 0;

    public CaveFeatureGenerator(int seed, WormCaveParams caveParams)
    {
        _seed = seed;
        _caveGenerator = new WormCaveGenerator(seed, caveParams);
    }

    public bool ShouldPlace(int globalX, int globalZ, int terrainHeight)
        => new System.Random(WorldGenHash.Pos(_seed, globalX ^ 0xC0DE, globalZ ^ 0xC0DE)).NextDouble() < CaveChance;

    public void Place(FeaturePlacementContext ctx)
        => _caveGenerator.GenerateCave(ctx.Builder, ctx.LocalPlacementVoxelPos, ctx.Rng);

    private readonly int              _seed;
    private readonly WormCaveGenerator _caveGenerator;
}
