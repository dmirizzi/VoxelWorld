public interface IBlockProperty
{
    int SerializedLengthInBytes { get; }

    IBlockPropertySerializer<T> GetSerializer<T>();
}