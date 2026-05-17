using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class DungeonGenerator
{
    private struct DungeonBounds
    {
        public Vector3Int Min, Max;

        public bool Intersects(DungeonBounds other) =>
            Min.x < other.Max.x && Max.x > other.Min.x &&
            Min.y < other.Max.y && Max.y > other.Min.y &&
            Min.z < other.Max.z && Max.z > other.Min.z;

        public bool ClearsBelowSurface(int surfaceY, int clearance) =>
            Max.y <= surfaceY - clearance;
    }

    private struct DungeonRoom
    {
        public Vector3Int Center;  // x=centerX, y=floorY, z=centerZ
        public int HalfW, HalfD, Height, Level;
    }

    private struct CorridorInfo
    {
        public Vector3Int Start;
        public BlockFace  Dir;
        public int        Length, Width, LevelDelta;
        public bool       IsStaircase;
    }

    private static readonly BlockFace[] CardinalFaces =
        { BlockFace.Front, BlockFace.Back, BlockFace.Left, BlockFace.Right };

    private readonly DungeonParams           _p;
    private readonly TerrainHeightSampler    _heightSampler;
    private readonly ushort _cobblestoneType;
    private readonly ushort _torchType;
    private readonly ushort _doorType;
    private readonly ushort _ladderType;
    private readonly ushort _wedgeType;
    private readonly ushort _grassType;
    private readonly ushort _dirtType;

    private List<DungeonBounds> _placedVolumes;
    private List<DungeonRoom>   _rooms;
    private List<CorridorInfo>  _corridorLog;
    private int                 _corridorWidth;

    public DungeonGenerator(TerrainHeightSampler heightSampler, DungeonParams p)
    {
        _p             = p;
        _heightSampler = heightSampler;

        _cobblestoneType = BlockDataRepository.GetBlockTypeId("Cobblestone");
        _torchType       = BlockDataRepository.GetBlockTypeId("Torch");
        _doorType        = BlockDataRepository.GetBlockTypeId("Door");
        _ladderType      = BlockDataRepository.GetBlockTypeId("Ladder");
        _wedgeType       = BlockDataRepository.GetBlockTypeId("CobblestoneWedge");
        _grassType       = BlockDataRepository.GetBlockTypeId("Grass");
        _dirtType        = BlockDataRepository.GetBlockTypeId("Dirt");
    }

    public void Generate(ChunkUpdateBuilder builder, Vector3Int globalEntry, System.Random rng)
    {
        _placedVolumes = new List<DungeonBounds>();
        _rooms         = new List<DungeonRoom>();
        _corridorLog   = new List<CorridorInfo>();

        float sizeRoll = (float)rng.NextDouble();
        int roomBudget, maxLevels;
        if (sizeRoll < _p.SmallDungeonThreshold)
        {
            roomBudget = _p.SmallRoomsMin  + rng.Next(_p.SmallRoomsRange  + 1);
            maxLevels  = _p.SmallMaxLevels;
        }
        else if (sizeRoll < _p.MediumDungeonThreshold)
        {
            roomBudget = _p.MediumRoomsMin + rng.Next(_p.MediumRoomsRange + 1);
            maxLevels  = _p.MediumMaxLevels;
        }
        else
        {
            roomBudget = _p.LargeRoomsMin  + rng.Next(_p.LargeRoomsRange  + 1);
            maxLevels  = _p.LargeMaxLevels;
        }

        _corridorWidth = _p.CorridorWidthMin + rng.Next(_p.CorridorWidthRange + 1);

        int ruinHalfSize = (_p.RuinSizeMin + rng.Next(_p.RuinSizeRange + 1)) / 2;
        int surfaceY     = globalEntry.y;

        GenerateSurfaceRuin(builder, globalEntry, ruinHalfSize, rng);
        int tunnelBottomY = GenerateEntryTunnel(builder, globalEntry, rng, out bool isShaft);
        GenerateDungeonLayout(builder, globalEntry, tunnelBottomY, surfaceY, rng, roomBudget, maxLevels);
        WriteDebugFile(globalEntry, surfaceY, ruinHalfSize, tunnelBottomY, isShaft);
    }

    private void GenerateSurfaceRuin(
        ChunkUpdateBuilder builder,
        Vector3Int globalEntry,
        int halfSize,
        System.Random rng)
    {
        int surfaceY   = globalEntry.y;
        int wallHeight = _p.RuinWallHeightMin + rng.Next(_p.RuinWallHeightRange + 1);
        int xMin = globalEntry.x - halfSize;
        int xMax = globalEntry.x + halfSize;
        int zMin = globalEntry.z - halfSize;
        int zMax = globalEntry.z + halfSize;
        int topY = surfaceY + wallHeight;

        TerrainFlattener.Flatten(
            builder,
            globalEntry.x, globalEntry.z,
            halfSize, halfSize,
            _heightSampler,
            _grassType,
            _dirtType,
            subsurfaceDepth: 3);

        // Build all wall voxels
        var wallPositions = new List<Vector3Int>();
        var cornerList    = new List<Vector3Int>();

        for (int y = surfaceY + 1; y <= topY; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                wallPositions.Add(new Vector3Int(x, y, zMin));
                wallPositions.Add(new Vector3Int(x, y, zMax));
            }
            for (int z = zMin + 1; z <= zMax - 1; z++)
            {
                wallPositions.Add(new Vector3Int(xMin, y, z));
                wallPositions.Add(new Vector3Int(xMax, y, z));
            }
            cornerList.Add(new Vector3Int(xMin, y, zMin));
            cornerList.Add(new Vector3Int(xMax, y, zMin));
            cornerList.Add(new Vector3Int(xMin, y, zMax));
            cornerList.Add(new Vector3Int(xMax, y, zMax));
        }

        foreach (var pos in wallPositions)
            builder.QueueGlobalVoxel(pos, _cobblestoneType);
        foreach (var pos in cornerList)
            builder.QueueGlobalVoxel(pos, _cobblestoneType);

        // Carve south-wall entry opening (overwrites the wall block that was just placed)
        int entryX = globalEntry.x;
        builder.QueueGlobalVoxel(new Vector3Int(entryX, surfaceY + 1, zMin), 0);
        builder.QueueGlobalVoxel(new Vector3Int(entryX, surfaceY + 2, zMin), 0);

        // Entry gate: door or broken opening
        if (rng.NextDouble() >= _p.BrokenEntryChance)
        {
            builder.QueueGlobalVoxel(new Vector3Int(entryX, surfaceY + 1, zMin),
                _doorType, DoorAuxData(BlockFace.Front, false));
            builder.QueueGlobalVoxel(new Vector3Int(entryX, surfaceY + 2, zMin),
                _doorType, DoorAuxData(BlockFace.Front, true));
        }

        // Protect entry opening from decay so it is never overwritten with air
        cornerList.Add(new Vector3Int(entryX, surfaceY + 1, zMin));
        cornerList.Add(new Vector3Int(entryX, surfaceY + 2, zMin));

        // Decay passes
        var allBlocks = new List<Vector3Int>(wallPositions.Count + cornerList.Count);
        allBlocks.AddRange(wallPositions);
        allBlocks.AddRange(cornerList);

        var nonTop = new List<Vector3Int>();
        var topRow = new List<Vector3Int>();
        foreach (var pos in allBlocks)
        {
            if (pos.y == topY) topRow.Add(pos);
            else               nonTop.Add(pos);
        }

        StructureCarver.DecayBlocks(builder, nonTop, cornerList, _p.RuinDecayChance, rng);
        var survivingTop = StructureCarver.DecayBlocks(builder, topRow, cornerList, _p.RuinTopRowDecayChance, rng);

        // Torches on surviving top-row blocks
        foreach (var pos in survivingTop)
        {
            if (rng.NextDouble() < _p.RuinTorchChance)
                builder.QueueGlobalVoxel(pos + Vector3Int.up, _torchType, FaceAuxData(BlockFace.Bottom));
        }
    }

    // Returns the Y of the first air voxel at the bottom of the tunnel (= first room's floorY).
    private int GenerateEntryTunnel(ChunkUpdateBuilder builder, Vector3Int globalEntry, System.Random rng, out bool isShaft)
    {
        int depth = _p.TunnelDepthMin + rng.Next(_p.TunnelDepthRange + 1);
        int topY  = globalEntry.y;
        isShaft   = rng.NextDouble() < 0.5;

        if (isShaft)
        {
            StructureCarver.CarveVerticalShaft(
                builder,
                new Vector3Int(globalEntry.x, topY, globalEntry.z),
                depth,
                _p.TunnelWidth,
                _cobblestoneType,
                _ladderType,
                BlockFace.Back,
                FaceAuxData(BlockFace.Back));
        }
        else
        {
            StructureCarver.CarveDiagonalRamp(
                builder,
                new Vector3Int(globalEntry.x, topY, globalEntry.z),
                CardinalFaces[rng.Next(4)],
                -depth,
                _p.TunnelWidth,
                _cobblestoneType,
                _cobblestoneType);
        }

        return topY - depth + 1;
    }

    private void GenerateDungeonLayout(
        ChunkUpdateBuilder builder,
        Vector3Int globalEntry,
        int startY,
        int surfaceY,
        System.Random rng,
        int roomBudget,
        int maxLevels)
    {
        var firstRoom = TryPlaceRoom(
            builder,
            globalEntry.x, globalEntry.z,
            floorY: startY, surfaceY, rng, level: 0);

        if (!firstRoom.HasValue) return;
        _rooms.Add(firstRoom.Value);

        int attemptsLeft = roomBudget * 8;
        int roomsPlaced  = 1;

        while (roomsPlaced < roomBudget && attemptsLeft-- > 0)
        {
            var fromRoom    = _rooms[rng.Next(_rooms.Count)];
            var dir         = CardinalFaces[rng.Next(4)];
            int corridorLen = _p.CorridorLengthMin + rng.Next(_p.CorridorLengthRange + 1);

            bool levelChange = fromRoom.Level < maxLevels - 1
                            && rng.NextDouble() < _p.LevelChangeChance;
            int levelDelta = levelChange
                ? -(_p.LevelStepYMin + rng.Next(_p.LevelStepYRange + 1))
                : 0;

            var exitPos        = ExitPoint(fromRoom, dir);
            var corridorBounds = ComputeCorridorBounds(exitPos, dir, corridorLen, _corridorWidth, _p.CorridorHeight);
            if (!IsPlaceable(corridorBounds, surfaceY)) continue;

            BlockFaceHelper.ToDirectionVectors(dir, out var fwd, out _);
            var destCenter = new Vector3Int(
                exitPos.x + fwd.x * corridorLen,
                fromRoom.Center.y + levelDelta,
                exitPos.z + fwd.z * corridorLen);

            int destLevel = fromRoom.Level + (levelChange ? 1 : 0);
            var newRoom   = TryPlaceRoom(
                builder,
                destCenter.x, destCenter.z,
                floorY: destCenter.y, surfaceY, rng, level: destLevel);

            if (!newRoom.HasValue) continue;

            CarveCorridor(builder, exitPos, dir, corridorLen, levelDelta, fromRoom.Level, rng);
            RegisterVolume(corridorBounds);
            _rooms.Add(newRoom.Value);
            roomsPlaced++;
        }

        foreach (var room in _rooms)
            PlaceRoomTorches(builder, room, rng);
    }

    private DungeonRoom? TryPlaceRoom(
        ChunkUpdateBuilder builder,
        int cx, int cz,
        int floorY, int surfaceY,
        System.Random rng,
        int level)
    {
        float roll  = (float)rng.NextDouble() * (_p.SmallRoomWeight + _p.MediumRoomWeight + _p.LargeRoomWeight);
        int halfW, halfD, height;

        if (roll < _p.SmallRoomWeight)
        {
            int sz = _p.SmallRoomSizeMin + rng.Next(_p.SmallRoomSizeRange + 1);
            halfW  = halfD = sz;
            height = _p.SmallRoomHeightMin  + rng.Next(_p.SmallRoomHeightRange  + 1);
        }
        else if (roll < _p.SmallRoomWeight + _p.MediumRoomWeight)
        {
            int sz = _p.MediumRoomSizeMin + rng.Next(_p.MediumRoomSizeRange + 1);
            halfW  = halfD = sz;
            height = _p.MediumRoomHeightMin + rng.Next(_p.MediumRoomHeightRange + 1);
        }
        else
        {
            halfW  = _p.LargeRoomSizeMin + rng.Next(_p.LargeRoomSizeRange + 1);
            halfD  = _p.LargeRoomSizeMin + rng.Next(_p.LargeRoomSizeRange + 1);
            height = _p.LargeRoomHeightMin  + rng.Next(_p.LargeRoomHeightRange  + 1);
        }

        // AABB covers the 1-block shell (floor, ceiling, walls)
        var bounds = new DungeonBounds
        {
            Min = new Vector3Int(cx - halfW - 1, floorY - 1,          cz - halfD - 1),
            Max = new Vector3Int(cx + halfW + 1, floorY + height + 1, cz + halfD + 1)
        };

        if (!IsPlaceable(bounds, surfaceY)) return null;

        StructureCarver.FillBox(
            builder,
            new Vector3Int(cx - halfW, floorY,              cz - halfD),
            new Vector3Int(cx + halfW, floorY + height - 1, cz + halfD));

        StructureCarver.CarveHollowBox(
            builder,
            new Vector3Int(cx - halfW - 1, floorY - 1,     cz - halfD - 1),
            new Vector3Int(cx + halfW + 1, floorY + height, cz + halfD + 1),
            _cobblestoneType);

        RegisterVolume(bounds);

        return new DungeonRoom
        {
            Center = new Vector3Int(cx, floorY, cz),
            HalfW  = halfW,
            HalfD  = halfD,
            Height = height,
            Level  = level
        };
    }

    private void CarveCorridor(
        ChunkUpdateBuilder builder,
        Vector3Int start, BlockFace dir,
        int length, int levelDelta,
        int fromLevel, System.Random rng)
    {
        bool isStaircase = false;

        if (levelDelta == 0)
        {
            StructureCarver.CarveCorridor(builder, start, dir, length, _corridorWidth, _p.CorridorHeight);
            PlaceCorridorTorches(builder, start, dir, length, rng);
        }
        else
        {
            float staircaseChance = fromLevel == 0 ? _p.StaircaseChanceUpperLevel
                                  : fromLevel == 1 ? _p.StaircaseChanceMidLevel
                                  : _p.StaircaseChanceLowerLevel;

            isStaircase = rng.NextDouble() < staircaseChance;
            if (isStaircase)
            {
                StructureCarver.CarveDiagonalRamp(
                    builder,
                    start, dir,
                    levelDelta, _corridorWidth,
                    _cobblestoneType,
                    _wedgeType);
            }
            else
            {
                int shaftDepth = Math.Abs(levelDelta) + _p.CorridorHeight;
                StructureCarver.CarveVerticalShaft(
                    builder,
                    new Vector3Int(start.x, start.y + _p.CorridorHeight - 1, start.z),
                    shaftDepth,
                    _corridorWidth,
                    _cobblestoneType,
                    _ladderType,
                    BlockFace.Back,
                    FaceAuxData(BlockFace.Back));
            }
        }

        _corridorLog.Add(new CorridorInfo
        {
            Start       = start,
            Dir         = dir,
            Length      = length,
            Width       = _corridorWidth,
            LevelDelta  = levelDelta,
            IsStaircase = isStaircase
        });
    }

    private void PlaceRoomTorches(ChunkUpdateBuilder builder, DungeonRoom room, System.Random rng)
    {
        int count = rng.Next(_p.RoomTorchCountMin, _p.RoomTorchCountMax + 1);

        for (int i = 0; i < count; i++)
        {
            int torchY = room.Center.y + 1 + rng.Next(Math.Max(1, room.Height - 1));

            if (rng.Next(2) == 0)
            {
                bool onLeft = rng.Next(2) == 0;
                int wallX   = room.Center.x + (onLeft ? -room.HalfW - 1 : room.HalfW + 1);
                int torchZ  = room.Center.z + rng.Next(room.HalfD * 2 + 1) - room.HalfD;
                var face    = onLeft ? BlockFace.Right : BlockFace.Left;
                builder.QueueGlobalVoxel(new Vector3Int(wallX, torchY, torchZ), _torchType, FaceAuxData(face));
            }
            else
            {
                bool onFront = rng.Next(2) == 0;
                int wallZ    = room.Center.z + (onFront ? -room.HalfD - 1 : room.HalfD + 1);
                int torchX   = room.Center.x + rng.Next(room.HalfW * 2 + 1) - room.HalfW;
                var face     = onFront ? BlockFace.Front : BlockFace.Back;
                builder.QueueGlobalVoxel(new Vector3Int(torchX, torchY, wallZ), _torchType, FaceAuxData(face));
            }
        }
    }

    private void PlaceCorridorTorches(
        ChunkUpdateBuilder builder,
        Vector3Int start, BlockFace dir,
        int length, System.Random rng)
    {
        BlockFaceHelper.ToDirectionVectors(dir, out var fwd, out _);
        for (int step = 0; step < length; step += 4)
        {
            if (rng.NextDouble() < _p.TorchChancePerCorridorSegment)
            {
                var pos = start + fwd * step + Vector3Int.up * (_p.CorridorHeight - 1);
                builder.QueueGlobalVoxel(pos, _torchType, FaceAuxData(BlockFace.Bottom));
            }
        }
    }

    // Returns the first voxel position outside the room shell in the given direction.
    private Vector3Int ExitPoint(DungeonRoom room, BlockFace dir)
    {
        BlockFaceHelper.ToDirectionVectors(dir, out var fwd, out _);
        int halfExtent = (dir == BlockFace.Front || dir == BlockFace.Back) ? room.HalfD + 2 : room.HalfW + 2;
        return room.Center + fwd * halfExtent;
    }

    private DungeonBounds ComputeCorridorBounds(
        Vector3Int origin, BlockFace dir,
        int length, int width, int height)
    {
        BlockFaceHelper.ToDirectionVectors(dir, out var fwd, out _);
        var end  = origin + fwd * (length - 1);
        int half = width / 2 + 1;

        // Extend sideways (perpendicular to forward) but not backward toward the originating room.
        return new DungeonBounds
        {
            Min = new Vector3Int(
                Math.Min(origin.x, end.x) - (fwd.x == 0 ? half : 0),
                origin.y - 1,
                Math.Min(origin.z, end.z) - (fwd.z == 0 ? half : 0)),
            Max = new Vector3Int(
                Math.Max(origin.x, end.x) + (fwd.x == 0 ? half : 0),
                origin.y + height,
                Math.Max(origin.z, end.z) + (fwd.z == 0 ? half : 0))
        };
    }

    private bool IsPlaceable(DungeonBounds bounds, int surfaceY)
    {
        if (!bounds.ClearsBelowSurface(surfaceY, _p.SurfaceClearance)) return false;
        foreach (var vol in _placedVolumes)
            if (bounds.Intersects(vol)) return false;
        return true;
    }

    private void RegisterVolume(DungeonBounds bounds) => _placedVolumes.Add(bounds);

    private static ushort FaceAuxData(BlockFace face) =>
        PropertySerializer.Serialize(new PlacementFaceProperty(face), (ushort)0, 0);

    private static ushort DoorAuxData(BlockFace face, bool isTopPart)
    {
        ushort data = PropertySerializer.Serialize(new PlacementFaceProperty(face), (ushort)0, 0);
        return PropertySerializer.Serialize(
            new DoorStateProperty { IsTopPart = isTopPart, IsOpen = false },
            data,
            PropertySerializer.GetTotalBitsForType(typeof(PlacementFaceProperty)));
    }

    private void WriteDebugFile(Vector3Int entry, int surfaceY, int ruinHalf, int tunnelBottomY, bool isShaft)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"DUNGEON @ entry=({entry.x},{entry.y},{entry.z}) surfaceY={surfaceY}");
        sb.AppendLine($"Tunnel: {(isShaft ? "shaft" : "ramp")} depth={surfaceY - tunnelBottomY} bottomY={tunnelBottomY}");
        sb.AppendLine($"Ruin: {ruinHalf * 2 + 1}x{ruinHalf * 2 + 1}  corridorWidth={_corridorWidth}  rooms={_rooms.Count}");
        sb.AppendLine();

        sb.AppendLine("ROOMS:");
        for (int i = 0; i < _rooms.Count; i++)
        {
            var r = _rooms[i];
            sb.AppendLine($"  [{i}] level={r.Level}  floor={r.Center.y}  " +
                          $"center=({r.Center.x},{r.Center.z})  " +
                          $"footprint={r.HalfW * 2 + 1}x{r.HalfD * 2 + 1}  height={r.Height}");
        }

        sb.AppendLine();
        sb.AppendLine("CORRIDORS:");
        foreach (var c in _corridorLog)
        {
            string kind = c.LevelDelta == 0 ? "flat"
                        : c.IsStaircase     ? $"stair dy={c.LevelDelta}"
                        :                     $"shaft dy={c.LevelDelta}";
            sb.AppendLine($"  start=({c.Start.x},{c.Start.y},{c.Start.z})  dir={c.Dir}  len={c.Length}  [{kind}]");
        }

        // ── ASCII top-down map ──────────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("MAP (top-down XZ; digit=room level, .=flat corridor, /=stair, |=shaft, #=ruin wall, E=entry):");
        sb.AppendLine("  Z+ ^");

        int minX = entry.x - ruinHalf - 2, maxX = entry.x + ruinHalf + 2;
        int minZ = entry.z - ruinHalf - 2, maxZ = entry.z + ruinHalf + 2;
        foreach (var r in _rooms)
        {
            minX = Math.Min(minX, r.Center.x - r.HalfW - 2);
            maxX = Math.Max(maxX, r.Center.x + r.HalfW + 2);
            minZ = Math.Min(minZ, r.Center.z - r.HalfD - 2);
            maxZ = Math.Max(maxZ, r.Center.z + r.HalfD + 2);
        }
        foreach (var c in _corridorLog)
        {
            BlockFaceHelper.ToDirectionVectors(c.Dir, out var fwd, out _);
            var end = c.Start + fwd * (c.Length - 1);
            minX = Math.Min(minX, Math.Min(c.Start.x, end.x) - 2);
            maxX = Math.Max(maxX, Math.Max(c.Start.x, end.x) + 2);
            minZ = Math.Min(minZ, Math.Min(c.Start.z, end.z) - 2);
            maxZ = Math.Max(maxZ, Math.Max(c.Start.z, end.z) + 2);
        }

        int w = maxX - minX + 1, d = maxZ - minZ + 1;
        var map = new char[d, w];
        for (int z = 0; z < d; z++)
        for (int x = 0; x < w; x++)
            map[z, x] = ' ';

        // Ruin walls
        for (int rz = entry.z - ruinHalf; rz <= entry.z + ruinHalf; rz++)
        for (int rx = entry.x - ruinHalf; rx <= entry.x + ruinHalf; rx++)
        {
            bool onEdge = rx == entry.x - ruinHalf || rx == entry.x + ruinHalf
                       || rz == entry.z - ruinHalf || rz == entry.z + ruinHalf;
            if (onEdge) SetCell(map, rx - minX, rz - minZ, w, d, '#');
        }

        // Corridors (drawn before rooms so rooms overwrite where they overlap)
        foreach (var c in _corridorLog)
        {
            BlockFaceHelper.ToDirectionVectors(c.Dir, out var fwd, out var right);
            char ch = c.LevelDelta == 0 ? '.' : (c.IsStaircase ? '/' : '|');
            int wMin = -(c.Width / 2), wMax = wMin + c.Width - 1;
            for (int step = 0; step < c.Length; step++)
            for (int wi = wMin; wi <= wMax; wi++)
            {
                var pos = c.Start + fwd * step + right * wi;
                SetCell(map, pos.x - minX, pos.z - minZ, w, d, ch);
            }
        }

        // Rooms
        foreach (var r in _rooms)
        {
            char ch = (char)('0' + Math.Min(r.Level, 9));
            for (int rz = r.Center.z - r.HalfD; rz <= r.Center.z + r.HalfD; rz++)
            for (int rx = r.Center.x - r.HalfW; rx <= r.Center.x + r.HalfW; rx++)
                SetCell(map, rx - minX, rz - minZ, w, d, ch);
        }

        // Entry point
        SetCell(map, entry.x - minX, entry.z - minZ, w, d, 'E');

        // Print Z from high to low so Z+ faces up
        for (int z = d - 1; z >= 0; z--)
        {
            sb.Append("  ");
            for (int x = 0; x < w; x++)
                sb.Append(map[z, x]);
            sb.AppendLine();
        }
        sb.AppendLine("         X+ >");

        try
        {
            string dir = Path.Combine(Path.GetTempPath(), "DungeonDebug");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, $"dungeon_{entry.x}_{entry.z}.txt"), sb.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DungeonDebug] write failed: {ex.Message}");
        }
    }

    private static void SetCell(char[,] map, int x, int z, int w, int d, char ch)
    {
        if (x >= 0 && x < w && z >= 0 && z < d) map[z, x] = ch;
    }
}
