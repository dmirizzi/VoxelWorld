
// Taken from https://stackoverflow.com/questions/25201242/json-net-serializing-enums-to-strings-in-dictionaries-by-default-how-to-make-i
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

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