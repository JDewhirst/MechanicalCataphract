using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hexes;

namespace MechanicalCataphract.Data;

/// <summary>
/// Custom JSON converter for the Hex struct, which has readonly fields
/// that System.Text.Json cannot deserialize by default.
/// </summary>
public class HexJsonConverter : JsonConverter<Hex>
{
    public override Hex Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object");

        int q = 0, r = 0, s = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "q":
                        q = reader.GetInt32();
                        break;
                    case "r":
                        r = reader.GetInt32();
                        break;
                    case "s":
                        s = reader.GetInt32();
                        break;
                }
            }
        }

        return new Hex(q, r, s);
    }

    public override void Write(Utf8JsonWriter writer, Hex value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("q", value.q);
        writer.WriteNumber("r", value.r);
        writer.WriteNumber("s", value.s);
        writer.WriteEndObject();
    }
}
