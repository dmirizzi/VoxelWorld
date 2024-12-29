public static class SerializationHelper
{
    public static ushort ExtractBits(ushort data, int offsetInBits, int lengthInBits)
    {
        return (ushort)((data >> offsetInBits) & ((1 << lengthInBits) - 1));
    }

    public static ushort OverwriteBits(ushort oldData, ushort newData, int offsetInBits, int lengthInBits)
    {
        var newDataMask  = (ushort)(newData << offsetInBits);
        var newDataZeroMask = ~(((1 << lengthInBits) - 1) << offsetInBits);
        oldData &= (ushort)newDataZeroMask;
        return (ushort)(oldData | newDataMask);
    }
}