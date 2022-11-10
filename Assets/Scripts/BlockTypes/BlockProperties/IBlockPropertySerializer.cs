public interface IBlockPropertySerializer<T>
{
    ushort Serialize(T auxData, ushort oldAuxData, int offset);

    T Deserialize(ushort serializedAuxData, int offsetInBits);

}