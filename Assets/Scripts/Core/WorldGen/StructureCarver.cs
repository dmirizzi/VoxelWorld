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
    public static void FillBox(ChunkUpdateBuilder builder, Vector3Int min, Vector3Int max, ushort blockType = 0)
    {
        for (int y = min.y; y <= max.y; y++)
        for (int z = min.z; z <= max.z; z++)
        for (int x = min.x; x <= max.x; x++)
            builder.QueueGlobalVoxel(new Vector3Int(x, y, z), blockType);
    }

    public static void CarveHollowBox(
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

    public static void CarveCorridor(
        ChunkUpdateBuilder builder,
        Vector3Int origin, BlockFace dir,
        int length, int width, int height)
    {
        BlockFaceHelper.ToDirectionVectors(dir, out var forward, out var right);
        int wMin = -(width / 2);
        var a = origin + right * wMin;
        var b = origin + forward * (length - 1) + right * (wMin + width - 1);
        FillBox(
            builder,
            new Vector3Int(Math.Min(a.x, b.x), origin.y,            Math.Min(a.z, b.z)),
            new Vector3Int(Math.Max(a.x, b.x), origin.y + height - 1, Math.Max(a.z, b.z)));
    }

    public static void CarveVerticalShaft(
        ChunkUpdateBuilder builder,
        Vector3Int top, int depth, int width,
        ushort wallType,
        ushort ladderType = 0, BlockFace ladderWall = BlockFace.Back)
    {
        int xMin = top.x - width / 2;
        int xMax = xMin + width - 1;
        int zMin = top.z - width / 2;
        int zMax = zMin + width - 1;
        int yBot = top.y - depth + 1;

        FillBox(builder, new Vector3Int(xMin - 1, yBot - 1, zMin - 1), new Vector3Int(xMax + 1, top.y, zMax + 1), wallType);
        FillBox(builder, new Vector3Int(xMin, yBot, zMin), new Vector3Int(xMax, top.y, zMax));

        if (ladderType == 0) return;

        for (int y = yBot; y <= top.y; y++)
        {
            var ladderPos = ladderWall switch
            {
                BlockFace.Front => new Vector3Int((xMin + xMax) / 2, y, zMax),
                BlockFace.Back  => new Vector3Int((xMin + xMax) / 2, y, zMin),
                BlockFace.Right => new Vector3Int(xMax, y, (zMin + zMax) / 2),
                BlockFace.Left  => new Vector3Int(xMin, y, (zMin + zMax) / 2),
                _               => new Vector3Int((xMin + xMax) / 2, y, zMin)
            };
            builder.QueueGlobalVoxel(ladderPos, ladderType, BlockProperties.PlacementFace(ladderWall));
        }
    }

    // Two-pass: air first, then structure — avoids step N's air overwriting step N-1's floor.
    public static void CarveDiagonalRamp(
        ChunkUpdateBuilder builder,
        Vector3Int start, BlockFace dir,
        int levelDelta, int width,
        ushort wallType, ushort stepType)
    {
        BlockFaceHelper.ToDirectionVectors(dir, out var forward, out var right);
        int wMin  = -(width / 2);
        int wMax  = wMin + width - 1;
        int steps = Math.Abs(levelDelta);
        int ySign = Math.Sign(levelDelta);
        var stepPlacementProperty = BlockProperties.PlacementFace(BlockFaceHelper.GetOppositeFace(dir));

        for (int step = 0; step < steps; step++)
        {
            int yOffset = ySign * step;
            for (int w = wMin; w <= wMax; w++)
            for (int h = 0; h < 3; h++)
                builder.QueueGlobalVoxel(start + forward * step + right * w + Vector3Int.up * (yOffset + h), 0);
        }

        for (int step = 0; step < steps; step++)
        {
            int yOffset = ySign * step;
            for (int w = wMin; w <= wMax; w++)
            {
                builder.QueueGlobalVoxel(
                    start + forward * step + right * w + Vector3Int.up * (yOffset - 1),
                    stepType, stepPlacementProperty);
                builder.QueueGlobalVoxel(
                    start + forward * step + right * w + Vector3Int.up * (yOffset + 3),
                    wallType);

                if (w == wMin || w == wMax)
                    for (int h = 0; h < 3; h++)
                        builder.QueueGlobalVoxel(start + forward * step + right * w + Vector3Int.up * (yOffset + h), wallType);
            }
        }
    }

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

}
