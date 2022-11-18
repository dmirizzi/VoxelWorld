using System.Collections.Generic;

public class BlockTypeRegistry
{
    private static BlockTypeRegistry _instance;

    private Dictionary<ushort, BlockTypeBase> _blockTypeMap;

    private BlockTypeRegistry()
    {
        _blockTypeMap = new Dictionary<ushort, BlockTypeBase>();

        //TODO: map via reflection in config?
        _blockTypeMap[5] = new TorchBlockType();
        _blockTypeMap[6] = new WedgeBlockType(6, 4);
        _blockTypeMap[7] = new DoorBlockType(7);
        _blockTypeMap[8] = new LadderBlockType();
    }

    public static BlockTypeBase GetBlockType(ushort type)
    {
        if(_instance == null)
        {
            _instance = new BlockTypeRegistry();
        }

        if(!_instance._blockTypeMap.ContainsKey(type))
        {
            return null;
        }

        return _instance._blockTypeMap[type];
    }
}
