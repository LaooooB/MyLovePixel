using System.Text.Json;

namespace MyLovePixel.PluginSdk;

public static class PluginScriptValueCodec
{
    public static byte[] Serialize(PluginValue? value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Write(writer, value);
        }
        return stream.ToArray();
    }

    public static PluginValue? Deserialize(ReadOnlySpan<byte> utf8)
    {
        using var document = JsonDocument.Parse(utf8.ToArray());
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Null) return null;
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("Plugin script value must be an object or null.");

        var kind = root.GetProperty("kind").GetString()
            ?? throw new JsonException("Plugin script value kind cannot be null.");
        var value = root.GetProperty("value");
        return kind switch
        {
            "integer" => PluginValue.Integer(value.GetInt64()),
            "number" => PluginValue.Number(value.GetDouble()),
            "boolean" => PluginValue.Boolean(value.GetBoolean()),
            "color" => PluginValue.Color(new PluginRgba32(
                value.GetProperty("r").GetByte(),
                value.GetProperty("g").GetByte(),
                value.GetProperty("b").GetByte(),
                value.GetProperty("a").GetByte())),
            "point" => PluginValue.Point(new PluginIntPoint(
                value.GetProperty("x").GetInt32(),
                value.GetProperty("y").GetInt32())),
            "identifier" => PluginValue.Identifier(value.GetGuid()),
            "text" => PluginValue.Text(value.GetString() ?? throw new JsonException("Plugin script text cannot be null.")),
            _ => throw new JsonException($"Unknown plugin script value kind '{kind}'."),
        };
    }

    private static void Write(Utf8JsonWriter writer, PluginValue? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("kind", GetKindName(value.Kind));
        writer.WritePropertyName("value");
        switch (value.Kind)
        {
            case PluginValueKind.Integer:
                writer.WriteNumberValue(value.IntegerValue);
                break;
            case PluginValueKind.Number:
                writer.WriteNumberValue(value.NumberValue);
                break;
            case PluginValueKind.Boolean:
                writer.WriteBooleanValue(value.BooleanValue);
                break;
            case PluginValueKind.Color:
                writer.WriteStartObject();
                writer.WriteNumber("r", value.ColorValue.R);
                writer.WriteNumber("g", value.ColorValue.G);
                writer.WriteNumber("b", value.ColorValue.B);
                writer.WriteNumber("a", value.ColorValue.A);
                writer.WriteEndObject();
                break;
            case PluginValueKind.Point:
                writer.WriteStartObject();
                writer.WriteNumber("x", value.PointValue.X);
                writer.WriteNumber("y", value.PointValue.Y);
                writer.WriteEndObject();
                break;
            case PluginValueKind.Identifier:
                writer.WriteStringValue(value.IdentifierValue);
                break;
            case PluginValueKind.Text:
                writer.WriteStringValue(value.TextValue);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), $"Unsupported plugin script value kind '{value.Kind}'.");
        }
        writer.WriteEndObject();
    }

    private static string GetKindName(PluginValueKind kind) => kind switch
    {
        PluginValueKind.Integer => "integer",
        PluginValueKind.Number => "number",
        PluginValueKind.Boolean => "boolean",
        PluginValueKind.Color => "color",
        PluginValueKind.Point => "point",
        PluginValueKind.Identifier => "identifier",
        PluginValueKind.Text => "text",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
