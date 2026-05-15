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

public interface IWorldGenFeatureGenerator
{
    FeatureContext Context { get; }
    
    int? ExclusionGroup { get; }
    
    int Priority { get; }
    
    bool ShouldPlace(int globalX, int globalZ, int terrainHeight);

    void Place(FeaturePlacementContext ctx);
}
