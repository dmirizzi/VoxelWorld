public interface IBlockPropertySerializer<T>
{
    ushort Serialize(T auxData);

    T Deserialize(ushort serializedAuxData, int offsetInBytes);

}