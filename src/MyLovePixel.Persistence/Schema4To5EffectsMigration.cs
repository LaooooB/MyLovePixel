using System.Text.Json.Nodes;

namespace MyLovePixel.Persistence;

public sealed class Schema4To5EffectsMigration : IProjectMigration
{
    public int FromVersion => 4;
    public int ToVersion => 5;

    public void Apply(JsonObject manifest, JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(document);

        if (document["cels"] is not JsonArray cels)
            throw new InvalidOperationException("schema4 document.cels must be an array before schema5 migration.");

        foreach (var node in cels)
        {
            if (node is not JsonObject cel)
                throw new InvalidOperationException("schema4 document.cels entries must be objects before schema5 migration.");

            if (cel["effects"] is null)
            {
                cel["effects"] = new JsonArray();
                continue;
            }

            if (cel["effects"] is not JsonArray)
                throw new InvalidOperationException("Existing schema4 cel.effects extension must be an array to migrate to schema5.");
        }
    }
}
