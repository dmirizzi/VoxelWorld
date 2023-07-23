using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public static class BlockDataRepository
{
    static BlockDataRepository()
    {
        var blockTypesContent = Resources.Load<TextAsset>("BlockTypes").text;
        _blockDataList = JsonConvert.DeserializeObject<BlockDataList>(blockTypesContent);

        for(ushort idx = 0; idx < _blockDataList.BlockData.Count; ++idx)
        {
            var blockType = _blockDataList.BlockData[idx];
            _blockDataByName[blockType.Name] = blockType;
            _blockIds[blockType.Name] = idx;
        }
    }

    public static ushort GetBlockTypeId(string name)
    {
        if(!_blockIds.ContainsKey(name))
        {
            throw new System.ArgumentException($"Unknown block {name}");
        }
        return _blockIds[name];
    }

    public static BlockData GetBlockData(string name)
    {
        if(!_blockIds.ContainsKey(name))
        {
            throw new System.ArgumentException($"Unknown block {name}");
        }
        return _blockDataByName[name];
    }

    public static BlockData GetBlockData(int index)
    {
        return _blockDataList.BlockData[index];
    }

    public static IReadOnlyList<BlockData> GetAllBlockData() => _blockDataList.BlockData;

    private static BlockDataList _blockDataList;

    private static Dictionary<string, BlockData> _blockDataByName = new Dictionary<string, BlockData>();

    private static Dictionary<string, ushort> _blockIds = new Dictionary<string, ushort>();

    [System.Serializable]
    private class BlockDataList
    {
        public List<BlockData> BlockData;
    }
}