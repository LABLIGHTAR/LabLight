using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PropertiesConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(Dictionary<string, object>);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var result = new Dictionary<string, object>();

        // Read the JSON object
        JObject jo = JObject.Load(reader);

        foreach (var prop in jo.Properties())
        {
            var value = ConvertToken(prop.Value);
            result[prop.Name] = value;
        }

        return result;
    }

    private object ConvertToken(JToken token)
    {
        switch (token.Type)
        {
            case JTokenType.Object:
                // Handle special Unity types
                if (TryParseColor(token as JObject, out var color))
                    return color;
                if (TryParseVector3(token as JObject, out var vector))
                    return vector;
                
                // Default object handling
                return token.ToObject<Dictionary<string, object>>();

            case JTokenType.Array:
                return token.ToObject<List<object>>();

            case JTokenType.Integer:
                return token.Value<long>();

            case JTokenType.Float:
                return token.Value<float>();

            case JTokenType.String:
                var stringValue = token.Value<string>();
                return stringValue;

            case JTokenType.Boolean:
                return token.Value<bool>();

            case JTokenType.Null:
                return null;

            default:
                return token.Value<object>();
        }
    }

    private bool TryParseColor(JObject jo, out Color color)
    {
        color = Color.white;
        if (jo == null) return false;

        // Check if object has r,g,b properties
        if (jo.ContainsKey("r") && jo.ContainsKey("g") && jo.ContainsKey("b"))
        {
            color = new Color(
                jo["r"].Value<float>(),
                jo["g"].Value<float>(),
                jo["b"].Value<float>(),
                jo.ContainsKey("a") ? jo["a"].Value<float>() : 1f
            );
            return true;
        }

        return false;
    }

    private bool TryParseVector3(JObject jo, out Vector3 vector)
    {
        vector = Vector3.zero;
        if (jo == null) return false;

        // Check if object has x,y,z properties
        if (jo.ContainsKey("x") && jo.ContainsKey("y") && jo.ContainsKey("z"))
        {
            vector = new Vector3(
                jo["x"].Value<float>(),
                jo["y"].Value<float>(),
                jo["z"].Value<float>()
            );
            return true;
        }

        return false;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var dictionary = (Dictionary<string, object>)value;
        writer.WriteStartObject();

        foreach (var kvp in dictionary)
        {
            writer.WritePropertyName(kvp.Key);
            WriteValue(writer, kvp.Value, serializer);
        }

        writer.WriteEndObject();
    }

    private void WriteValue(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        switch (value)
        {
            case Color color:
                writer.WriteStartObject();
                writer.WritePropertyName("r");
                writer.WriteValue(color.r);
                writer.WritePropertyName("g");
                writer.WriteValue(color.g);
                writer.WritePropertyName("b");
                writer.WriteValue(color.b);
                writer.WritePropertyName("a");
                writer.WriteValue(color.a);
                writer.WriteEndObject();
                break;

            case Vector3 vector:
                writer.WriteStartObject();
                writer.WritePropertyName("x");
                writer.WriteValue(vector.x);
                writer.WritePropertyName("y");
                writer.WriteValue(vector.y);
                writer.WritePropertyName("z");
                writer.WriteValue(vector.z);
                writer.WriteEndObject();
                break;

            default:
                serializer.Serialize(writer, value);
                break;
        }
    }
} 