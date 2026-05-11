public class BlockTypeRegistry
{
    private static readonly BlockTypeBase[] _blockTypeMap = BuildMap();

    private static BlockTypeBase[] BuildMap()
    {
        var map = new BlockTypeBase[256];

        //TODO: map via reflection in config?
        map[5] = new TorchBlockType();
        map[6] = new WedgeBlockType(6, 4);
        map[7] = new DoorBlockType(7);
        map[8] = new LadderBlockType();

        return map;
    }

    public static BlockTypeBase GetBlockType(ushort type)
    {
        return _blockTypeMap[type];
    }
}
