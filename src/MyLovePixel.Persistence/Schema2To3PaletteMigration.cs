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
    }
}
