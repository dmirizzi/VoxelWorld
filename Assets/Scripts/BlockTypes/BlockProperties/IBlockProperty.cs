public interface IBlockProperty
{
    int SerializedLengthInBits { get; }

    IBlockPropertySerializer<T> GetSerializer<T>();
}