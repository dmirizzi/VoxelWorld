using System.Collections.Generic;

public class WorldFeatureGeneratorConfig
{
    public string Type;
    public FeatureContext Context;
    public int? ExclusionGroup;
    public int Priority;
    public float PlacementChance;
    public Dictionary<string, string> Properties;
}
