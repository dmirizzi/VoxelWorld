using System;

public class ReadOnly3DArray<T>
{
    public ReadOnly3DArray(T[,,] array)
    {
        _array = array;
    }

    public T this[int index1, int index2, int index3]
    {
        get => _array[index1, index2, index3];
        set => throw new InvalidOperationException("Read only array cannot be written to");
    }

    private T[,,] _array;
}