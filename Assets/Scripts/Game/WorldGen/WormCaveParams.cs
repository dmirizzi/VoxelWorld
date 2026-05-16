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
        MaxTotalSteps          = 40000,  // hard cap on carving steps across all worms; bounds peak compute per cave
        MaxBranchGenerations   = 3,      // how many times a lineage can fork before children can no longer split
        MinChildLife           = 20,     // clamps child lifespan; prevents splits from dying immediately

        SmallCaveThreshold  = 0.50f,    // roll < 0.50 → small (50% of caves)
        MediumCaveThreshold = 0.90f,    // roll 0.50..0.90 → medium (40%); above → large (10%)

        SmallRadiusMin      = 1.2f,     // voxel radius: actual = Min + rng * Range
        SmallRadiusRange    = 0.8f,     // → 1.2..2.0 voxels
        SmallLifeMin        = 60,       // carve steps: actual = Min + rng * Range
        SmallLifeRange      = 40,       // → 60..100 steps
        SmallSplitChance    = 0.02f,    // per-step probability of forking into 2–3 children

        MediumRadiusMin     = 1.8f,     // → 1.8..2.8 voxels
        MediumRadiusRange   = 1.0f,
        MediumLifeMin       = 100,      // → 100..160 steps; medium caves travel further than small
        MediumLifeRange     = 60,
        MediumSplitChance   = 0.04f,

        LargeRadiusMin      = 2.0f,     // → 2.0..3.5 voxels
        LargeRadiusRange    = 1.5f,
        LargeLifeMin        = 80,       // → 80..120 steps (shorter life but very high split chance)
        LargeLifeRange      = 40,
        LargeSplitChance    = 0.30f,    // large caves branch aggressively, producing complex networks

        RoomBranchChance    = 0.40f,    // per-split, chance the first child becomes a room worm (not a tunnel)
        RoomRadiusMultMin   = 2.0f,     // room radius = parent.radius × (Min + rng * Range)
        RoomRadiusMultRange = 5.0f,     // → 1.5×..3.5× parent; rooms are substantially wider than tunnels
        RoomRadiusMax       = 50.0f,    // hard voxel cap so rooms can't grow unbounded
        RoomLifeMin         = 25,       // → 25..40 steps; rooms are short-lived but carve a dense blob
        RoomLifeRange       = 30,
        RoomSplitChance     = 0.50f,    // rooms fork often, spawning many tributary tunnels outward
        RoomFixedStepSize   = 0.5f,     // tiny fixed step makes the room worm carve overlapping spheres; > 0 is also the internal marker that identifies a worm as a room

        LargeChildRadiusMin   = 0.50f,  // child radius = parent.radius × (Min + rng * Range)
        LargeChildRadiusRange = 0.30f,  // → 0.50×..0.80× parent radius
        LargeChildLifeMin     = 0.50f,  // child lifespan = parent.maxLifespan × (Min + rng * Range)
        LargeChildLifeRange   = 0.30f,  // → 50%..80% of parent life
        LargeChildSplitChance = 0.06f,  // children can still branch, keeping the network growing

        MedChildRadiusMin     = 0.50f,  // → 0.50×..0.80× parent radius
        MedChildRadiusRange   = 0.30f,
        MedChildLifeMin       = 0.35f,  // → 35%..60% of parent life; narrower and shorter than large children
        MedChildLifeRange     = 0.25f,
        MedChildSplitChance   = 0.03f,

        SmallChildRadiusMin   = 0.40f,  // → 0.40×..0.65× parent radius; hairline dead-end passages
        SmallChildRadiusRange = 0.25f,
        SmallChildLifeMin     = 0.20f,  // → 20%..35% of parent life
        SmallChildLifeRange   = 0.15f,
        SmallChildSplitChance = 0.01f,  // tiny tunnels almost never branch further

        RoomTribRadiusMin   = 0.20f,    // tributary radius = room.radius × (Min + rng * Range)
        RoomTribRadiusRange = 0.20f,    // → 0.20×..0.40× room radius
        RoomTribLifeMin     = 20,       // absolute step count (not a fraction like other children)
        RoomTribLifeRange   = 20,       // → 20..40 steps
        RoomTribSplitChance = 0.02f,    // tributaries rarely branch

        OriginRepulsionScale     = 0.5f,  // constant push away from birth position each step; prevents tight loops and keeps tunnels from doubling back
        BranchDirectionInertia   = 2.5f,  // lower than parent's DirectionInertia so branch worms curve more freely
        BranchNudgeScale         = 1.2f,  // higher than parent's NudgeScale so branches meander more than the trunk
        ChildPositionSpreadRadii = 2.0f,  // child spawn offset = parent.radius × 2.0; spreads branch origins apart at the split point

        DirectionInertia     = 4.0f,   // resistance to turning; higher = straighter tunnels
        NudgeScale           = 0.7f,   // amplitude of the random jitter added to direction each step; higher = wigglier
        DownwardBias         = 0.02f,  // small constant nudge toward Vector3.down; makes caves gradually descend
        NormalStepMultiplier = 0.65f,  // step size = max(1, radius × 0.65); lower values give more carve overlap and smoother walls
        SplitLockFraction    = 0.88f,  // splitting is only allowed when lifeFrac < 0.88, i.e. after the first ~12% of life
        MinSplitRadius       = 1.0f,   // worm must be wider than this to be eligible for splitting
        ChildSpreadScale     = 2.5f,   // magnitude of random divergence applied to each child's direction; higher = more splayed branches
        StartDirDownMin      = 0.2f,   // minimum downward Y component of the root worm's starting direction
        StartDirDownRange    = 0.5f,   // → 0.2..0.7 downward bias; ensures caves always head underground initially

        DeathChanceAtBirth = 0.002f,   // per-step random death probability at the start of life (lerp low end)
        DeathChanceAtDeath = 0.015f,   // per-step random death probability near end of life (lerp high end); old worms fade out naturally

        CarveStretchAlong  = 1.4f,     // elongates the carved ellipsoid along the travel direction; prevents gaps between steps at low step-counts
        CarveBoundaryNoise = 0.15f,    // per-voxel noise on the ellipsoid surface; roughens tunnel walls

        CrystalChancePerStep = 0.02f,  // 2% chance per carve step to spawn a lit crystal cluster on the tunnel floor
    };
}
