using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using MyLovePixel.Commands;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Persistence.Tests;

public sealed class ColorCyclePersistenceTests
{
    [Fact]
    public void ColorCycleTrack_RoundTripPreservesSemanticHashAndTrackValue()
    {
        var project = CreateProjectWithColorCycle(out var frameId, out var paletteId);
        var expectedHash = ProjectSemanticHash.Compute(project.Document);

        using var stream = new MemoryStream();
        PixelProjectFile.Save(stream, project);
        stream.Position = 0;
        var loaded = PixelProjectFile.Load(stream);

        Assert.Equal(expectedHash, ProjectSemanticHash.Compute(loaded.Document));
        Assert.True(loaded.Document.Animation.ColorCycleTrack.TryGetValue(frameId, out var value));
        var cycle = Assert.Single(value.Cycles);
        Assert.Equal(paletteId, cycle.PaletteId);
        Assert.Equal((byte)1, cycle.StartIndex);
        Assert.Equal((byte)3, cycle.EndIndex);
        Assert.Equal(-1, cycle.Offset);
    }

    [Fact]
    public void Schema2To3Migration_CreatesStableColorCycleTrackId()
    {
        var documentId = Guid.NewGuid().ToString("N");
        var firstManifest = JsonNode.Parse("{\"schemaVersion\":2}")!.AsObject();
        var secondManifest = JsonNode.Parse("{\"schemaVersion\":2}")!.AsObject();
        var firstDocument = JsonNode.Parse($"{{\"id\":\"{documentId}\",\"animation\":{{}}}}")!.AsObject();
        var secondDocument = JsonNode.Parse($"{{\"id\":\"{documentId}\",\"animation\":{{}}}}")!.AsObject();
        var migration = new Schema2To3PaletteMigration();

        migration.Apply(firstManifest, firstDocument);
        migration.Apply(secondManifest, secondDocument);

        var firstTrack = firstDocument["animation"]!["colorCycleTrack"]!.AsObject();
        var secondTrack = secondDocument["animation"]!["colorCycleTrack"]!.AsObject();
        Assert.Equal(firstTrack["id"]!.GetValue<string>(), secondTrack["id"]!.GetValue<string>());
        Assert.Equal("Color Cycles", firstTrack["name"]!.GetValue<string>());
        Assert.Empty(firstTrack["keyframes"]!.AsArray());
        Assert.Empty(firstDocument["palettes"]!.AsArray());
    }

    [Fact]
    public void ColorCycleUnknownJson_RoundTripsWithoutLoss()
    {
        var entries = SaveToEntries(CreateProjectWithColorCycle(out _, out _));
        var manifest = Parse(entries, PixelProjectFormat.ManifestEntry);
        var documentEntry = manifest["documentEntry"]!.GetValue<string>();
        var document = Parse(entries, documentEntry);
        var track = document["animation"]!["colorCycleTrack"]!.AsObject();
        var keyframe = track["keyframes"]![0]!.AsObject();
        var value = keyframe["value"]!.AsObject();
        var cycle = value["cycles"]![0]!.AsObject();
        track["futureTrack"] = "keep-track";
        keyframe["futureKeyframe"] = 12;
        value["futureValue"] = true;
        cycle["futureCycle"] = new JsonObject { ["mode"] = "keep-cycle" };
        entries[documentEntry] = Encoding.UTF8.GetBytes(document.ToJsonString(ProjectJson.Options));
        Rehash(entries, manifest);

        using var injected = WriteEntries(entries);
        var loaded = PixelProjectFile.Load(injected);
        using var savedAgain = new MemoryStream();
        PixelProjectFile.Save(savedAgain, loaded);
        savedAgain.Position = 0;
        var result = ReadEntries(savedAgain);
        var resultManifest = Parse(result, PixelProjectFormat.ManifestEntry);
        var resultDocument = Parse(result, resultManifest["documentEntry"]!.GetValue<string>());
        var resultTrack = resultDocument["animation"]!["colorCycleTrack"]!.AsObject();
        var resultKeyframe = resultTrack["keyframes"]![0]!.AsObject();
        var resultValue = resultKeyframe["value"]!.AsObject();
        var resultCycle = resultValue["cycles"]![0]!.AsObject();

        Assert.Equal("keep-track", resultTrack["futureTrack"]!.GetValue<string>());
        Assert.Equal(12, resultKeyframe["futureKeyframe"]!.GetValue<int>());
        Assert.True(resultValue["futureValue"]!.GetValue<bool>());
        Assert.Equal("keep-cycle", resultCycle["futureCycle"]!["mode"]!.GetValue<string>());
    }

    private static PixelProject CreateProjectWithColorCycle(out FrameId frameId, out PaletteId paletteId)
    {
        var document = PixelDocumentFactory.CreateBlank(3, 1);
        frameId = document.FrameOrder[0];
        var cel = document.Cels.Single();
        var oldSurfaceId = cel.SurfaceId;
        paletteId = PaletteId.New();
        document.Resources.AddPalette(
            paletteId,
            new Palette([
                Rgba32.Transparent,
                new Rgba32(255, 0, 0, 255),
                new Rgba32(0, 255, 0, 255),
                new Rgba32(0, 0, 255, 255),
            ], transparentIndex: 0));
        var surface = PixelSurface.CreateIndexed(new IntSize(3, 1), paletteId);
        surface.ReplaceIndices([1, 2, 3]);
        var surfaceId = document.Resources.AddSurface(surface);
        cel.SurfaceId = surfaceId;
        document.Resources.RemoveSurface(oldSurfaceId);

        var bus = new CommandBus(document);
        bus.Execute(new SetColorCyclesKeyframeCommand(
            frameId,
            new ColorCycleFrameValue([
                new PaletteCycle(paletteId, 1, 3, -1),
            ])));
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

    private static JsonObject Parse(IReadOnlyDictionary<string, byte[]> entries, string name) =>
        ProjectJson.ParseObject(entries[name], name);

    private static void Rehash(Dictionary<string, byte[]> entries, JsonObject manifest)
    {
        manifest["contentHash"] = ProjectContentHash.Compute(
            entries.Where(pair => !string.Equals(pair.Key, PixelProjectFormat.ManifestEntry, StringComparison.Ordinal)));
        entries[PixelProjectFormat.ManifestEntry] = Encoding.UTF8.GetBytes(manifest.ToJsonString(ProjectJson.Options));
    }
}
