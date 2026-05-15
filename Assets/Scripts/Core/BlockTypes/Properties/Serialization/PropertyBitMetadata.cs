using System;
using System.Reflection;

internal class PropertyBitMetadata
{
    public PropertyInfo PropertyInfo { get; set; }

    /// <summary>
    /// Final computed bit length (either from attribute or inferred).
    /// </summary>
    public int BitLength { get; set; }

    /// <summary>
    /// Offset in the ushort bitfield, computed by summing lengths of preceding properties.
    /// </summary>
    public int Offset { get; set; }

    /// <summary>
    /// The 'Order' from the BitFieldAttribute, used to sort properties.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Delegates for fast property get/set.
    /// </summary>
    public Func<object, object> Getter { get; set; }
    public Action<object, object> Setter { get; set; }
}