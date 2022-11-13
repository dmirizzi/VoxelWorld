using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class BlockTypeBase : IBlockType
{
    public BlockTypeBase(params IBlockProperty[] blockProperties)
    {
        _blockProperties = new List<IBlockProperty>();
        _propertyTypeOffset = new Dictionary<Type, int>();

        int offset = 0;
        foreach(var prop in blockProperties)
        {
            _blockProperties.Add(prop);
            _propertyTypeOffset[prop.GetType()] = offset;
            offset += prop.SerializedLengthInBits;
        }
    }

    public abstract bool OnPlace(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition, BlockFace? placementFace, BlockFace? lookDir);

    public abstract void OnChunkVoxelMeshBuild(VoxelWorld world, Chunk chunk, ushort voxelType, Vector3Int globalVoxelPos, Vector3Int localVoxelPos, ChunkMesh chunkMesh);

    public abstract void OnChunkBuild(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition);

    public abstract bool OnRemove(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition);

    public abstract bool OnUse(VoxelWorld world, Vector3Int globalPosition, BlockFace lookDir);

    public abstract BlockFace GetForwardFace(VoxelWorld world, Vector3Int globalPosition);

    protected T GetProperty<T>(VoxelWorld world, Vector3Int globalPos) where T : IBlockProperty
    {
        var auxData = world.GetVoxelAuxiliaryData(globalPos);
        return auxData != null 
                ? _blockProperties.Single(x => x is T).GetSerializer<T>().Deserialize(auxData.Value, _propertyTypeOffset[typeof(T)]) 
                : default(T);
    }

    protected void SetProperty<T>(VoxelWorld world, Vector3Int globalPos, T property) where T : IBlockProperty
    {
        var oldAuxData = world.GetVoxelAuxiliaryData(globalPos) ?? 0;
        var newAuxData = property.GetSerializer<T>().Serialize(property, oldAuxData, _propertyTypeOffset[typeof(T)]);
        world.SetVoxelAuxiliaryData(globalPos, newAuxData);
    }

    protected void UpdateProperty<T>(VoxelWorld world, Vector3Int globalPos, Func<T, T> updateFunc) where T : IBlockProperty
    {
        var newAuxData = updateFunc(GetProperty<T>(world, globalPos));
        SetProperty(world, globalPos, newAuxData);
    }

    private List<IBlockProperty> _blockProperties;

    private Dictionary<Type, int> _propertyTypeOffset;

    private VoxelWorld _world;
}