namespace Memoria.Utils;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Custom JSON converter for GUID values that handles string representations.
/// Use this to ensure GUIDs are properly serialized/deserialized in JSON caching scenarios.
/// </summary>
public class GuidJsonConverter : JsonConverter<Guid>
{
    /// <summary>
    /// Reads a GUID from JSON. Handles both string and null values.
    /// </summary>
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                // Try to parse the string as a GUID
                if (reader.TryGetGuid(out var guid))
                {
                    return guid;
                }

                // Fallback: manually parse the string
                var stringValue = reader.GetString();
                if (string.IsNullOrWhiteSpace(stringValue))
                {
                    return Guid.Empty;
                }

                return Guid.TryParse(stringValue, out var parsedGuid) 
                    ? parsedGuid 
                    : Guid.Empty;

            case JsonTokenType.Null:
                return Guid.Empty;

            default:
                throw new JsonException(
                    $"Unexpected token '{reader.TokenType}' when parsing a GUID. Expected String or Null.");
        }
    }

    /// <summary>
    /// Writes a GUID to JSON as a string.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}