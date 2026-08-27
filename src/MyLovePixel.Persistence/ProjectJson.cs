using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyLovePixel.Persistence;

internal static class ProjectJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
    };

    public static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    public static T Deserialize<T>(ReadOnlySpan<byte> utf8, string entryName)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(utf8, Options)
                ?? throw new PixelProjectException(PixelProjectErrorCode.InvalidJson, $"Entry '{entryName}' contained JSON null.", entryName);
        }
        catch (PixelProjectException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new PixelProjectException(PixelProjectErrorCode.InvalidJson, $"Entry '{entryName}' contains invalid JSON.", entryName, ex);
        }
    }

    public static JsonObject ParseObject(ReadOnlySpan<byte> utf8, string entryName)
    {
        try
        {
            var node = JsonNode.Parse(utf8);
            return node as JsonObject
                ?? throw new PixelProjectException(PixelProjectErrorCode.InvalidJson, $"Entry '{entryName}' must contain a JSON object.", entryName);
        }
        catch (PixelProjectException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new PixelProjectException(PixelProjectErrorCode.InvalidJson, $"Entry '{entryName}' contains invalid JSON.", entryName, ex);
        }
    }
}
