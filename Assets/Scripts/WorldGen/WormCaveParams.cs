using UnityEngine;

[System.Serializable]
public struct WormCaveParams
{
    [Header("Simulation Budget")]
    public int   MaxTotalSteps;
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
        MaxTotalSteps       = 200000,
        MinChildLife        = 10,

        SmallCaveThreshold  = 0.10f,
        MediumCaveThreshold = 0.20f,

        SmallRadiusMin      = 1.5f,
        SmallRadiusRange    = 1.0f,
        SmallLifeMin        = 40,
        SmallLifeRange      = 21,
        SmallSplitChance    = 0.02f,

        MediumRadiusMin     = 2.0f,
        MediumRadiusRange   = 2.0f,
        MediumLifeMin       = 80,
        MediumLifeRange     = 41,
        MediumSplitChance   = 0.05f,

        LargeRadiusMin      = 4.0f,
        LargeRadiusRange    = 15.0f,
        LargeLifeMin        = 60,
        LargeLifeRange      = 100,
        LargeSplitChance    = 0.2f,

        RoomBranchChance    = 0.10f,
        RoomRadiusMultMin   = 1.5f,
        RoomRadiusMultRange = 3.0f,
        RoomRadiusMax       = 50.0f,
        RoomLifeMin         = 35,
        RoomLifeRange       = 26,
        RoomSplitChance     = 0.60f,
        RoomFixedStepSize   = 0.5f,

        LargeChildRadiusMin   = 0.45f,
        LargeChildRadiusRange = 0.35f,
        LargeChildLifeMin     = 0.8f,
        LargeChildLifeRange   = 1.2f,
        LargeChildSplitChance = 0.05f,

        MedChildRadiusMin     = 0.50f,
        MedChildRadiusRange   = 0.40f,
        MedChildLifeMin       = 0.40f,
        MedChildLifeRange     = 0.10f,
        MedChildSplitChance   = 0.03f,

        SmallChildRadiusMin   = 0.40f,
        SmallChildRadiusRange = 0.30f,
        SmallChildLifeMin     = 0.25f,
        SmallChildLifeRange   = 0.10f,
        SmallChildSplitChance = 0.01f,

        RoomTribRadiusMin   = 0.20f,
        RoomTribRadiusRange = 0.20f,
        RoomTribLifeMin     = 30,
        RoomTribLifeRange   = 41,
        RoomTribSplitChance = 0.02f,

        OriginRepulsionScale    = 0.5f,
        BranchDirectionInertia  = 1.0f,
        BranchNudgeScale        = 2.5f,
        ChildPositionSpreadRadii = 1.5f,

        DirectionInertia     = 2.0f,
        NudgeScale           = 1.5f,
        DownwardBias         = 0.03f,
        NormalStepMultiplier = 0.6f,
        SplitLockFraction    = 0.8f,
        MinSplitRadius       = 1.5f,
        ChildSpreadScale     = 2.0f,
        StartDirDownMin      = 0.2f,
        StartDirDownRange    = 0.6f,

        DeathChanceAtBirth = 0.01f,
        DeathChanceAtDeath = 0.10f,

        CarveStretchAlong  = 1.4f,
        CarveBoundaryNoise = 0.2f,

        CrystalChancePerStep = 0.03f,
    };
}
