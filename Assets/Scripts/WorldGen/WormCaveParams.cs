using UnityEngine;

[System.Serializable]
public struct WormCaveParams
{
    [Header("Simulation Budget")]
    public int   MaxTotalSteps;
    public int   MaxBranchGenerations;
    public int   MinChildLife;

    [Header("Size Class Selection (cumulative roll thresholds)")]
    public float SmallCaveThreshold;
    public float MediumCaveThreshold;

    [Header("Small Caves")]
    public float SmallRadiusMin;
    public float SmallRadiusRange;
    public int   SmallLifeMin;
    public int   SmallLifeRange;
    public float SmallSplitChance;

    [Header("Medium Caves")]
    public float MediumRadiusMin;
    public float MediumRadiusRange;
    public int   MediumLifeMin;
    public int   MediumLifeRange;
    public float MediumSplitChance;

    [Header("Large Caves")]
    public float LargeRadiusMin;
    public float LargeRadiusRange;
    public int   LargeLifeMin;
    public int   LargeLifeRange;
    public float LargeSplitChance;

    [Header("Room Branches (large caves only)")]
    public float RoomBranchChance;
    public float RoomRadiusMultMin;
    public float RoomRadiusMultRange;
    public float RoomRadiusMax;
    public int   RoomLifeMin;
    public int   RoomLifeRange;
    public float RoomSplitChance;
    public float RoomFixedStepSize;

    [Header("Children of Large Caves")]
    public float LargeChildRadiusMin;
    public float LargeChildRadiusRange;
    public float LargeChildLifeMin;
    public float LargeChildLifeRange;
    public float LargeChildSplitChance;

    [Header("Children of Medium Caves")]
    public float MedChildRadiusMin;
    public float MedChildRadiusRange;
    public float MedChildLifeMin;
    public float MedChildLifeRange;
    public float MedChildSplitChance;

    [Header("Children of Small Caves")]
    public float SmallChildRadiusMin;
    public float SmallChildRadiusRange;
    public float SmallChildLifeMin;
    public float SmallChildLifeRange;
    public float SmallChildSplitChance;

    [Header("Tributaries from Room Worms")]
    public float RoomTribRadiusMin;
    public float RoomTribRadiusRange;
    public int   RoomTribLifeMin;
    public int   RoomTribLifeRange;
    public float RoomTribSplitChance;

    [Header("Repulsion & Branch Divergence")]
    public float OriginRepulsionScale;
    public float BranchDirectionInertia;
    public float BranchNudgeScale;
    public float ChildPositionSpreadRadii;

    [Header("Worm Movement & Direction")]
    public float DirectionInertia;
    public float NudgeScale;
    public float DownwardBias;
    public float NormalStepMultiplier;
    public float SplitLockFraction;
    public float MinSplitRadius;
    public float ChildSpreadScale;
    public float StartDirDownMin;
    public float StartDirDownRange;

    [Header("Death Probability")]
    public float DeathChanceAtBirth;
    public float DeathChanceAtDeath;

    [Header("Carving")]
    public float CarveStretchAlong;
    public float CarveBoundaryNoise;

    [Header("Crystal Decoration")]
    public float CrystalChancePerStep;

    public static WormCaveParams Default => new WormCaveParams
    {
        MaxTotalSteps          = 40000,
        MaxBranchGenerations   = 3,
        MinChildLife           = 20,

        SmallCaveThreshold  = 0.50f,
        MediumCaveThreshold = 0.90f,

        SmallRadiusMin      = 1.2f,
        SmallRadiusRange    = 0.8f,
        SmallLifeMin        = 60,
        SmallLifeRange      = 40,
        SmallSplitChance    = 0.02f,

        MediumRadiusMin     = 1.8f,
        MediumRadiusRange   = 1.0f,
        MediumLifeMin       = 100,
        MediumLifeRange     = 60,
        MediumSplitChance   = 0.04f,

        LargeRadiusMin      = 2.0f,
        LargeRadiusRange    = 1.5f,
        LargeLifeMin        = 80,
        LargeLifeRange      = 40,
        LargeSplitChance    = 0.30f,

        RoomBranchChance    = 0.10f,
        RoomRadiusMultMin   = 1.5f,
        RoomRadiusMultRange = 2.0f,
        RoomRadiusMax       = 25.0f,
        RoomLifeMin         = 25,
        RoomLifeRange       = 15,
        RoomSplitChance     = 0.50f,
        RoomFixedStepSize   = 0.5f,

        LargeChildRadiusMin   = 0.50f,
        LargeChildRadiusRange = 0.30f,
        LargeChildLifeMin     = 0.50f,
        LargeChildLifeRange   = 0.30f,
        LargeChildSplitChance = 0.06f,

        MedChildRadiusMin     = 0.50f,
        MedChildRadiusRange   = 0.30f,
        MedChildLifeMin       = 0.35f,
        MedChildLifeRange     = 0.25f,
        MedChildSplitChance   = 0.03f,

        SmallChildRadiusMin   = 0.40f,
        SmallChildRadiusRange = 0.25f,
        SmallChildLifeMin     = 0.20f,
        SmallChildLifeRange   = 0.15f,
        SmallChildSplitChance = 0.01f,

        RoomTribRadiusMin   = 0.20f,
        RoomTribRadiusRange = 0.20f,
        RoomTribLifeMin     = 20,
        RoomTribLifeRange   = 20,
        RoomTribSplitChance = 0.02f,

        OriginRepulsionScale     = 0.5f,
        BranchDirectionInertia   = 2.5f,
        BranchNudgeScale         = 1.2f,
        ChildPositionSpreadRadii = 2.0f,

        DirectionInertia     = 4.0f,
        NudgeScale           = 0.7f,
        DownwardBias         = 0.02f,
        NormalStepMultiplier = 0.65f,
        SplitLockFraction    = 0.88f,
        MinSplitRadius       = 1.0f,
        ChildSpreadScale     = 2.5f,
        StartDirDownMin      = 0.2f,
        StartDirDownRange    = 0.5f,

        DeathChanceAtBirth = 0.002f,
        DeathChanceAtDeath = 0.015f,

        CarveStretchAlong  = 1.4f,
        CarveBoundaryNoise = 0.15f,

        CrystalChancePerStep = 0.02f,
    };
}
