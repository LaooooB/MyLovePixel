using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace MyLovePixel.Persistence;

internal sealed class Schema2To3PaletteMigration : IProjectMigration
{
    public int FromVersion => 2;
    public int ToVersion => 3;

    public void Apply(JsonObject manifest, JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(document);

        if (document["palettes"] is null)
            document["palettes"] = new JsonArray();

        if (document["animation"] is not JsonObject animation)
            throw new InvalidOperationException("Schema 2 document must contain animation metadata.");

        if (animation["colorCycleTrack"] is null)
        {
            var documentId = document["id"]?.GetValue<string>() ?? string.Empty;
            animation["colorCycleTrack"] = new JsonObject
            {
                ["id"] = DeriveStableId(documentId),
                ["name"] = "Color Cycles",
                ["keyframes"] = new JsonArray(),
            };
        }
    }

    private static string DeriveStableId(string documentId)
    {
        var input = Encoding.UTF8.GetBytes($"MyLovePixel:schema3:{documentId}:color-cycle");
        var digest = SHA256.HashData(input);
        Span<byte> guidBytes = stackalloc byte[16];
        digest.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes).ToString("N");
    }
}
