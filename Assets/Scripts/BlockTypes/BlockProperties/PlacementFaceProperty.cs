using UnityEngine;

public class PlacementFaceProperty : IBlockProperty
{
    public int SerializedLengthInBits => 3;

    public BlockFace PlacementFace { get; private set; }

    public PlacementFaceProperty()
    {        
    }

    public PlacementFaceProperty(BlockFace placementFace)
    {
        PlacementFace = placementFace;
    }

    public IBlockPropertySerializer<T> GetSerializer<T>()
    {
        return (IBlockPropertySerializer<T>)new PlacementFacePropertySerializer();
    }
}

public class PlacementFacePropertySerializer : IBlockPropertySerializer<PlacementFaceProperty>
{
    public PlacementFaceProperty Deserialize(ushort serializedAuxData, int offsetInBits)
    {
        var blockFace = (BlockFace)SerializationHelper.ExtractBits(serializedAuxData, offsetInBits, 3);
        return new PlacementFaceProperty(blockFace);
    }

    public ushort Serialize(PlacementFaceProperty auxData, ushort oldAuxData, int offset)
    {
        var val = SerializationHelper.OverwriteBits(oldAuxData, (ushort)auxData.PlacementFace, offset, 3);
        return val;
    }
}