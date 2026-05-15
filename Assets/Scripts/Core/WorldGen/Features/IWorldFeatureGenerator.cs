using UnityEngine;

public enum FeatureContext 
{ 
    OnSurface, 
    Underground,
    Underwater, 
    Anywhere 
}

public struct FeaturePlacementContext
{
    public ChunkUpdateBuilder Builder;
    public Vector3Int LocalPlacementVoxelPos;
    public System.Random Rng;
}

public interface IWorldFeatureGenerator
{
    FeatureContext Context { get; }
    int? ExclusionGroup { get; }
    int Priority { get; }
    float PlacementChance { get; }
    bool ShouldPlace(int globalX, int globalZ, int terrainHeight);
    void Place(FeaturePlacementContext ctx);
    System.Random GetPlacementRng(int globalX, int globalZ);
}
