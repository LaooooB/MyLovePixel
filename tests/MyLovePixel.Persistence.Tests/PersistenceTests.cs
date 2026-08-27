using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Persistence.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public void RoundTrip_PreservesSemanticHashAndLinkedCelIdentity()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var firstCel = document.Cels.Single();
        var secondFrame = new Frame(FrameId.New(), 250_000);
        document.AddFrame(secondFrame);
        document.AddCel(new Cel(CelId.New(), firstCel.LayerId, secondFrame.Id, firstCel.SurfaceId));

        var bus = new CommandBus(document);
        var color = new Rgba32(10, 20, 30, 255);
        bus.Execute(new PixelPatchCommand(firstCel.SurfaceId, [new PixelWrite(2, 1, color)]));
        var expectedHash = ProjectSemanticHash.Compute(document);

        using var stream = new MemoryStream();
        PixelProjectFile.Save(stream, new PixelProject(document));
        stream.Position = 0;
        var loaded = PixelProjectFile.Load(stream);

        Assert.Equal(expectedHash, ProjectSemanticHash.Compute(loaded.Document));
        Assert.Equal(2, loaded.Document.Cels.Count);
        Assert.Single(loaded.Document.Cels.Select(c => c.SurfaceId).Distinct());
        Assert.Equal(color, loaded.Document.Resources.GetSurface(loaded.Document.Cels.First().SurfaceId).GetPixel(2, 1));
        Assert.Equal(250_000, loaded.Document.GetFrame(secondFrame.Id).DurationTicks);
    }

    [Fact]
    public void UnknownJsonAndOpaquePluginPayload_RoundTripWithoutLoss()
    {
        var entries = SaveToEntries(new PixelProject(PixelDocumentFactory.CreateBlank(2, 2)));
        var manifest = ParseEntry(entries, PixelProjectFormat.ManifestEntry);
        var documentEntry = RequireString(manifest, "documentEntry");
        var document = ParseEntry(entries, documentEntry);

        manifest["futureManifest"] = new JsonObject { ["enabled"] = true };
        document["futureDocument"] = 42;
        var layers = document["layers"]!.AsArray();
        layers[0]!.AsObject()["futureLayer"] = "preserve-me";
        entries[documentEntry] = Encoding.UTF8.GetBytes(document.ToJsonString(ProjectJson.Options));
        entries["plugins/example/payload.bin"] = [0, 1, 2, 3, 254, 255];
        Rehash(entries, manifest);

        using var injected = WriteEntries(entries);
        var loaded = PixelProjectFile.Load(injected);
        using var savedAgain = new MemoryStream();
        PixelProjectFile.Save(savedAgain, loaded);
        savedAgain.Position = 0;
        var result = ReadEntries(savedAgain);

        Assert.Equal(entries["plugins/example/payload.bin"], result["plugins/example/payload.bin"]);
        var resultManifest = ParseEntry(result, PixelProjectFormat.ManifestEntry);
        var resultDocumentEntry = RequireString(resultManifest, "documentEntry");
        var resultDocument = ParseEntry(result, resultDocumentEntry);
        Assert.True(resultManifest["futureManifest"]!["enabled"]!.GetValue<bool>());
        Assert.Equal(42, resultDocument["futureDocument"]!.GetValue<int>());
        Assert.Equal("preserve-me", resultDocument["layers"]![0]!["futureLayer"]!.GetValue<string>());
    }

    [Fact]
    public void GlobalContentHashMismatch_IsRejectedBeforeJsonMutationIsTrusted()
    {
        var entries = SaveToEntries(new PixelProject(PixelDocumentFactory.CreateBlank(2, 2)));
        var manifest = ParseEntry(entries, PixelProjectFormat.ManifestEntry);
        var documentEntry = RequireString(manifest, "documentEntry");
        entries[documentEntry][^1] ^= 0x01;

        using var stream = WriteEntries(entries);
        var error = Assert.Throws<PixelProjectException>(() => PixelProjectFile.Load(stream));
        Assert.Equal(PixelProjectErrorCode.ContentHashMismatch, error.Code);
    }

    [Fact]
    public void SurfaceInternalHashMismatch_IsRejectedEvenWhenGlobalHashWasRecomputed()
    {
        var entries = SaveToEntries(new PixelProject(PixelDocumentFactory.CreateBlank(2, 2)));
        var manifest = ParseEntry(entries, PixelProjectFormat.ManifestEntry);
        var documentEntry = RequireString(manifest, "documentEntry");
        var document = ParseEntry(entries, documentEntry);
        var surfaceEntry = document["surfaces"]![0]!["entry"]!.GetValue<string>();
        entries[surfaceEntry][^1] ^= 0x01;
        Rehash(entries, manifest);

        using var stream = WriteEntries(entries);
        var error = Assert.Throws<PixelProjectException>(() => PixelProjectFile.Load(stream));
        Assert.Equal(PixelProjectErrorCode.InvalidSurface, error.Code);
        Assert.Equal(surfaceEntry, error.EntryName);
    }

    [Fact]
    public void MissingCelSurfaceReference_ReturnsStructuredReferenceError()
    {
        var entries = SaveToEntries(new PixelProject(PixelDocumentFactory.CreateBlank(2, 2)));
        var manifest = ParseEntry(entries, PixelProjectFormat.ManifestEntry);
        var documentEntry = RequireString(manifest, "documentEntry");
        var document = ParseEntry(entries, documentEntry);
        document["cels"]![0]!["surfaceId"] = Guid.NewGuid().ToString("N");
        entries[documentEntry] = Encoding.UTF8.GetBytes(document.ToJsonString(ProjectJson.Options));
        Rehash(entries, manifest);

        using var stream = WriteEntries(entries);
        var error = Assert.Throws<PixelProjectException>(() => PixelProjectFile.Load(stream));
        Assert.Equal(PixelProjectErrorCode.InvalidReference, error.Code);
    }

    [Fact]
    public void MigrationRegistry_RunsEveryIntermediateVersionExactlyOnce()
    {
        var manifest = JsonNode.Parse("{\"schemaVersion\":1}")!.AsObject();
        var document = JsonNode.Parse("{}")!.AsObject();
        var registry = new ProjectMigrationRegistry();
        registry.Register(new TestMigration(1, 2, (_, doc) => doc["oneToTwo"] = true));
        registry.Register(new TestMigration(2, 3, (_, doc) => doc["twoToThree"] = true));

        registry.Migrate(manifest, document, 3);

        Assert.Equal(3, ProjectMigrationRegistry.ReadSchemaVersion(manifest));
        Assert.True(document["oneToTwo"]!.GetValue<bool>());
        Assert.True(document["twoToThree"]!.GetValue<bool>());
    }

    [Fact]
    public void AtomicWriter_FailureBeforeCommit_LeavesOldFileUntouched()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MyLovePixel.Persistence.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "project.pixelproj");
        File.WriteAllText(path, "old-content", Encoding.UTF8);

        try
        {
            Assert.Throws<InjectedFailureException>(() =>
                AtomicFileWriter.Write(
                    path,
                    stream => stream.Write("new-content"u8),
                    _ => throw new InjectedFailureException()));

            Assert.Equal("old-content", File.ReadAllText(path, Encoding.UTF8));
            Assert.DoesNotContain(Directory.EnumerateFiles(directory), file => file.EndsWith(".tmp", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Dictionary<string, byte[]> SaveToEntries(PixelProject project)
    {
        using var stream = new MemoryStream();
        PixelProjectFile.Save(stream, project);
        stream.Position = 0;
        return ReadEntries(stream);
    }

    private static Dictionary<string, byte[]> ReadEntries(Stream source)
    {
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true, Encoding.UTF8);
        return archive.Entries.ToDictionary(
            entry => entry.FullName,
            entry =>
            {
                using var input = entry.Open();
                using var buffer = new MemoryStream();
                input.CopyTo(buffer);
                return buffer.ToArray();
            },
            StringComparer.Ordinal);
    }

    private static MemoryStream WriteEntries(IReadOnlyDictionary<string, byte[]> entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            foreach (var pair in entries.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var entry = archive.CreateEntry(pair.Key, CompressionLevel.Optimal);
                using var output = entry.Open();
                output.Write(pair.Value);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static JsonObject ParseEntry(IReadOnlyDictionary<string, byte[]> entries, string name) =>
        ProjectJson.ParseObject(entries[name], name);

    private static string RequireString(JsonObject node, string propertyName) =>
        node[propertyName]!.GetValue<string>();

    private static void Rehash(Dictionary<string, byte[]> entries, JsonObject manifest)
    {
        manifest["contentHash"] = ProjectContentHash.Compute(
            entries.Where(pair => !string.Equals(pair.Key, PixelProjectFormat.ManifestEntry, StringComparison.Ordinal)));
        entries[PixelProjectFormat.ManifestEntry] = Encoding.UTF8.GetBytes(manifest.ToJsonString(ProjectJson.Options));
    }

    private sealed class TestMigration(int fromVersion, int toVersion, Action<JsonObject, JsonObject> apply) : IProjectMigration
    {
        public int FromVersion { get; } = fromVersion;
        public int ToVersion { get; } = toVersion;
        public void Apply(JsonObject manifest, JsonObject document) => apply(manifest, document);
    }

    private sealed class InjectedFailureException : Exception;
}
