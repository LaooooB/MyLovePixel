using System.Text.Json.Nodes;

namespace MyLovePixel.Persistence;

public sealed class Schema3To4TilemapMigration : IProjectMigration
{
    public int FromVersion => 3;
    public int ToVersion => 4;

    public void Apply(JsonObject manifest, JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(document);

        var documentId = ReadDocumentId(document);
        if (document["seed"] is null)
        {
            document["seed"] = DeriveSeed(documentId);
        }
        else if (document["seed"] is not JsonValue seedValue || !seedValue.TryGetValue<ulong>(out _))
        {
            throw new InvalidOperationException("Existing schema3 document.seed extension must be an unsigned integer to migrate to schema4.");
        }

        EnsureArray(document, "tilesets");
        EnsureArray(document, "tilemaps");
    }

    internal static ulong DeriveSeed(Guid documentId)
    {
        if (documentId == Guid.Empty) throw new ArgumentException("Document id cannot be empty.", nameof(documentId));
        Span<byte> bytes = stackalloc byte[16];
        if (!documentId.TryWriteBytes(bytes)) throw new InvalidOperationException("Unable to encode document id.");

        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        foreach (var value in bytes)
        {
            hash ^= value;
            hash *= prime;
        }
        return hash;
    }

    private static Guid ReadDocumentId(JsonObject document)
    {
        if (document["id"] is JsonValue value &&
            value.TryGetValue<string>(out var text) &&
            Guid.TryParseExact(text, "N", out var id) &&
            id != Guid.Empty)
            return id;
        throw new InvalidOperationException("schema3 document.id must be a non-empty 32-digit Guid before schema4 migration.");
    }

    private static void EnsureArray(JsonObject document, string propertyName)
    {
        if (document[propertyName] is null)
        {
            document[propertyName] = new JsonArray();
            return;
        }
        if (document[propertyName] is not JsonArray)
            throw new InvalidOperationException($"Existing schema3 document.{propertyName} extension must be an array to migrate to schema4.");
    }
}
