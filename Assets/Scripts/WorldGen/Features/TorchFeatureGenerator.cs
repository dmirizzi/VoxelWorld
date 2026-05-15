using System.Collections.Generic;
using UnityEngine;

public class TorchFeatureGenerator : WorldFeatureGeneratorBase
{
    private ushort _torchType;

    protected override void OnConfigure(Dictionary<string, string> properties)
        => _torchType = BlockDataRepository.GetBlockTypeId("Torch");

    public override void Place(FeaturePlacementContext ctx)
        => ctx.Builder.QueueVoxel(ctx.LocalPlacementVoxelPos + Vector3Int.up, _torchType);
}
