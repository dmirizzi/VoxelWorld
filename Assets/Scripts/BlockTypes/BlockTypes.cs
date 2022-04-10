

using System.Collections.Generic;

public class BlockTypes
{
    private static BlockTypes _instance;

    private Dictionary<VoxelType, IBlockType> _blockTypeMap;

    private BlockTypes()
    {
        _blockTypeMap = new Dictionary<VoxelType, IBlockType>();

        _blockTypeMap[VoxelType.Torch] = new TorchBlockType();
        _blockTypeMap[VoxelType.CobblestoneWedge] = new WedgeBlockType(VoxelType.CobblestoneWedge, VoxelType.Cobblestone);
    }

    public static IBlockType GetBlockType(VoxelType type)
    {
        if(_instance == null)
        {
            _instance = new BlockTypes();
        }

        if(!_instance._blockTypeMap.ContainsKey(type))
        {
            return null;
        }

        return _instance._blockTypeMap[type];
    }
}
