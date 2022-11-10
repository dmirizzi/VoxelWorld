
// Taken from https://stackoverflow.com/questions/25201242/json-net-serializing-enums-to-strings-in-dictionaries-by-default-how-to-make-i
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class VoxelColorConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(Color32?);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var hex = reader.Value as string;
        if(hex == null)
        {
            return null;
        }

        byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
        byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
        byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

        return new Color32(r, g, b, 255);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var color = (Color32)value;
        var sb = new StringBuilder();
        sb.Append(color.r.ToString("X2"));
        sb.Append(color.b.ToString("X2"));
        sb.Append(color.g.ToString("X2"));
        writer.WriteRawValue(sb.ToString());
    }
}

public class DictionaryWithEnumKeyConverter<T, U> : JsonConverter where T : System.Enum
{
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var dictionary = (Dictionary<T, U>)value;

        writer.WriteStartObject();

        foreach (KeyValuePair<T, U> pair in dictionary)
        {
            writer.WritePropertyName(Convert.ToInt32(pair.Key).ToString());
            serializer.Serialize(writer, pair.Value);
        }

        writer.WriteEndObject();
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var result = new Dictionary<T, U>();
        var jObject = JObject.Load(reader);

        foreach (var x in jObject)
        {
            T key = (T)Enum.Parse(typeof(T), x.Key);
            U value = (U) x.Value.ToObject(typeof(U));
            result.Add(key, value);
        }

        return result;
    }

    public override bool CanConvert(Type objectType)
    {
        return typeof(IDictionary<T, U>) == objectType;
    }
}