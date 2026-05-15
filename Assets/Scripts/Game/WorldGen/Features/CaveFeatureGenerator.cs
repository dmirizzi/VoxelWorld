using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

public class CaveFeatureGenerator : WorldFeatureGeneratorBase
{
    private WormCaveGenerator _caveGenerator;

    protected override void OnConfigure(Dictionary<string, string> properties)
    {
        object caveParams = WormCaveParams.Default;
        foreach (var kvp in properties)
        {
            var field = typeof(WormCaveParams).GetField(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
            if (field == null) continue;
            var value = Convert.ChangeType(kvp.Value, field.FieldType, CultureInfo.InvariantCulture);
            field.SetValue(caveParams, value);
        }
        _caveGenerator = new WormCaveGenerator(Seed, (WormCaveParams)caveParams);
    }

    public override void Place(FeaturePlacementContext ctx)
        => _caveGenerator.GenerateCave(ctx.Builder, ctx.LocalPlacementVoxelPos, ctx.Rng);
}
