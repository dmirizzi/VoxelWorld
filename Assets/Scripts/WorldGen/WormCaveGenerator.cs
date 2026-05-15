using System;
using System.Collections.Generic;
using UnityEngine;

public class WormCaveGenerator
{
    // ── Simulation budget ──────────────────────────────────────────────────────
    private const int   MaxTotalSteps = 3000;
    private const int   MinChildLife  = 10;      // floor on child lifespan

    // ── Size-class selection (cumulative roll thresholds) ─────────────────────
    private const float SmallCaveThreshold  = 0.10f;  // 0 – 0.70  → small
    private const float MediumCaveThreshold = 0.20f;  // 0.70 – 0.95 → medium; rest → large

    // ── Small caves ───────────────────────────────────────────────────────────
    private const float SmallRadiusMin   = 1.5f;
    private const float SmallRadiusRange = 1.0f;   // radius drawn from [min, min+range]
    private const int   SmallLifeMin     = 40;
    private const int   SmallLifeRange   = 21;     // [40, 60]
    private const float SmallSplitChance = 0.02f;

    // ── Medium caves ──────────────────────────────────────────────────────────
    private const float MediumRadiusMin   = 2.0f;
    private const float MediumRadiusRange = 2.0f;  // [2, 4]
    private const int   MediumLifeMin     = 80;
    private const int   MediumLifeRange   = 41;    // [80, 120]
    private const float MediumSplitChance = 0.05f;

    // ── Large caves ───────────────────────────────────────────────────────────
    private const float LargeRadiusMin   = 4.0f;
    private const float LargeRadiusRange = 3.0f;  // [4, 7]
    private const int   LargeLifeMin     = 300;
    private const int   LargeLifeRange   = 301;   // [300, 600]
    private const float LargeSplitChance = 0.06f;

    // ── Room branches (large caves only) ──────────────────────────────────────
    private const float RoomBranchChance    = 0.10f;  // chance first child of large split becomes a room
    private const float RoomRadiusMultMin   = 1.5f;   // room radius = parent.MaxRadius × mult
    private const float RoomRadiusMultRange = 0.5f;   // mult drawn from [min, min+range]
    private const float RoomRadiusMax       = 10.0f;  // hard cap on room radius
    private const int   RoomLifeMin         = 35;
    private const int   RoomLifeRange       = 26;     // [35, 60] slow steps → compact chamber
    private const float RoomSplitChance     = 0.60f;  // splits quickly to emit tributary exits
    private const float RoomFixedStepSize   = 0.5f;   // tiny step → overlapping carves → round room

    // ── Children of large-cave worms ──────────────────────────────────────────
    private const float LargeChildRadiusMin   = 0.45f;  // fraction of parent.Radius
    private const float LargeChildRadiusRange = 0.35f;
    private const float LargeChildLifeMin     = 0.20f;  // fraction of parent.Lifespan
    private const float LargeChildLifeRange   = 0.15f;
    private const float LargeChildSplitChance = 0.05f;

    // ── Children of medium-cave worms ─────────────────────────────────────────
    private const float MedChildRadiusMin   = 0.50f;
    private const float MedChildRadiusRange = 0.40f;
    private const float MedChildLifeMin     = 0.40f;
    private const float MedChildLifeRange   = 0.10f;
    private const float MedChildSplitChance = 0.03f;

    // ── Children of small-cave worms ──────────────────────────────────────────
    private const float SmallChildRadiusMin   = 0.40f;
    private const float SmallChildRadiusRange = 0.30f;
    private const float SmallChildLifeMin     = 0.25f;
    private const float SmallChildLifeRange   = 0.10f;
    private const float SmallChildSplitChance = 0.01f;

    // ── Tributaries from room worms ───────────────────────────────────────────
    private const float RoomTribRadiusMin   = 0.20f;  // fraction of room.MaxRadius
    private const float RoomTribRadiusRange = 0.20f;
    private const int   RoomTribLifeMin     = 30;
    private const int   RoomTribLifeRange   = 41;     // [30, 70]
    private const float RoomTribSplitChance = 0.02f;

    // ── Worm movement & direction ─────────────────────────────────────────────
    private const float DirectionInertia    = 2.0f;   // current-heading weight vs nudge
    private const float NudgeScale          = 1.5f;   // random nudge magnitude each step
    private const float DownwardBias        = 0.03f;  // gentle persistent downward pull
    private const float NormalStepMultiplier = 0.6f;  // normal stepSize = Max(1, radius × this)
    private const float SplitLockFraction   = 0.8f;   // no splits while lifeFrac > this (first 20%)
    private const float MinSplitRadius      = 1.5f;   // minimum radius to be eligible for a split
    private const float ChildSpreadScale    = 2.0f;   // how widely children diverge from parent heading
    private const float StartDirDownMin     = 0.2f;   // starting Y component in [−min, −(min+range)]
    private const float StartDirDownRange   = 0.6f;

    // ── Death probability (lerped over lifespan) ──────────────────────────────
    private const float DeathChanceAtBirth = 0.01f;
    private const float DeathChanceAtDeath = 0.10f;

    // ── Carving ───────────────────────────────────────────────────────────────
    private const float CarveStretchAlong  = 1.4f;   // ellipsoid stretch factor along travel direction
    private const float CarveBoundaryNoise = 0.2f;   // max fractional boundary shift from BlockNoise

    // ── Crystal decoration ────────────────────────────────────────────────────
    private const float CrystalChancePerStep = 0.03f;

    public WormCaveGenerator(int seed)
    {
        _seed            = seed;
        _yellowLightType = BlockDataRepository.GetBlockTypeId("YellowLightblock");
        _redLightType    = BlockDataRepository.GetBlockTypeId("RedLightblock");
        _blueLightType   = BlockDataRepository.GetBlockTypeId("BlueLightblock");
    }

    public void GenerateCave(ChunkUpdateBuilder builder, Vector3Int chunkBasePos,
                             int globalX, int globalZ, int terrainHeight, System.Random rng)
    {
        var worms = new Queue<CaveWorm>();
        worms.Enqueue(MakeStartWorm(globalX, terrainHeight, globalZ, rng));

        int totalSteps = 0;

        while (worms.Count > 0 && totalSteps < MaxTotalSteps)
        {
            var w = worms.Dequeue();
            if (w.Lifespan <= 0) continue;

            totalSteps++;

            float lifeFrac = (float)w.Lifespan / w.MaxLifespan;
            w.Radius = 1f + (w.MaxRadius - 1f) * Mathf.Sqrt(lifeFrac);

            int cx = Mathf.RoundToInt(w.Position.x);
            int cy = Mathf.RoundToInt(w.Position.y);
            int cz = Mathf.RoundToInt(w.Position.z);
            CarveNoisy(builder, chunkBasePos, cx, cy, cz, w.Radius, w.Direction);

            if (rng.NextDouble() < CrystalChancePerStep)
            {
                int floorY   = cy - Mathf.RoundToInt(w.Radius) - 1;
                var floorPos = new Vector3Int(cx - chunkBasePos.x, floorY - chunkBasePos.y, cz - chunkBasePos.z);
                PlaceCrystalCluster(builder, floorPos, rng);
            }

            // Room worms use a fixed slow step so overlapping carves accumulate into a round chamber
            float stepSize = w.FixedStepSize > 0f
                ? w.FixedStepSize
                : Mathf.Max(1f, w.Radius * NormalStepMultiplier);
            w.Position += w.Direction * stepSize;
            w.Lifespan--;

            var nudge = new Vector3(
                (float)(rng.NextDouble() * 2.0 - 1.0),
                (float)(rng.NextDouble() * 2.0 - 1.0),
                (float)(rng.NextDouble() * 2.0 - 1.0)
            ) * NudgeScale;
            w.Direction = Vector3.Normalize(w.Direction * DirectionInertia + nudge);
            w.Direction = Vector3.Normalize(w.Direction + Vector3.down * DownwardBias);

            float  deathChance = Mathf.Lerp(DeathChanceAtBirth, DeathChanceAtDeath, 1f - lifeFrac);
            bool   canSplit    = lifeFrac < SplitLockFraction && w.Radius > MinSplitRadius;
            double roll        = rng.NextDouble();

            if (canSplit && roll < w.SplitChance)
            {
                SpawnChildren(worms, w, rng);
                // parent dies on split
            }
            else if (roll < w.SplitChance + deathChance)
            {
                // early death
            }
            else
            {
                worms.Enqueue(w);
            }
        }
    }

    private CaveWorm MakeStartWorm(int globalX, int terrainHeight, int globalZ, System.Random rng)
    {
        double sizeRoll = rng.NextDouble();

        float radius, splitChance;
        int   life;
        bool  isLarge;

        if (sizeRoll < SmallCaveThreshold)
        {
            radius      = SmallRadiusMin + (float)rng.NextDouble() * SmallRadiusRange;
            life        = SmallLifeMin + rng.Next(SmallLifeRange);
            splitChance = SmallSplitChance;
            isLarge     = false;
        }
        else if (sizeRoll < MediumCaveThreshold)
        {
            radius      = MediumRadiusMin + (float)rng.NextDouble() * MediumRadiusRange;
            life        = MediumLifeMin + rng.Next(MediumLifeRange);
            splitChance = MediumSplitChance;
            isLarge     = false;
        }
        else
        {
            radius      = LargeRadiusMin + (float)rng.NextDouble() * LargeRadiusRange;
            life        = LargeLifeMin + rng.Next(LargeLifeRange);
            splitChance = LargeSplitChance;
            isLarge     = true;
        }

        return new CaveWorm
        {
            Position      = new Vector3(globalX, terrainHeight, globalZ),
            Direction     = RandomDownwardDirection(rng),
            Radius        = radius,
            MaxRadius     = radius,
            Lifespan      = life,
            MaxLifespan   = life,
            SplitChance   = splitChance,
            FixedStepSize = 0f,
            IsLarge       = isLarge,
        };
    }

    private void SpawnChildren(Queue<CaveWorm> worms, CaveWorm parent, System.Random rng)
    {
        int childCount = 2 + rng.Next(2); // 2–3

        for (int i = 0; i < childCount; i++)
        {
            var spread = new Vector3(
                (float)(rng.NextDouble() * 2.0 - 1.0),
                (float)(rng.NextDouble() * 2.0 - 1.0),
                (float)(rng.NextDouble() * 2.0 - 1.0)
            );
            var childDir = Vector3.Normalize(parent.Direction + spread * ChildSpreadScale);
            childDir = Vector3.Normalize(childDir + Vector3.down * DownwardBias);

            float radius, splitChance, fixedStep;
            int   life;
            bool  isLarge;

            bool parentIsRoom = parent.FixedStepSize > 0f;

            if (parentIsRoom)
            {
                // Narrow tributary passage branching off the chamber
                radius      = Mathf.Max(1.0f, parent.MaxRadius * (RoomTribRadiusMin + (float)rng.NextDouble() * RoomTribRadiusRange));
                life        = RoomTribLifeMin + rng.Next(RoomTribLifeRange);
                splitChance = RoomTribSplitChance;
                fixedStep   = 0f;
                isLarge     = false;
            }
            else if (parent.IsLarge)
            {
                // First child has a chance to become a room worm
                bool makeRoom = (i == 0) && rng.NextDouble() < RoomBranchChance;

                if (makeRoom)
                {
                    radius      = Mathf.Min(RoomRadiusMax, parent.MaxRadius * (RoomRadiusMultMin + (float)rng.NextDouble() * RoomRadiusMultRange));
                    life        = RoomLifeMin + rng.Next(RoomLifeRange);
                    splitChance = RoomSplitChance;
                    fixedStep   = RoomFixedStepSize;
                    isLarge     = false;
                }
                else
                {
                    radius      = Mathf.Max(1.5f, parent.Radius * (LargeChildRadiusMin + (float)rng.NextDouble() * LargeChildRadiusRange));
                    life        = (int)(parent.Lifespan * (LargeChildLifeMin + rng.NextDouble() * LargeChildLifeRange));
                    splitChance = LargeChildSplitChance;
                    fixedStep   = 0f;
                    isLarge     = true;
                }
            }
            else if (parent.SplitChance >= MediumSplitChance)
            {
                radius      = Mathf.Max(1.0f, parent.Radius * (MedChildRadiusMin + (float)rng.NextDouble() * MedChildRadiusRange));
                life        = (int)(parent.Lifespan * (MedChildLifeMin + rng.NextDouble() * MedChildLifeRange));
                splitChance = MedChildSplitChance;
                fixedStep   = 0f;
                isLarge     = false;
            }
            else
            {
                radius      = Mathf.Max(1.0f, parent.Radius * (SmallChildRadiusMin + (float)rng.NextDouble() * SmallChildRadiusRange));
                life        = (int)(parent.Lifespan * (SmallChildLifeMin + rng.NextDouble() * SmallChildLifeRange));
                splitChance = SmallChildSplitChance;
                fixedStep   = 0f;
                isLarge     = false;
            }

            worms.Enqueue(new CaveWorm
            {
                Position      = parent.Position,
                Direction     = childDir,
                Radius        = radius,
                MaxRadius     = radius,
                Lifespan      = Mathf.Max(MinChildLife, life),
                MaxLifespan   = Mathf.Max(MinChildLife, life),
                SplitChance   = splitChance,
                FixedStepSize = fixedStep,
                IsLarge       = isLarge,
            });
        }
    }

    private void CarveNoisy(ChunkUpdateBuilder builder, Vector3Int chunkBasePos,
                            int cx, int cy, int cz, float radius, Vector3 dir)
    {
        int r = Mathf.CeilToInt(radius + 1.5f);

        float rLong = radius * CarveStretchAlong;
        float rPerp = radius;

        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
        for (int dz = -r; dz <= r; dz++)
        {
            var   offset   = new Vector3(dx, dy, dz);
            float along    = Vector3.Dot(offset, dir);
            float perpSqr  = (offset - dir * along).sqrMagnitude;
            float distNorm = (along * along) / (rLong * rLong) + perpSqr / (rPerp * rPerp);
            float noise    = BlockNoise(cx + dx, cy + dy, cz + dz) * CarveBoundaryNoise;

            if (distNorm <= 1f + noise)
            {
                var localPos = new Vector3Int(cx + dx - chunkBasePos.x,
                                             cy + dy - chunkBasePos.y,
                                             cz + dz - chunkBasePos.z);
                builder.QueueVoxel(localPos, 0);
            }
        }
    }

    private void PlaceCrystalCluster(ChunkUpdateBuilder builder, Vector3Int localRootPos, System.Random rng)
    {
        ushort[] palette  = { _yellowLightType, _redLightType, _blueLightType };
        ushort   mainColor = palette[rng.Next(palette.Length)];

        int mainHeight = rng.Next(2, 5);
        for (int ty = 1; ty <= mainHeight; ++ty)
            builder.QueueVoxel(localRootPos + Vector3Int.up * ty, mainColor);

        int satellites = rng.Next(1, 3);
        for (int s = 0; s < satellites; ++s)
        {
            int    ox       = rng.Next(-2, 3);
            int    oz       = rng.Next(-2, 3);
            ushort satColor = palette[rng.Next(palette.Length)];
            int    satHeight = rng.Next(1, mainHeight);
            for (int ty = 1; ty <= satHeight; ++ty)
                builder.QueueVoxel(localRootPos + new Vector3Int(ox, ty, oz), satColor);
        }
    }

    // Deterministic per-block noise in [−0.5, 0.5] using the world seed
    private float BlockNoise(int x, int y, int z)
    {
        unchecked
        {
            uint h = (uint)_seed;
            h ^= (uint)x; h *= 0x9e3779b9u; h ^= h >> 16;
            h ^= (uint)y; h *= 0x85ebca6bu; h ^= h >> 13;
            h ^= (uint)z; h *= 0xc2b2ae35u; h ^= h >> 16;
            return (h & 0xFFu) / 255f - 0.5f;
        }
    }

    private Vector3 RandomDownwardDirection(System.Random rng)
    {
        return Vector3.Normalize(new Vector3(
            (float)(rng.NextDouble() * 2.0 - 1.0),
            -(StartDirDownMin + (float)(rng.NextDouble() * StartDirDownRange)),
            (float)(rng.NextDouble() * 2.0 - 1.0)
        ));
    }

    private struct CaveWorm
    {
        public Vector3 Position;
        public Vector3 Direction;
        public float   Radius;
        public float   MaxRadius;
        public int     Lifespan;
        public int     MaxLifespan;
        public float   SplitChance;
        public float   FixedStepSize; // 0 = default formula; >0 = fixed (room worms)
        public bool    IsLarge;       // enables room-branch logic when spawning children
    }

    private readonly int   _seed;
    private readonly ushort _yellowLightType;
    private readonly ushort _redLightType;
    private readonly ushort _blueLightType;
}
