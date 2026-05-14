using System;
using System.Collections.Generic;
using System.ComponentModel;  // For TypeDescriptor
using System.Linq;
using System.Reflection;

public static class ReflectionHelper
{
    /// <summary>
    /// Creates an instance of <paramref name="type"/> by matching the given
    /// dictionary of parameterName -> objectValue (which may be string or an actual object)
    /// to a constructor's parameters.
    /// </summary>
    /// <param name="type">The type to instantiate.</param>
    /// <param name="namedParameters">
    ///     A dictionary where each key corresponds to a constructor parameter name,
    ///     and each value is either a string or an already-typed object.
    /// </param>
    /// <returns>The newly created object, or throws if no suitable constructor is found.</returns>
    public static object CreateInstance(
        Type type,
        Dictionary<string, object> namedParameters)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));
        if (namedParameters == null)
            throw new ArgumentNullException(nameof(namedParameters));

        // Try each public constructor
        foreach (var ctor in type.GetConstructors())
        {
            var parameters = ctor.GetParameters();

            // Quick check: same number of parameters?
            if (parameters.Length != namedParameters.Count)
                continue;

            // We'll try to build the argument list for this constructor
            object[] args = new object[parameters.Length];
            bool allMatched = true;

            for (int i = 0; i < parameters.Length; i++)
            {
                var parameterInfo = parameters[i];
                var paramName = parameterInfo.Name;

                // Does the dictionary contain an entry for this parameter name?
                if (!namedParameters.TryGetValue(paramName, out object valueObj))
                {
                    allMatched = false;
                    break;
                }

                try
                {
                    // Attempt to convert the dictionary value to the parameter type
                    args[i] = ConvertParameterValue(valueObj, parameterInfo.ParameterType);
                }
                catch
                {
                    // If any conversion fails, this constructor won't work
                    allMatched = false;
                    break;
                }
            }

            // If all parameters matched and converted successfully, invoke the constructor
            if (allMatched)
            {
                return ctor.Invoke(args);
            }
        }

        // If we reach here, no constructor matched
        throw new InvalidOperationException($"No suitable constructor found for type {type.FullName}.");
    }

    /// <summary>
    /// Converts a given object (which may be a string or another object) to the desired type.
    /// </summary>
    private static object ConvertParameterValue(object valueObj, Type desiredType)
    {
        if (valueObj == null)
        {
            // Handle null separately if needed.
            // If 'desiredType' is a non-nullable value type, this will throw.
            // Otherwise, null is fine for class/interface types or Nullable<T>.
            return null;
        }

        // Quick success check if the existing type is already compatible
        if (desiredType.IsInstanceOfType(valueObj))
        {
            // E.g. if the value is an int and desired type is int or object or a base class/interface
            return valueObj;
        }

        // If the value is a string, try to parse/convert via TypeDescriptor or Convert.ChangeType
        if (valueObj is string stringVal)
        {
            return ConvertStringToType(stringVal, desiredType);
        }
        else
        {
            // Last resort: attempt system conversion
            // e.g. converting an int to a double, or a float to an int, etc.
            return Convert.ChangeType(valueObj, desiredType);
        }
    }

    /// <summary>
    /// Converts a string to the desired type using TypeDescriptor, falling back to Convert.ChangeType.
    /// </summary>
    private static object ConvertStringToType(string stringVal, Type desiredType)
    {
        // First try TypeDescriptor for typical conversions (primitives, enums, DateTime, etc.)
        TypeConverter converter = TypeDescriptor.GetConverter(desiredType);
        if (converter != null && converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromString(stringVal);
        }

        // If TypeDescriptor couldn't handle it, try Convert.ChangeType
        return Convert.ChangeType(stringVal, desiredType);
    }
}