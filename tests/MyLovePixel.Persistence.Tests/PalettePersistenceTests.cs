using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Persistence.Tests;

public sealed class PalettePersistenceTests
{
    [Fact]
    public void IndexedProject_RoundTripPreservesSemanticHashAndCompactPayload()
    {
        var project = CreateIndexedProject(out var paletteId, out var surfaceId);
        var expectedHash = ProjectSemanticHash.Compute(project.Document);

        using var stream = new MemoryStream();
        PixelProjectFile.Save(stream, project);
        stream.Position = 0;
        var entries = ReadEntries(stream);
        var manifest = ParseEntry(entries, PixelProjectFormat.ManifestEntry);
        var documentEntry = manifest["documentEntry"]!.GetValue<string>();
        var document = ParseEntry(entries, documentEntry);
        var surface = document["surfaces"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(node => node["id"]!.GetValue<string>() == surfaceId.ToString());
        var surfaceEntry = surface["entry"]!.GetValue<string>();

        Assert.Equal(4, manifest["schemaVersion"]!.GetValue<int>());
        Assert.Equal("indexed8", surface["format"]!.GetValue<string>());
        Assert.Equal(paletteId.ToString(), surface["paletteId"]!.GetValue<string>());
        Assert.Equal(56, entries[surfaceEntry].Length); // 52-byte MLPX header + 4 indexed pixels.
        Assert.Equal((byte)PixelFormat.Indexed8, entries[surfaceEntry][6]);

        using var reloadStream = WriteEntries(entries);
        var loaded = PixelProjectFile.Load(reloadStream);
        Assert.Equal(expectedHash, ProjectSemanticHash.Compute(loaded.Document));
        var loadedPalette = loaded.Document.Resources.GetPalette(paletteId);
        var loadedSurface = loaded.Document.Resources.GetSurface(surfaceId);
        Assert.Equal((byte)0, loadedPalette.TransparentIndex);
        Assert.Equal(new Rgba32(255, 0, 0, 255), loadedPalette.GetColor(1));
        Assert.Equal(PixelFormat.Indexed8, loadedSurface.Format);
        Assert.Equal(paletteId, loadedSurface.PaletteId);
        Assert.Equal(new byte[] { 0, 1, 2, 1 }, loadedSurface.Snapshot().Bytes.ToArray());
    }

    [Fact]
    public void Schema2Project_MigratesThroughSchema4WithEmptyPaletteAndTileCollections()
    {
        var entries = SaveToEntries(new PixelProject(PixelDocumentFactory.CreateBlank(2, 2)));
        var manifest = ParseEntry(entries, PixelProjectFormat.ManifestEntry);
        var documentEntry = manifest["documentEntry"]!.GetValue<string>();
        var document = ParseEntry(entries, documentEntry);

        manifest["schemaVersion"] = 2;
        document.Remove("palettes");
        document.Remove("seed");
        document.Remove("tilesets");
        document.Remove("tilemaps");
        entries[documentEntry] = Encoding.UTF8.GetBytes(document.ToJsonString(ProjectJson.Options));
        Rehash(entries, manifest);

        using var oldProject = WriteEntries(entries);
        var loaded = PixelProjectFile.Load(oldProject);
        Assert.Empty(loaded.Document.Resources.PaletteIds);
        Assert.Empty(loaded.Document.Resources.TilesetIds);
        Assert.Empty(loaded.Document.Resources.TilemapIds);

        using var savedAgain = new MemoryStream();
        PixelProjectFile.Save(savedAgain, loaded);
        savedAgain.Position = 0;
        var migratedEntries = ReadEntries(savedAgain);
        var migratedManifest = ParseEntry(migratedEntries, PixelProjectFormat.ManifestEntry);
        var migratedDocument = ParseEntry(
            migratedEntries,
            migratedManifest["documentEntry"]!.GetValue<string>());

        Assert.Equal(4, migratedManifest["schemaVersion"]!.GetValue<int>());
        Assert.Empty(migratedDocument["palettes"]!.AsArray());
        Assert.Empty(migratedDocument["tilesets"]!.AsArray());
        Assert.Empty(migratedDocument["tilemaps"]!.AsArray());
    }

    [Fact]
    public void PaletteColorAndIndexedSurfaceUnknownJson_RoundTripWithoutLoss()
    {
        var entries = SaveToEntries(CreateIndexedProject(out _, out _));
        var manifest = ParseEntry(entries, PixelProjectFormat.ManifestEntry);
        var documentEntry = manifest["documentEntry"]!.GetValue<string>();
        var document = ParseEntry(entries, documentEntry);
        var palette = document["palettes"]![0]!.AsObject();
        var color = palette["colors"]![0]!.AsObject();
        var surface = document["surfaces"]![0]!.AsObject();

        palette["futurePalette"] = new JsonObject { ["name"] = "keep-palette" };
        color["futureColor"] = 123;
        surface["futureIndexedSurface"] = true;
        entries[documentEntry] = Encoding.UTF8.GetBytes(document.ToJsonString(ProjectJson.Options));
        Rehash(entries, manifest);

        using var injected = WriteEntries(entries);
        var loaded = PixelProjectFile.Load(injected);
        using var savedAgain = new MemoryStream();
        PixelProjectFile.Save(savedAgain, loaded);
        savedAgain.Position = 0;
        var result = ReadEntries(savedAgain);
        var resultManifest = ParseEntry(result, PixelProjectFormat.ManifestEntry);
        var resultDocument = ParseEntry(result, resultManifest["documentEntry"]!.GetValue<string>());
        var resultPalette = resultDocument["palettes"]![0]!.AsObject();
        var resultColor = resultPalette["colors"]![0]!.AsObject();
        var resultSurface = resultDocument["surfaces"]![0]!.AsObject();

        Assert.Equal("keep-palette", resultPalette["futurePalette"]!["name"]!.GetValue<string>());
        Assert.Equal(123, resultColor["futureColor"]!.GetValue<int>());
        Assert.True(resultSurface["futureIndexedSurface"]!.GetValue<bool>());
    }

    private static PixelProject CreateIndexedProject(out PaletteId paletteId, out ResourceId surfaceId)
    {
        var document = PixelDocumentFactory.CreateBlank(4, 1);
        var cel = document.Cels.Single();
        var oldSurfaceId = cel.SurfaceId;

        paletteId = PaletteId.New();
        document.Resources.AddPalette(
            paletteId,
            new Palette(
                [
                    new Rgba32(20, 30, 40, 255),
                    new Rgba32(255, 0, 0, 255),
                    new Rgba32(0, 255, 0, 255),
                ],
                transparentIndex: 0));

        var indexed = PixelSurface.CreateIndexed(new IntSize(4, 1), paletteId);
        indexed.ReplaceIndices([0, 1, 2, 1]);
        surfaceId = document.Resources.AddSurface(indexed);
        cel.SurfaceId = surfaceId;
        document.Resources.RemoveSurface(oldSurfaceId);
        return new PixelProject(document);
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
            foreach (var pair in entries.OrderBy(item => item.Key, StringComparer.Ordinal))
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

    private static void Rehash(Dictionary<string, byte[]> entries, JsonObject manifest)
    {
        manifest["contentHash"] = ProjectContentHash.Compute(
            entries.Where(pair => !string.Equals(pair.Key, PixelProjectFormat.ManifestEntry, StringComparison.Ordinal)));
        entries[PixelProjectFormat.ManifestEntry] = Encoding.UTF8.GetBytes(manifest.ToJsonString(ProjectJson.Options));
    }
}
