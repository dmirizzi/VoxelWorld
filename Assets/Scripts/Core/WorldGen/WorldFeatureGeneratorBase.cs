using System.Collections.Generic;

public abstract class WorldFeatureGeneratorBase : IWorldFeatureGenerator
{
    public FeatureContext Context { get; private set; }
    public int? ExclusionGroup { get; private set; }
    public int Priority { get; private set; }
    public float PlacementChance { get; private set; }

    protected int Seed { get; private set; }

    private int _typeSalt;

    public void Configure(int seed, WorldFeatureGeneratorConfig config)
    {
        Seed = seed;
        Context = config.Context;
        ExclusionGroup  = config.ExclusionGroup;
        Priority = config.Priority;
        PlacementChance = config.PlacementChance;
        _typeSalt = WorldGenHash.Str(GetType().Name);
        OnConfigure(config.Properties ?? new Dictionary<string, string>());
    }

    protected virtual void OnConfigure(Dictionary<string, string> properties) { }

    public virtual bool ShouldPlace(int globalX, int globalZ, int terrainHeight)
    {
        int h = WorldGenHash.Pos(Seed, globalX ^ _typeSalt, globalZ ^ (_typeSalt >> 16));
        return new System.Random(h).NextDouble() < PlacementChance;
    }

    public System.Random GetPlacementRng(int globalX, int globalZ)
    {
        int h = WorldGenHash.Pos(Seed, globalX ^ (_typeSalt >> 8), globalZ ^ (_typeSalt << 8));
        return new System.Random(h);
    }

    public abstract void Place(FeaturePlacementContext ctx);
}
