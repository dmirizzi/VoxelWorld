using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum BoxFaces
{
    Floor   = 1,
    Ceiling = 2,
    Walls   = 4,
    All     = Floor | Ceiling | Walls
}

public static class StructureCarver
{
    // Fill a rectangular volume with air.
    public static void CarveBox(ChunkUpdateBuilder builder, Vector3Int min, Vector3Int max)
    {
        for (int y = min.y; y <= max.y; y++)
        for (int z = min.z; z <= max.z; z++)
        for (int x = min.x; x <= max.x; x++)
            builder.QueueGlobalVoxel(new Vector3Int(x, y, z), 0);
    }

    // Place `type` on the 1-block-thick shell of a rectangular volume.
    public static void LineBox(
        ChunkUpdateBuilder builder,
        Vector3Int min, Vector3Int max,
        ushort type,
        BoxFaces faces = BoxFaces.All)
    {
        for (int y = min.y; y <= max.y; y++)
        for (int z = min.z; z <= max.z; z++)
        for (int x = min.x; x <= max.x; x++)
        {
            bool onFloor   = y == min.y;
            bool onCeiling = y == max.y;
            bool onWall    = x == min.x || x == max.x || z == min.z || z == max.z;

            bool include = ((faces & BoxFaces.Floor)   != 0 && onFloor)
                        || ((faces & BoxFaces.Ceiling) != 0 && onCeiling)
                        || ((faces & BoxFaces.Walls)   != 0 && onWall && !onFloor && !onCeiling);

            if (include)
                builder.QueueGlobalVoxel(new Vector3Int(x, y, z), type);
        }
    }

    // Carve a flat axis-aligned corridor (air only). Combine with LineBox for walls.
    // `origin` is the floor-level start corner. Width is perpendicular to dir.
    public static void CarveCorridor(
        ChunkUpdateBuilder builder,
        Vector3Int origin, BlockFace dir,
        int length, int width, int height)
    {
        ToDirectionVectors(dir, out var forward, out var right);
        int wMin = -(width / 2);
        int wMax = wMin + width - 1;

        for (int step = 0; step < length; step++)
        for (int h   = 0; h   < height;  h++)
        for (int w   = wMin; w <= wMax;  w++)
            builder.QueueGlobalVoxel(origin + forward * step + right * w + Vector3Int.up * h, 0);
    }

    // Carve a vertical shaft (air), surround with wallType, optionally place ladders
    // on the interior face of one side. Pass ladderType = 0 to skip ladders.
    // `top` is the topmost air voxel. The shaft descends `depth` blocks.
    public static void CarveVerticalShaft(
        ChunkUpdateBuilder builder,
        Vector3Int top, int depth, int width,
        ushort wallType,
        ushort ladderType = 0, BlockFace ladderWall = BlockFace.Back,
        ushort ladderAuxData = 0)
    {
        int xMin = top.x - width / 2;
        int xMax = xMin + width - 1;
        int zMin = top.z - width / 2;
        int zMax = zMin + width - 1;
        int yBot = top.y - depth + 1;

        // Carve shaft air
        for (int y = yBot; y <= top.y; y++)
        for (int z = zMin; z <= zMax; z++)
        for (int x = xMin; x <= xMax; x++)
            builder.QueueGlobalVoxel(new Vector3Int(x, y, z), 0);

        // Cobblestone ring around shaft at each Y level (exterior)
        for (int y = yBot; y <= top.y; y++)
        for (int z = zMin - 1; z <= zMax + 1; z++)
        for (int x = xMin - 1; x <= xMax + 1; x++)
        {
            if (x >= xMin && x <= xMax && z >= zMin && z <= zMax) continue; // interior
            builder.QueueGlobalVoxel(new Vector3Int(x, y, z), wallType);
        }

        // Cobblestone floor one below the shaft bottom
        for (int z = zMin; z <= zMax; z++)
        for (int x = xMin; x <= xMax; x++)
            builder.QueueGlobalVoxel(new Vector3Int(x, yBot - 1, z), wallType);

        // Ladders on interior face of chosen wall (placed as actual blocks inside the shaft)
        if (ladderType != 0)
        {
            for (int y = yBot; y <= top.y; y++)
            {
                Vector3Int ladderPos = ladderWall switch
                {
                    BlockFace.Front => new Vector3Int((xMin + xMax) / 2, y, zMax),
                    BlockFace.Back  => new Vector3Int((xMin + xMax) / 2, y, zMin),
                    BlockFace.Right => new Vector3Int(xMax, y, (zMin + zMax) / 2),
                    BlockFace.Left  => new Vector3Int(xMin, y, (zMin + zMax) / 2),
                    _               => new Vector3Int((xMin + xMax) / 2, y, zMin)
                };
                builder.QueueGlobalVoxel(ladderPos, ladderType, ladderAuxData);
            }
        }
    }

    // Carve a diagonal descending ramp lined with wallType. Flat floor (no wedges).
    // Two-pass: air first, then structure — avoids step N's air overwriting step N-1's floor.
    public static void CarveDiagonalRamp(
        ChunkUpdateBuilder builder,
        Vector3Int start, BlockFace dir,
        int depth, int width, ushort wallType)
    {
        ToDirectionVectors(dir, out var forward, out var right);
        int wMin = -(width / 2);
        int wMax = wMin + width - 1;

        // Pass 1: carve all air
        for (int step = 0; step < depth; step++)
        {
            int yOffset = -step;
            for (int w = wMin; w <= wMax; w++)
            for (int h = 0; h < 3; h++)
                builder.QueueGlobalVoxel(start + forward * step + right * w + Vector3Int.up * (yOffset + h), 0);
        }

        // Pass 2: place walls, floor, ceiling
        for (int step = 0; step < depth; step++)
        {
            int yOffset = -step;
            for (int w = wMin; w <= wMax; w++)
            {
                builder.QueueGlobalVoxel(start + forward * step + right * w + Vector3Int.up * (yOffset - 1), wallType); // floor
                builder.QueueGlobalVoxel(start + forward * step + right * w + Vector3Int.up * (yOffset + 3), wallType); // ceiling

                if (w == wMin || w == wMax)
                    for (int h = 0; h < 3; h++)
                        builder.QueueGlobalVoxel(start + forward * step + right * w + Vector3Int.up * (yOffset + h), wallType);
            }
        }
    }

    // Carve a descending staircase with wedge floor voxels across the full width.
    // Two-pass: air first, then structure.
    public static void CarveStaircase(
        ChunkUpdateBuilder builder,
        Vector3Int start, BlockFace dir,
        int levelDelta, int width,
        ushort wallType, ushort stepType, ushort stepAuxData)
    {
        ToDirectionVectors(dir, out var forward, out var right);
        int wMin  = -(width / 2);
        int wMax  = wMin + width - 1;
        int steps = Mathf.Abs(levelDelta);
        int ySign = levelDelta < 0 ? -1 : 1;

        // Pass 1: carve all air
        for (int step = 0; step < steps; step++)
        {
            int yOffset = ySign * step;
            for (int w = wMin; w <= wMax; w++)
            for (int h = 0; h < 3; h++)
                builder.QueueGlobalVoxel(start + forward * step + right * w + Vector3Int.up * (yOffset + h), 0);
        }

        // Pass 2: place structure
        for (int step = 0; step < steps; step++)
        {
            int yOffset = ySign * step;
            for (int w = wMin; w <= wMax; w++)
            {
                builder.QueueGlobalVoxel(                                                                           // wedge step
                    start + forward * step + right * w + Vector3Int.up * (yOffset - 1),
                    stepType, stepAuxData);
                builder.QueueGlobalVoxel(                                                                           // ceiling
                    start + forward * step + right * w + Vector3Int.up * (yOffset + 3),
                    wallType);

                if (w == wMin || w == wMax)
                    for (int h = 0; h < 3; h++)
                        builder.QueueGlobalVoxel(start + forward * step + right * w + Vector3Int.up * (yOffset + h), wallType);
            }
        }
    }

    // Randomly remove (set to air) voxels from `positions`. Corners are never removed.
    // Returns the surviving positions.
    public static IReadOnlyList<Vector3Int> DecayBlocks(
        ChunkUpdateBuilder builder,
        IEnumerable<Vector3Int> positions,
        IEnumerable<Vector3Int> cornerPositions,
        float decayChance,
        System.Random rng)
    {
        var corners   = new HashSet<Vector3Int>(cornerPositions);
        var survivors = new List<Vector3Int>();

        foreach (var pos in positions)
        {
            if (corners.Contains(pos) || rng.NextDouble() >= decayChance)
                survivors.Add(pos);
            else
                builder.QueueGlobalVoxel(pos, 0);
        }

        return survivors;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static void ToDirectionVectors(BlockFace face, out Vector3Int forward, out Vector3Int right)
    {
        switch (face)
        {
            case BlockFace.Front:  forward = new Vector3Int( 0, 0,  1); right = new Vector3Int( 1, 0,  0); break;
            case BlockFace.Back:   forward = new Vector3Int( 0, 0, -1); right = new Vector3Int(-1, 0,  0); break;
            case BlockFace.Right:  forward = new Vector3Int( 1, 0,  0); right = new Vector3Int( 0, 0, -1); break;
            case BlockFace.Left:   forward = new Vector3Int(-1, 0,  0); right = new Vector3Int( 0, 0,  1); break;
            default:               forward = new Vector3Int( 0, 0,  1); right = new Vector3Int( 1, 0,  0); break;
        }
    }

    public static BlockFace OppositeOf(BlockFace face) => face switch
    {
        BlockFace.Front  => BlockFace.Back,
        BlockFace.Back   => BlockFace.Front,
        BlockFace.Left   => BlockFace.Right,
        BlockFace.Right  => BlockFace.Left,
        BlockFace.Top    => BlockFace.Bottom,
        BlockFace.Bottom => BlockFace.Top,
        _                => face
    };

    public static BlockFace LeftOf(BlockFace face) => face switch
    {
        BlockFace.Front => BlockFace.Left,
        BlockFace.Back  => BlockFace.Right,
        BlockFace.Left  => BlockFace.Back,
        BlockFace.Right => BlockFace.Front,
        _               => face
    };

    public static BlockFace RightOf(BlockFace face) => face switch
    {
        BlockFace.Front => BlockFace.Right,
        BlockFace.Back  => BlockFace.Left,
        BlockFace.Left  => BlockFace.Front,
        BlockFace.Right => BlockFace.Back,
        _               => face
    };
}
