using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

public static class WorldFeatureGeneratorRegistry
{
    private const string ConfigPath = "WorldFeatureGenerators";

    public static List<IWorldFeatureGenerator> Load(int seed)
    {
        var asset = Resources.Load<TextAsset>(ConfigPath);
        if (asset == null)
            throw new Exception($"Missing Resources/{ConfigPath}.json");

        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new StringEnumConverter());
        var configs = JsonConvert.DeserializeObject<List<WorldFeatureGeneratorConfig>>(asset.text, settings);

        var result = new List<IWorldFeatureGenerator>(configs.Count);
        foreach (var config in configs)
        {
            var type = typeof(WorldFeatureGeneratorBase).Assembly
                .GetTypes()
                .FirstOrDefault(t => t.Name == config.Type);

            if (type == null)
                throw new Exception($"WorldFeatureGeneratorRegistry: type '{config.Type}' not found.");
            if (!typeof(WorldFeatureGeneratorBase).IsAssignableFrom(type))
                throw new Exception($"WorldFeatureGeneratorRegistry: '{config.Type}' must extend WorldFeatureGeneratorBase.");

            var instance = (WorldFeatureGeneratorBase)Activator.CreateInstance(type);
            instance.Configure(seed, config);
            result.Add(instance);
        }
        return result;
    }
}
