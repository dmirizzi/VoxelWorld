using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class BlockTypeBase
{
    public BlockTypeBase(ushort voxelType, BlockData blockData, params IBlockProperty[] blockProperties)
    {
        VoxelType = voxelType;
        BlockData = blockData;

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

    // Is called when the player attempts to place a block of this type. This will always be called before the build methods,
    // so that voxel auxiliary data can be set for building.
    // Returns true if it can be placed, false otherwise
    public virtual bool OnPlace(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition, BlockFace? placementFace, BlockFace? lookDir) { return true; }

    // Called when a chunk mesh is being built. Custom meshes can be added here that will use
    // the global voxel material (ie texture atlas).
    public virtual void OnChunkVoxelMeshBuild(VoxelWorld world, Chunk chunk, ushort voxelType, Vector3Int globalVoxelPos, Vector3Int localVoxelPos, ChunkMesh chunkMesh) {}

    // Called, when the chunk is (re)built. Any GameObject blocks needed shall be added to the chunk in this method.
    public virtual void OnChunkBuild(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition) {}

    // Is called when the player attempts to remove/replace a block of this type.
    // Returns true if it can be removed, false otherwise
    public virtual bool OnRemove(VoxelWorld world, Chunk chunk, Vector3Int globalPosition, Vector3Int localPosition) { return true; }

    // Is called when the player looks at and uses a specific block of this type
    public virtual bool OnUse(VoxelWorld world, Vector3Int globalPosition, BlockFace lookDir) { return true; }

    // Called when the player starts touching a specific block of this type
    public virtual void OnTouchStart(VoxelWorld world, Vector3Int globalPosition, PlayerController player) {}

    // Called when the player stops touching a specific block of this type
    public virtual void OnTouchEnd(VoxelWorld world, Vector3Int globalPosition, PlayerController player) {}

    // This needs to return the globally forward (i.e. in +z direction) facing voxel face, e.g. if a voxel
    // is rotated right, it needs to return left etc.
    // This is important for hidden face removal during chunk mesh building.
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

    protected BlockData BlockData { get; private set; }

    protected ushort VoxelType { get; private set; }

    private List<IBlockProperty> _blockProperties;

    private Dictionary<Type, int> _propertyTypeOffset;
}