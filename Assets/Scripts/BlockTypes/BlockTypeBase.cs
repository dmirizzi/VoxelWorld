using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BlockTypeBase
{
    public BlockTypeBase(VoxelWorld world, List<IBlockProperty> blockProperties)
    {
        _world = world;

        int offset = 0;
        foreach(var prop in blockProperties)
        {
            _blockProperties.Add(prop);
            _propertyTypeOffset[prop.GetType()] = offset;
            offset += prop.SerializedLengthInBytes;
        }
    }

    bool OnPlace(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition, BlockFace? placementFace, BlockFace? lookDir)
    {

    }

    protected T GetProperty<T>(Vector3Int globalPos, ushort serializedAuxData) where T : IBlockProperty
    {
        return _blockProperties.Single(x => x is T).GetSerializer<T>().Deserialize(serializedAuxData, _propertyTypeOffset[typeof(T)]);
    }

    protected void SetProperty<T>(Vector3Int globalPos, T property) where T : IBlockProperty
    {
        _world.SetVoxelAuxiliaryData(globalPos,
            property.GetSerializer<T>().Serialize());
    }

    private List<IBlockProperty> _blockProperties;

    private Dictionary<Type, int> _propertyTypeOffset;

    private VoxelWorld _world;
}