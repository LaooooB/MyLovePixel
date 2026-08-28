using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace MyLovePixel.Persistence;

internal sealed class Schema1To2AnimationMigration : IProjectMigration
{
    public int FromVersion => 1;
    public int ToVersion => 2;

    public void Apply(JsonObject manifest, JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(document);

        if (document["animation"] is null)
            document["animation"] = CreateAnimation(document);
    }

    private static JsonObject CreateAnimation(JsonObject document)
    {
        var documentId = document["id"]?.GetValue<string>() ?? string.Empty;
        return new JsonObject
        {
            ["clips"] = new JsonArray(),
            ["tags"] = new JsonArray(),
            ["slices"] = new JsonArray(),
            ["pivotTrack"] = CreateTrack(documentId, "pivot", "Pivot"),
            ["hitboxTrack"] = CreateTrack(documentId, "hitbox", "Hitboxes"),
            ["hurtboxTrack"] = CreateTrack(documentId, "hurtbox", "Hurtboxes"),
            ["socketTrack"] = CreateTrack(documentId, "socket", "Sockets"),
            ["eventTrack"] = CreateTrack(documentId, "event", "Events"),
        };
    }

    private static JsonObject CreateTrack(string documentId, string key, string name) => new()
    {
        ["id"] = DeriveStableId(documentId, key),
        ["name"] = name,
        ["keyframes"] = new JsonArray(),
    };

    private static string DeriveStableId(string documentId, string key)
    {
        var input = Encoding.UTF8.GetBytes($"MyLovePixel:schema2:{documentId}:{key}");
        var digest = SHA256.HashData(input);
        Span<byte> guidBytes = stackalloc byte[16];
        digest.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes).ToString("N");
    }
}
