using System;

/// <summary>
/// Attribute that indicates a property should be part of bitfield serialization.
/// The 'Order' property is mandatory to ensure consistent ordering.
/// The 'BitLength' is optional.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class BitFieldAttribute : Attribute
{
    /// <summary>
    /// The order in which this property appears in the bitfield, starting at 1.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// If null, we'll infer bit length (e.g., 1 for bool).
    /// Otherwise, we use the provided length (e.g., 4 bits for a small enum).
    /// </summary>
    public int? BitLength { get; }

    public BitFieldAttribute(int order)
    {
        Order = order;
    }

    public BitFieldAttribute(int order, int bitLength)
    {
        Order = order;
        BitLength = bitLength;
    }
}