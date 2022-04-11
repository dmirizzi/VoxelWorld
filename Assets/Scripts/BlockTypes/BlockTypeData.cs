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
public class BlockType
{
    public string Name { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public BlockRenderType RenderType { get; set; }

    // Which faces of a block are opaque
    [JsonProperty (ItemConverterType = typeof(StringEnumConverter))]
    public List<BlockFaceSelector> OpaqueFaces { get; set; }

    public bool Transparent { get; set; }

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

    public int[] GetFaceTextureTileCoords(BlockFace face)
    {
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
            throw new System.ArgumentException($"Invalid FaceTextureTileCoords for block type {Name}. Must either contain a set of tile coords for specific faces (and optionally a default) or one specifier for all faces");
        }

        if(coords.Length != 2)
        {
            throw new System.ArgumentException($"Invalid FaceTextureTileCoords for block type {Name}. Tile coordinates must have exactly two values - x and y.");
        }

        return coords;
    }
}

[System.Serializable]
public class BlockTypeList
{
    public List<BlockType> BlockTypes;
}

public static class BlockTypeData
{

    static BlockTypeData()
    {
        var blockTypesContent = Resources.Load<TextAsset>("BlockTypes").text;
        _blockTypeList = JsonConvert.DeserializeObject<BlockTypeList>(blockTypesContent);

        for(ushort idx = 0; idx < _blockTypeList.BlockTypes.Count; ++idx)
        {
            var blockType = _blockTypeList.BlockTypes[idx];
            _blockTypesByName[blockType.Name] = blockType;
            _blockTypeIds[blockType.Name] = idx;
        }
    }

    public static ushort GetBlockTypeId(string name)
    {
        if(!_blockTypeIds.ContainsKey(name))
        {
            throw new System.ArgumentException($"Unknown block {name}");
        }
        return _blockTypeIds[name];
    }

    public static BlockType GetBlockType(string name)
    {
        if(!_blockTypeIds.ContainsKey(name))
        {
            throw new System.ArgumentException($"Unknown block {name}");
        }
        return _blockTypesByName[name];
    }

    public static BlockType GetBlockData(int index)
    {
        return _blockTypeList.BlockTypes[index];
    }

    private static BlockTypeList _blockTypeList;

    private static Dictionary<string, BlockType> _blockTypesByName = new Dictionary<string, BlockType>();

    private static Dictionary<string, ushort> _blockTypeIds = new Dictionary<string, ushort>();
}