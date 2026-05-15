using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

public static class PropertySerializer
{
    /// <summary>
    /// Deserialize from bits into an instance of T, starting at the given bit offset in 'data'.
    /// </summary>
    public static T Deserialize<T>(ushort data, int offsetInBits = 0) where T : new()
    {
        var metadataList = EnsureTypeIsCached(typeof(T));
        var obj = new T();

        foreach (var meta in metadataList)
        {
            // Apply the caller-specified offsetInBits + the property offset
            int finalOffset = offsetInBits + meta.Offset;

            // Extract the bits
            int rawValue = SerializationHelper.ExtractBits(data, finalOffset, meta.BitLength);

            // Convert to the property type
            object converted = ConvertBitsToType(rawValue, meta.PropertyInfo.PropertyType);
            meta.Setter(obj, converted);
        }

        return obj;
    }

    /// <summary>
    /// Serialize 'obj' into bits, placing the output into 'oldData', 
    /// starting at the given bit offset in 'oldData'.
    /// </summary>
    public static ushort Serialize<T>(T obj, ushort oldData, int offsetInBits = 0) where T : new()
    {
        var metadataList = EnsureTypeIsCached(typeof(T));
        ushort newData = oldData;

        foreach (var meta in metadataList)
        {
            int finalOffset = offsetInBits + meta.Offset;

            // Get the property value
            var value = meta.Getter(obj);

            // Convert to bits
            ushort bitsToWrite = ConvertTypeToBits(value, meta.BitLength);

            // Overwrite in the finalOffset
            newData = SerializationHelper.OverwriteBits(newData, bitsToWrite, finalOffset, meta.BitLength);
        }

        return newData;
    }

    public static int GetTotalBitsForType(Type type)
    {
        // Ensure the metadata is built/cached
        var metadataList = EnsureTypeIsCached(type);

        // If no properties (edge case), total bits is 0
        if (metadataList.Count == 0)
        {
            return 0;
        }

        // The last property in the sorted metadata list has the highest offset
        // The total number of bits is: last property's offset + its bit length.
        var lastMeta = metadataList[metadataList.Count - 1];
        return lastMeta.Offset + lastMeta.BitLength;
    }

    private static List<PropertyBitMetadata> EnsureTypeIsCached(Type type)
    {
        if (_propertyMetaDataCache.TryGetValue(type, out var list))
            return list;

        list = BuildMetadataForType(type);
        _propertyMetaDataCache[type] = list;
        return list;
    }

    /// <summary>
    /// Build the property metadata for a given type:
    /// - Sort properties by [BitField(Order)] 
    /// - Compute offsets cumulatively 
    /// - Infer 1 bit if no BitLength for a bool, else throw
    /// - Check we don't exceed 16 bits (if that is your desired limit)
    /// </summary>
    private static List<PropertyBitMetadata> BuildMetadataForType(Type type)
    {
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (Prop: p, Attr: p.GetCustomAttribute<BitFieldAttribute>()));

        var propsWithoutAttr = props
            .Where(x => x.Attr == null)
            .Select(x => x.Prop.Name)
            .ToList();

        if(propsWithoutAttr.Any())
        {
            var propertyNames = string.Join(", ", propsWithoutAttr);
            throw new InvalidOperationException(
                $"The type {type.FullName} contains the following properties without BitField attribute: {propertyNames}"
            );
        }

        var propsWithAttr = props.Where(x => x.Attr != null)
            .ToList();

        // Sort by 'Order'
        propsWithAttr.Sort((a, b) => a.Attr.Order.CompareTo(b.Attr.Order));

        var metaList = new List<PropertyBitMetadata>();
        int currentOffset = 0;

        foreach (var (prop, attr) in propsWithAttr)
        {
            int bitLength;
            if (attr.BitLength.HasValue)
            {
                bitLength = attr.BitLength.Value;
            }
            else
            {
                // If no BitLength specified and property is bool => 1 bit
                if (prop.PropertyType == typeof(bool))
                {
                    bitLength = 1;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Property '{prop.Name}' on '{type.Name}' " +
                        "has no BitLength specified, and we only auto-infer 1 bit for booleans."
                    );
                }
            }

            var getter = BuildGetter(prop);
            var setter = BuildSetter(prop);

            metaList.Add(new PropertyBitMetadata
            {
                PropertyInfo = prop,
                BitLength = bitLength,
                Offset = currentOffset,
                Order = attr.Order,
                Getter = getter,
                Setter = setter
            });

            currentOffset += bitLength;

            // If you want to ensure total bits <= 16 for the entire object, do so here:
            if (currentOffset > 16)
            {
                throw new InvalidOperationException(
                    $"Type '{type.Name}' defines more than 16 bits of data (exceeded after '{prop.Name}')."
                );
            }
        }

        return metaList;
    }

    private static Func<object, object> BuildGetter(PropertyInfo prop)
    {
        var objParam = Expression.Parameter(typeof(object), "obj");
        var typedObj = Expression.Convert(objParam, prop.DeclaringType);
        var propertyAccess = Expression.Property(typedObj, prop);
        var convertToObject = Expression.Convert(propertyAccess, typeof(object));
        return (Func<object, object>)Expression.Lambda(convertToObject, objParam).Compile();
    }

    private static Action<object, object> BuildSetter(PropertyInfo prop)
    {
        var objParam = Expression.Parameter(typeof(object), "obj");
        var valueParam = Expression.Parameter(typeof(object), "val");

        var typedObj = Expression.Convert(objParam, prop.DeclaringType);
        var typedValue = Expression.Convert(valueParam, prop.PropertyType);

        var propertyAccess = Expression.Property(typedObj, prop);
        var assign = Expression.Assign(propertyAccess, typedValue);

        // Wrap the assignment so that the final expression is "void"
        var block = Expression.Block(typeof(void), assign);

        // Now it's an Action<object, object>
        return Expression.Lambda<Action<object, object>>(block, objParam, valueParam).Compile();
    }

    private static object ConvertBitsToType(int rawValue, Type targetType)
    {
        if (targetType == typeof(bool))
            return rawValue != 0;
        if (targetType.IsEnum)
            return Enum.ToObject(targetType, rawValue);
        if (targetType == typeof(int))
            return rawValue;
        if (targetType == typeof(uint))
            return (uint)rawValue;

        throw new NotSupportedException("Type {targetType.FullName} is not supported for auxiliary data types");
    }

    private static ushort ConvertTypeToBits(object value, int bitCount)
    {
        if (value is bool b)
            return (ushort)(b ? 1 : 0);
        if (value.GetType().IsEnum)
            return (ushort)Convert.ToInt32(value);
        if (value is int i)
            return (ushort)(i & ((1 << bitCount) - 1));
        if (value is uint ui)
            return (ushort)(ui & ((1 << bitCount) - 1));

        throw new NotSupportedException("Type {targetType.FullName} is not supported for auxiliary data types");
    }

    private static readonly ConcurrentDictionary<Type, List<PropertyBitMetadata>> _propertyMetaDataCache 
        = new ConcurrentDictionary<Type, List<PropertyBitMetadata>>();    
}