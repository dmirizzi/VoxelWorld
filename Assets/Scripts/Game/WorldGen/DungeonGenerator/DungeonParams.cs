using UnityEngine;

[System.Serializable]
public struct DungeonParams
{
    [Header("Size Class Selection (cumulative roll thresholds)")]
    public float SmallDungeonThreshold;   // roll < this → small
    public float MediumDungeonThreshold;  // roll < this → medium; above → large

    [Header("Room Budgets per Size Class")]
    public int SmallRoomsMin;
    public int SmallRoomsRange;           // actual rooms = Min + rng * Range
    public int MediumRoomsMin;
    public int MediumRoomsRange;
    public int LargeRoomsMin;
    public int LargeRoomsRange;

    [Header("Max Dungeon Levels per Size Class")]
    public int SmallMaxLevels;            // number of vertical layers the dungeon can span
    public int MediumMaxLevels;
    public int LargeMaxLevels;

    [Header("Surface Ruin")]
    public int   RuinSizeMin;             // outer wall footprint (square side length, in voxels)
    public int   RuinSizeRange;
    public int   RuinWallHeightMin;       // how many voxels tall the ruin walls start
    public int   RuinWallHeightRange;
    public float RuinDecayChance;         // per-block removal probability for non-top rows
    public float RuinTopRowDecayChance;   // higher removal chance on the top wall row
    public float RuinTorchChance;         // per surviving wall-top block: chance to place a torch

    [Header("Entry Tunnel")]
    public int TunnelDepthMin;            // vertical drop from ruin floor to dungeon level
    public int TunnelDepthRange;
    public int TunnelWidth;              // interior air width of shaft or ramp

    [Header("Corridors")]
    public int CorridorWidthMin;          // rolled once per dungeon and kept consistent (3–6)
    public int CorridorWidthRange;
    public int CorridorLengthMin;
    public int CorridorLengthRange;
    public int CorridorHeight;            // fixed clearance in voxels above the floor
    public int MaxExitsPerRoom;           // upper bound on exits grown from any single room

    [Header("Rooms — Size Class Weights")]
    public float SmallRoomWeight;         // relative draw probability for a small room
    public float MediumRoomWeight;
    public float LargeRoomWeight;

    [Header("Rooms — Small")]
    public int SmallRoomSizeMin;          // XZ half-extent (actual inner side = 2*min..2*(min+range))
    public int SmallRoomSizeRange;
    public int SmallRoomHeightMin;
    public int SmallRoomHeightRange;

    [Header("Rooms — Medium")]
    public int MediumRoomSizeMin;
    public int MediumRoomSizeRange;
    public int MediumRoomHeightMin;
    public int MediumRoomHeightRange;

    [Header("Rooms — Large")]
    public int LargeRoomSizeMin;
    public int LargeRoomSizeRange;
    public int LargeRoomHeightMin;
    public int LargeRoomHeightRange;

    [Header("Level Changes")]
    public int   LevelStepYMin;               // vertical distance between dungeon levels
    public int   LevelStepYRange;
    public float LevelChangeChance;           // per-room probability of spawning on a new level
    public float StaircaseChanceUpperLevel;   // prob. of staircase vs ladder drop: level 0→1
    public float StaircaseChanceMidLevel;     // level 1→2
    public float StaircaseChanceLowerLevel;   // level 2→3+

    [Header("Doors")]
    public float DoorChance;                  // probability a corridor–room junction gets a door
    public float BrokenEntryChance;           // entry gate: broken wall instead of door wall

    [Header("Torches")]
    public float TorchChancePerCorridorSegment; // per 4-block corridor segment
    public int   RoomTorchCountMin;
    public int   RoomTorchCountMax;
    public float DoorTorchChance;               // per door side

    [Header("Safety Margins")]
    public int SurfaceClearance;  // min voxels between element ceiling and terrain surface
    public int OverlapPadding;    // min separation between placed-element AABBs

    public static DungeonParams Default => new DungeonParams
    {
        SmallDungeonThreshold  = 0.55f,   // 55% of dungeons are small
        MediumDungeonThreshold = 0.90f,   // 35% medium; 10% large

        SmallRoomsMin    = 4,  SmallRoomsRange  = 3,   // 4–7 rooms
        MediumRoomsMin   = 8,  MediumRoomsRange = 6,   // 8–14 rooms
        LargeRoomsMin    = 15, LargeRoomsRange  = 8,   // 15–23 rooms

        SmallMaxLevels   = 2,
        MediumMaxLevels  = 3,
        LargeMaxLevels   = 4,

        RuinSizeMin           = 8,     // outer wall footprint 8–14 voxels per side
        RuinSizeRange         = 6,
        RuinWallHeightMin     = 2,
        RuinWallHeightRange   = 3,     // 2–5 blocks tall
        RuinDecayChance       = 0.30f, // 30% of non-corner wall blocks removed
        RuinTopRowDecayChance = 0.65f, // top row heavily decayed
        RuinTorchChance       = 0.15f, // 15% of surviving top-row blocks get a torch

        TunnelDepthMin   = 8,
        TunnelDepthRange = 8,          // 8–16 blocks deep
        TunnelWidth      = 2,

        CorridorWidthMin    = 3,
        CorridorWidthRange  = 3,       // 3–6 wide, rolled once per dungeon
        CorridorLengthMin   = 5,
        CorridorLengthRange = 11,      // 5–16 blocks long
        CorridorHeight      = 3,
        MaxExitsPerRoom     = 3,

        SmallRoomWeight  = 0.60f,
        MediumRoomWeight = 0.30f,
        LargeRoomWeight  = 0.10f,

        SmallRoomSizeMin    = 2, SmallRoomSizeRange  = 2,  // inner side 4–8 voxels
        SmallRoomHeightMin  = 3, SmallRoomHeightRange = 0,
        MediumRoomSizeMin   = 3, MediumRoomSizeRange = 3,  // inner side 6–12 voxels
        MediumRoomHeightMin = 3, MediumRoomHeightRange = 1,
        LargeRoomSizeMin    = 5, LargeRoomSizeRange  = 4,  // inner side 10–18 voxels
        LargeRoomHeightMin  = 4, LargeRoomHeightRange = 2,

        LevelStepYMin              = 5,
        LevelStepYRange            = 4,   // 5–9 blocks between levels
        LevelChangeChance          = 0.30f,
        StaircaseChanceUpperLevel  = 0.70f,
        StaircaseChanceMidLevel    = 0.50f,
        StaircaseChanceLowerLevel  = 0.30f,

        DoorChance        = 0.40f,
        BrokenEntryChance = 0.50f,

        TorchChancePerCorridorSegment = 0.25f,
        RoomTorchCountMin             = 1,
        RoomTorchCountMax             = 3,
        DoorTorchChance               = 0.50f,

        SurfaceClearance = 2,
        OverlapPadding   = 1,
    };
}
