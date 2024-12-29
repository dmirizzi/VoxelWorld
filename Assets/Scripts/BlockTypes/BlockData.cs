using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

public enum BlockRenderType
{
    // Block will be rendered as a simple cube, i.e. voxel
    Voxel,

    // Block will provide its own custom mesh via the IBlockType interface
    CustomMesh,

    // Block will provide its own GameObject via the IBlockType interface
    GameObject
}

public enum BlockFace
{
    Top = 0,
    Bottom,
    Front,
    Back,
    Left,
    Right
}

public enum BlockFaceSelector
{
    Top,
    Bottom,
    Front,
    Back,
    Left,
    Right,
    All,
    None,
    Default
}

[Serializable]
public class BlockData
{
    public string Name { get; set; }

    public string BlockTypeClass { get; set; }

    public Dictionary<string, string> CustomArgs { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public BlockRenderType RenderType { get; set; }

    // Which faces of a block are opaque
    [JsonProperty (ItemConverterType = typeof(StringEnumConverter))]
    public List<BlockFaceSelector> OpaqueFaces { get; set; }

    public bool Transparent { get; set; }

    [JsonConverter (typeof(VoxelColorConverter))]
    public Color32? LightColor { get; set; }

    // Mapping of block faces -> texture tile coordinates
    [JsonConverter (typeof(DictionaryWithEnumKeyConverter<BlockFaceSelector, int[]>))]
    public Dictionary<BlockFaceSelector, int[]> FaceTextureTileCoords { get; set; }

    // Y-Offset of the top block face (> 0 will lower the top face)
    public float HeightOffset { get; set; }

    public bool IsFaceOpaque(BlockFace face, int yRotation)
    {
        if(yRotation != 0)
        {
            var newFace = BlockFaceHelper.RotateFaceY(face, yRotation);
            face = newFace;
        }

        var selector = BlockFaceHelper.ToBlockFaceSelector(face);

        if(OpaqueFaces.Contains(selector))
        {
            return true;
        }
        if(OpaqueFaces.Contains(BlockFaceSelector.All))
        {
            return true;
        }
        return false;
    }

    public bool TryGetFaceTextureTileCoords(BlockFace face, out int[] tileCoords)
    {
        if(FaceTextureTileCoords == null)
        {
            tileCoords = null;
            return false;
        }

        var blockFaceSelector = BlockFaceHelper.ToBlockFaceSelector(face);
        int[] coords = null;
        if(FaceTextureTileCoords.ContainsKey(blockFaceSelector))
        {
            coords = FaceTextureTileCoords[blockFaceSelector];
        }
        else if(FaceTextureTileCoords.ContainsKey(BlockFaceSelector.All))
        {
            coords = FaceTextureTileCoords[BlockFaceSelector.All];
        }
        else if(FaceTextureTileCoords.ContainsKey(BlockFaceSelector.Default))
        {
            coords = FaceTextureTileCoords[BlockFaceSelector.Default];
        }

        if(coords == null)
        {
            UnityEngine.Debug.LogWarning($"Invalid FaceTextureTileCoords for block type {Name}. Must either contain a set of tile coords for specific faces (and optionally a default) or one specifier for all faces");
            tileCoords = null;
            return false;
        }

        if(coords.Length != 2)
        {
            UnityEngine.Debug.LogWarning($"Invalid FaceTextureTileCoords for block type {Name}. Tile coordinates must have exactly two values - x and y.");
            tileCoords = null;
            return false;
        }

        tileCoords = coords;
        return true;
    }
}
