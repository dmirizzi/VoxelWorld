public class DoorStateProperty : IBlockProperty
{
    public int SerializedLengthInBits => 2;

    // Whether this block is the top or bottom part of the door
    public bool IsTopPart { get; set; }

    public bool IsOpen { get; set; }

    public IBlockPropertySerializer<T> GetSerializer<T>()
    {
        return (IBlockPropertySerializer<T>)new DoorStatePropertySerializer();
    }
}

public class DoorStatePropertySerializer : IBlockPropertySerializer<DoorStateProperty>
{
    public DoorStateProperty Deserialize(ushort serializedAuxData, int offsetInBits) => 
        new DoorStateProperty
        {
            IsTopPart = SerializationHelper.ExtractBits(serializedAuxData, offsetInBits, 1) == 1,
            IsOpen = SerializationHelper.ExtractBits(serializedAuxData, offsetInBits + 1, 1) == 1
        };

    public ushort Serialize(DoorStateProperty auxData, ushort oldAuxData, int offset)
    {
        var isTopPart = (ushort)(auxData.IsTopPart ? 1 : 0);
        var isOpen = (ushort)(auxData.IsOpen ? 1 : 0);
        var newAuxData = SerializationHelper.OverwriteBits(oldAuxData, isTopPart, offset, 1);
        newAuxData = SerializationHelper.OverwriteBits(newAuxData, isOpen, offset + 1, 1);
        return newAuxData;
    }
}