using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public static class BlockDataRepository
{
    static BlockDataRepository()
    {
        _blockTypeMap = new Dictionary<ushort, BlockTypeBase>();

        var blockTypesContent = Resources.Load<TextAsset>("BlockTypes").text;
        _blockDataList = JsonConvert.DeserializeObject<BlockDataList>(blockTypesContent);

        for(ushort idx = 0; idx < _blockDataList.BlockData.Count; ++idx)
        {
            var blockData = _blockDataList.BlockData[idx];
            _blockDataByName[blockData.Name] = blockData;
            _blockIds[blockData.Name] = idx;

            if(blockData.BlockTypeClass != null)
            {
                try
                {
                    _blockTypeMap[idx] = CreateBlockTypeObject(
                        blockData.BlockTypeClass,
                        idx,
                        blockData,
                        blockData.CustomArgs);    
                }
                catch(Exception e)
                {
                    Debug.Log($"Failed to instantiate block type class {blockData.BlockTypeClass}: {e.Message}\n{e.StackTrace}");
                }            
            }
        }
    }

    private static BlockTypeBase CreateBlockTypeObject(
        string BlockTypeClassName, 
        ushort voxelType, 
        BlockData blockData, 
        Dictionary<string, string> customArgs)
    {
        var args = new Dictionary<string, object>
        {
            { "voxelType", voxelType },
            { "blockData", blockData }
        };

        if(customArgs != null)
        {
            foreach(var customArg in customArgs)
            {
                args[customArg.Key] = customArg.Value;
            }
        }

        var BlockTypeClass = Type.GetType(BlockTypeClassName);
        return (BlockTypeBase)ReflectionHelper.CreateInstance(BlockTypeClass, args);
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

    public static BlockTypeBase GetBlockType(ushort type)
    {
        if(!_blockTypeMap.ContainsKey(type))
        {
            return null;
        }

        return _blockTypeMap[type];
    }    

    public static IReadOnlyList<BlockData> GetAllBlockData() => _blockDataList.BlockData;

    private static BlockDataList _blockDataList;

    private static Dictionary<string, BlockData> _blockDataByName = new Dictionary<string, BlockData>();

    private static Dictionary<string, ushort> _blockIds = new Dictionary<string, ushort>();

    private static Dictionary<ushort, BlockTypeBase> _blockTypeMap;

    [System.Serializable]
    private class BlockDataList
    {
        public List<BlockData> BlockData;
    }
}