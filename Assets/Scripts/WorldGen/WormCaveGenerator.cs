using System;
using System.Collections.Generic;
using UnityEngine;

public class WormCaveGenerator
{
    public WormCaveGenerator(int seed, WormCaveParams p)
    {
        _seed            = seed;
        _p               = p;
        _yellowLightType = BlockDataRepository.GetBlockTypeId("YellowLightblock");
        _redLightType    = BlockDataRepository.GetBlockTypeId("RedLightblock");
        _blueLightType   = BlockDataRepository.GetBlockTypeId("BlueLightblock");
    }

    // Original entry point used by ChunkGenerator; positions passed to builder are chunk-local.
    public void GenerateCave(ChunkUpdateBuilder builder, Vector3Int chunkBasePos,
                             int globalX, int globalZ, int terrainHeight, System.Random rng)
    {
        RunCave(globalX, globalZ, terrainHeight, rng, (globalPos, type) =>
        {
            builder.QueueVoxel(globalPos - chunkBasePos, type);
        });
    }

    // Prototyping entry point — callback receives global voxel positions directly.
    public void GenerateCaveRaw(int globalX, int globalZ, int terrainHeight, System.Random rng,
                                Action<Vector3Int, ushort> onGlobalVoxel)
    {
        RunCave(globalX, globalZ, terrainHeight, rng, onGlobalVoxel);
    }

    // ── Shared simulation ─────────────────────────────────────────────────────

    private void RunCave(int globalX, int globalZ, int terrainHeight, System.Random rng,
                         Action<Vector3Int, ushort> onVoxel)
    {
        var worms = new Queue<CaveWorm>();
        worms.Enqueue(MakeStartWorm(globalX, terrainHeight, globalZ, rng));

        int totalSteps = 0;

        while (worms.Count > 0 && totalSteps < _p.MaxTotalSteps)
        {
            var w = worms.Dequeue();
            if (w.Lifespan <= 0) continue;

            totalSteps++;

            float lifeFrac = (float)w.Lifespan / w.MaxLifespan;
            w.Radius = 1f + (w.MaxRadius - 1f) * Mathf.Sqrt(lifeFrac);

            int cx = Mathf.RoundToInt(w.Position.x);
            int cy = Mathf.RoundToInt(w.Position.y);
            int cz = Mathf.RoundToInt(w.Position.z);
            CarveNoisy(onVoxel, cx, cy, cz, w.Radius, w.Direction);

            if (rng.NextDouble() < _p.CrystalChancePerStep)
            {
                int floorY      = cy - Mathf.RoundToInt(w.Radius) - 1;
                var globalFloor = new Vector3Int(cx, floorY, cz);
                PlaceCrystalCluster(onVoxel, globalFloor, rng);
            }

            float stepSize = w.FixedStepSize > 0f
                ? w.FixedStepSize
                : Mathf.Max(1f, w.Radius * _p.NormalStepMultiplier);
            w.Position += w.Direction * stepSize;
            w.Lifespan--;

            var nudge = new Vector3(
                (float)(rng.NextDouble() * 2.0 - 1.0),
                (float)(rng.NextDouble() * 2.0 - 1.0),
                (float)(rng.NextDouble() * 2.0 - 1.0)
            ) * w.NudgeScale;
            var awayFromBirth = w.Position - w.BirthPosition;
            float birthDist   = awayFromBirth.magnitude;
            var repulsion     = birthDist > 0.01f
                ? (awayFromBirth / birthDist) * _p.OriginRepulsionScale
                : Vector3.zero;
            w.Direction = Vector3.Normalize(w.Direction * w.DirectionInertia + nudge + repulsion);
            w.Direction = Vector3.Normalize(w.Direction + Vector3.down * _p.DownwardBias);

            float  deathChance = Mathf.Lerp(_p.DeathChanceAtBirth, _p.DeathChanceAtDeath, 1f - lifeFrac);
            bool   canSplit    = lifeFrac < _p.SplitLockFraction && w.Radius > _p.MinSplitRadius;
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

        Debug.Log($"Rng={sizeRoll}. Cave Size: {(sizeRoll < _p.SmallCaveThreshold ? "Small" : sizeRoll < _p.MediumCaveThreshold ? "Medium" : "Large")}");
        if (sizeRoll < _p.SmallCaveThreshold)
        {
            radius      = _p.SmallRadiusMin + (float)rng.NextDouble() * _p.SmallRadiusRange;
            life        = _p.SmallLifeMin + rng.Next(_p.SmallLifeRange);
            splitChance = _p.SmallSplitChance;
            isLarge     = false;
        }
        else if (sizeRoll < _p.MediumCaveThreshold)
        {
            radius      = _p.MediumRadiusMin + (float)rng.NextDouble() * _p.MediumRadiusRange;
            life        = _p.MediumLifeMin + rng.Next(_p.MediumLifeRange);
            splitChance = _p.MediumSplitChance;
            isLarge     = false;
        }
        else
        {
            radius      = _p.LargeRadiusMin + (float)rng.NextDouble() * _p.LargeRadiusRange;
            life        = _p.LargeLifeMin + rng.Next(_p.LargeLifeRange);
            splitChance = _p.LargeSplitChance;
            isLarge     = true;
        }

        var startPos = new Vector3(globalX, terrainHeight, globalZ);
        return new CaveWorm
        {
            Position         = startPos,
            BirthPosition    = startPos,
            Direction        = RandomDownwardDirection(rng),
            Radius           = radius,
            MaxRadius        = radius,
            Lifespan         = life,
            MaxLifespan      = life,
            SplitChance      = splitChance,
            FixedStepSize    = 0f,
            IsLarge          = isLarge,
            DirectionInertia = _p.DirectionInertia,
            NudgeScale       = _p.NudgeScale,
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
            var childDir = Vector3.Normalize(parent.Direction + spread * _p.ChildSpreadScale);
            childDir = Vector3.Normalize(childDir + Vector3.down * _p.DownwardBias);

            float radius, splitChance, fixedStep;
            int   life;
            bool  isLarge;

            bool parentIsRoom = parent.FixedStepSize > 0f;

            if (parentIsRoom)
            {
                radius      = Mathf.Max(1.0f, parent.MaxRadius * (_p.RoomTribRadiusMin + (float)rng.NextDouble() * _p.RoomTribRadiusRange));
                life        = _p.RoomTribLifeMin + rng.Next(_p.RoomTribLifeRange);
                splitChance = _p.RoomTribSplitChance;
                fixedStep   = 0f;
                isLarge     = false;
            }
            else if (parent.IsLarge)
            {
                bool makeRoom = (i == 0) && rng.NextDouble() < _p.RoomBranchChance;

                if (makeRoom)
                {
                    radius      = Mathf.Min(_p.RoomRadiusMax, parent.MaxRadius * (_p.RoomRadiusMultMin + (float)rng.NextDouble() * _p.RoomRadiusMultRange));
                    life        = _p.RoomLifeMin + rng.Next(_p.RoomLifeRange);
                    splitChance = _p.RoomSplitChance;
                    fixedStep   = _p.RoomFixedStepSize;
                    isLarge     = false;
                }
                else
                {
                    radius      = Mathf.Max(1.5f, parent.Radius * (_p.LargeChildRadiusMin + (float)rng.NextDouble() * _p.LargeChildRadiusRange));
                    life        = (int)(parent.MaxLifespan * (_p.LargeChildLifeMin + rng.NextDouble() * _p.LargeChildLifeRange));
                    splitChance = _p.LargeChildSplitChance;
                    fixedStep   = 0f;
                    isLarge     = true;
                }
            }
            else if (parent.SplitChance >= _p.MediumSplitChance)
            {
                radius      = Mathf.Max(1.0f, parent.Radius * (_p.MedChildRadiusMin + (float)rng.NextDouble() * _p.MedChildRadiusRange));
                life        = (int)(parent.MaxLifespan * (_p.MedChildLifeMin + rng.NextDouble() * _p.MedChildLifeRange));
                splitChance = _p.MedChildSplitChance;
                fixedStep   = 0f;
                isLarge     = false;
            }
            else
            {
                radius      = Mathf.Max(1.0f, parent.Radius * (_p.SmallChildRadiusMin + (float)rng.NextDouble() * _p.SmallChildRadiusRange));
                life        = (int)(parent.MaxLifespan * (_p.SmallChildLifeMin + rng.NextDouble() * _p.SmallChildLifeRange));
                splitChance = _p.SmallChildSplitChance;
                fixedStep   = 0f;
                isLarge     = false;
            }

            var childPos = parent.Position + childDir * (parent.Radius * _p.ChildPositionSpreadRadii);
            worms.Enqueue(new CaveWorm
            {
                Position         = childPos,
                BirthPosition    = childPos,
                Direction        = childDir,
                Radius           = radius,
                MaxRadius        = radius,
                Lifespan         = Mathf.Max(_p.MinChildLife, life),
                MaxLifespan      = Mathf.Max(_p.MinChildLife, life),
                SplitChance      = splitChance,
                FixedStepSize    = fixedStep,
                IsLarge          = isLarge,
                DirectionInertia = _p.BranchDirectionInertia,
                NudgeScale       = _p.BranchNudgeScale,
            });
        }
    }

    private void CarveNoisy(Action<Vector3Int, ushort> onVoxel,
                            int cx, int cy, int cz, float radius, Vector3 dir)
    {
        int r = Mathf.CeilToInt(radius + 1.5f);

        float rLong = radius * _p.CarveStretchAlong;
        float rPerp = radius;

        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
        for (int dz = -r; dz <= r; dz++)
        {
            var   offset   = new Vector3(dx, dy, dz);
            float along    = Vector3.Dot(offset, dir);
            float perpSqr  = (offset - dir * along).sqrMagnitude;
            float distNorm = (along * along) / (rLong * rLong) + perpSqr / (rPerp * rPerp);
            float noise    = BlockNoise(cx + dx, cy + dy, cz + dz) * _p.CarveBoundaryNoise;

            if (distNorm <= 1f + noise)
                onVoxel(new Vector3Int(cx + dx, cy + dy, cz + dz), 0);
        }
    }

    private void PlaceCrystalCluster(Action<Vector3Int, ushort> onVoxel,
                                     Vector3Int globalRootPos, System.Random rng)
    {
        ushort[] palette   = { _yellowLightType, _redLightType, _blueLightType };
        ushort   mainColor = palette[rng.Next(palette.Length)];

        int mainHeight = rng.Next(2, 5);
        for (int ty = 1; ty <= mainHeight; ++ty)
            onVoxel(globalRootPos + Vector3Int.up * ty, mainColor);

        int satellites = rng.Next(1, 3);
        for (int s = 0; s < satellites; ++s)
        {
            int    ox        = rng.Next(-2, 3);
            int    oz        = rng.Next(-2, 3);
            ushort satColor  = palette[rng.Next(palette.Length)];
            int    satHeight = rng.Next(1, mainHeight);
            for (int ty = 1; ty <= satHeight; ++ty)
                onVoxel(globalRootPos + new Vector3Int(ox, ty, oz), satColor);
        }
    }

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
            -(_p.StartDirDownMin + (float)(rng.NextDouble() * _p.StartDirDownRange)),
            (float)(rng.NextDouble() * 2.0 - 1.0)
        ));
    }

    private struct CaveWorm
    {
        public Vector3 Position;
        public Vector3 BirthPosition;
        public Vector3 Direction;
        public float   Radius;
        public float   MaxRadius;
        public int     Lifespan;
        public int     MaxLifespan;
        public float   SplitChance;
        public float   FixedStepSize;
        public bool    IsLarge;
        public float   DirectionInertia;
        public float   NudgeScale;
    }

    private readonly int          _seed;
    private readonly WormCaveParams _p;
    private readonly ushort       _yellowLightType;
    private readonly ushort       _redLightType;
    private readonly ushort       _blueLightType;
}
