public enum DoorOpenState
{
    Closed = 0,
    OpenInward = 1,
    OpenOutward = 2
}

public class DoorStateProperty : IBlockProperty
{
    public int SerializedLengthInBits => 3;

    // Whether this block is the top or bottom part of the door
    public bool IsTopPart { get; set; }

    public DoorOpenState OpenState { get; set; }

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
            OpenState = (DoorOpenState)SerializationHelper.ExtractBits(serializedAuxData, offsetInBits + 1, 2)
        };

    public ushort Serialize(DoorStateProperty auxData, ushort oldAuxData, int offset)
    {
        var isTopPart = (ushort)(auxData.IsTopPart ? 1 : 0);
        var newAuxData = SerializationHelper.OverwriteBits(oldAuxData, isTopPart, offset, 1);
        newAuxData = SerializationHelper.OverwriteBits(newAuxData, (ushort)auxData.OpenState, offset + 1, 2);
        return newAuxData;
    }
}