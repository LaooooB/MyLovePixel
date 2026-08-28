using System.Text.Json.Nodes;

namespace MyLovePixel.Persistence;

public interface IProjectMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    void Apply(JsonObject manifest, JsonObject document);
}

public sealed class ProjectMigrationRegistry
{
    private readonly Dictionary<int, IProjectMigration> _byFromVersion = [];

    public static ProjectMigrationRegistry CreateDefault()
    {
        var registry = new ProjectMigrationRegistry();
        registry.Register(new Schema1To2AnimationMigration());
        registry.Register(new Schema2To3PaletteMigration());
        registry.Register(new Schema3To4TilemapMigration());
        return registry;
    }

    public void Register(IProjectMigration migration)
    {
        ArgumentNullException.ThrowIfNull(migration);
        if (migration.FromVersion < 0) throw new ArgumentOutOfRangeException(nameof(migration), "FromVersion cannot be negative.");
        if (migration.ToVersion != migration.FromVersion + 1)
            throw new ArgumentException("Migrations must advance exactly one schema version.", nameof(migration));
        if (!_byFromVersion.TryAdd(migration.FromVersion, migration))
            throw new InvalidOperationException($"A migration from schema {migration.FromVersion} is already registered.");
    }

    public void Migrate(JsonObject manifest, JsonObject document, int targetVersion)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(document);
        if (targetVersion < 0) throw new ArgumentOutOfRangeException(nameof(targetVersion));

        var currentVersion = ReadSchemaVersion(manifest);
        if (currentVersion > targetVersion)
            throw new PixelProjectException(
                PixelProjectErrorCode.UnsupportedSchemaVersion,
                $"Project schema {currentVersion} is newer than supported schema {targetVersion}.",
                PixelProjectFormat.ManifestEntry);

        while (currentVersion < targetVersion)
        {
            if (!_byFromVersion.TryGetValue(currentVersion, out var migration))
                throw new PixelProjectException(
                    PixelProjectErrorCode.MigrationMissing,
                    $"No migration is registered from schema {currentVersion} to {currentVersion + 1}.",
                    PixelProjectFormat.ManifestEntry);

            try
            {
                migration.Apply(manifest, document);
            }
            catch (PixelProjectException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PixelProjectException(
                    PixelProjectErrorCode.MigrationInvalid,
                    $"Migration {migration.FromVersion}->{migration.ToVersion} failed.",
                    PixelProjectFormat.ManifestEntry,
                    ex);
            }

            currentVersion = migration.ToVersion;
            manifest["schemaVersion"] = currentVersion;
        }
    }

    internal static int ReadSchemaVersion(JsonObject manifest)
    {
        var node = manifest["schemaVersion"];
        if (node is not JsonValue value || !value.TryGetValue<int>(out var version) || version < 0)
            throw new PixelProjectException(
                PixelProjectErrorCode.InvalidJson,
                "manifest.json must contain a non-negative integer schemaVersion.",
                PixelProjectFormat.ManifestEntry);
        return version;
    }
}
