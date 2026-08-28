using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Persistence.Tests;

public sealed class EffectPersistenceTests
{
    [Fact]
    public void Schema5EffectGraph_RoundTripPreservesUnknownTypeParametersTracksAndSemanticHash()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 2);
        var cel = document.Cels.Single();
        var paletteId = PaletteId.New();
        document.Resources.AddPalette(paletteId, new Palette([new Rgba32(1, 2, 3, 255)]));

        var effectId = EffectInstanceId.New();
        var trackId = AnimationTrackId.New();
        var effect = new EffectInstance(effectId, "vendor.future-effect", enabled: true);
        effect.SetParameter("count", EffectValue.Integer(7), out _);
        effect.SetParameter("amount", EffectValue.Number(0.75), out _);
        effect.SetParameter("flag", EffectValue.Boolean(true), out _);
        effect.SetParameter("color", EffectValue.Color(new Rgba32(10, 20, 30, 40)), out _);
        effect.SetParameter("offset", EffectValue.Point(new IntPoint(-2, 5)), out _);
        effect.SetParameter("palette", EffectValue.PaletteReference(paletteId), out _);
        effect.SetParameter("label", EffectValue.Text("opaque-data"), out _);
        effect.SetKeyframe(
            "amount",
            cel.FrameId,
            EffectValue.Number(0.25),
            trackId,
            out _,
            out _);
        cel.Effects.Add(effect);

        var expectedHash = ProjectSemanticHash.Compute(document);
        using var stream = new MemoryStream();
        PixelProjectFile.Save(stream, new PixelProject(document));
        stream.Position = 0;
        var loaded = PixelProjectFile.Load(stream).Document;

        Assert.Equal(expectedHash, ProjectSemanticHash.Compute(loaded));
        var loadedEffect = loaded.Cels.Single().Effects.GetEffect(effectId);
        Assert.Equal("vendor.future-effect", loadedEffect.TypeId);
        Assert.True(loadedEffect.Enabled);
        Assert.Equal(EffectValue.Integer(7), loadedEffect.Parameters["count"]);
        Assert.Equal(EffectValue.Number(0.75), loadedEffect.Parameters["amount"]);
        Assert.Equal(EffectValue.Boolean(true), loadedEffect.Parameters["flag"]);
        Assert.Equal(EffectValue.Color(new Rgba32(10, 20, 30, 40)), loadedEffect.Parameters["color"]);
        Assert.Equal(EffectValue.Point(new IntPoint(-2, 5)), loadedEffect.Parameters["offset"]);
        Assert.Equal(EffectValue.PaletteReference(paletteId), loadedEffect.Parameters["palette"]);
        Assert.Equal(EffectValue.Text("opaque-data"), loadedEffect.Parameters["label"]);
        var loadedTrack = loadedEffect.ParameterTracks["amount"];
        Assert.Equal(trackId, loadedTrack.Id);
        Assert.Equal(EffectValue.Number(0.25), loadedTrack.Values[cel.FrameId]);
    }

    [Fact]
    public void EffectUnknownJson_RoundTripsAtEveryExtensibleLevel()
    {
        var document = PixelDocumentFactory.CreateBlank(1, 1);
        var cel = document.Cels.Single();
        var effect = new EffectInstance(EffectInstanceId.New(), "vendor.future-effect");
        effect.SetParameter("color", EffectValue.Color(new Rgba32(10, 20, 30, 255)), out _);
        effect.SetKeyframe(
            "color",
            cel.FrameId,
            EffectValue.Color(new Rgba32(40, 50, 60, 255)),
            AnimationTrackId.New(),
            out _,
            out _);
        cel.Effects.Add(effect);

        var entries = SaveToEntries(new PixelProject(document));
        var manifest = Parse(entries, PixelProjectFormat.ManifestEntry);
        var documentEntry = manifest["documentEntry"]!.GetValue<string>();
        var json = Parse(entries, documentEntry);
        var effectJson = json["cels"]![0]!["effects"]![0]!.AsObject();
        var parameter = effectJson["parameters"]![0]!.AsObject();
        var parameterValue = parameter["value"]!.AsObject();
        var parameterColor = parameterValue["colorValue"]!.AsObject();
        var track = effectJson["tracks"]![0]!.AsObject();
        var keyframe = track["keyframes"]![0]!.AsObject();
        var keyframeValue = keyframe["value"]!.AsObject();
        var keyframeColor = keyframeValue["colorValue"]!.AsObject();

        effectJson["futureEffect"] = new JsonObject { ["mode"] = "keep-effect" };
        parameter["futureParameter"] = 17;
        parameterValue["futureValue"] = "keep-value";
        parameterColor["futureColor"] = true;
        track["futureTrack"] = new JsonArray(1, 2, 3);
        keyframe["futureKeyframe"] = "keep-keyframe";
        keyframeValue["futureAnimatedValue"] = 91;
        keyframeColor["futureAnimatedColor"] = "keep-color";
        entries[documentEntry] = Encoding.UTF8.GetBytes(json.ToJsonString(ProjectJson.Options));
        Rehash(entries, manifest);

        using var injected = WriteEntries(entries);
        var loaded = PixelProjectFile.Load(injected);
        using var savedAgain = new MemoryStream();
        PixelProjectFile.Save(savedAgain, loaded);
        savedAgain.Position = 0;
        var result = ReadEntries(savedAgain);
        var resultManifest = Parse(result, PixelProjectFormat.ManifestEntry);
        var resultJson = Parse(result, resultManifest["documentEntry"]!.GetValue<string>());
        var resultEffect = resultJson["cels"]![0]!["effects"]![0]!.AsObject();
        var resultParameter = resultEffect["parameters"]![0]!.AsObject();
        var resultParameterValue = resultParameter["value"]!.AsObject();
        var resultTrack = resultEffect["tracks"]![0]!.AsObject();
        var resultKeyframe = resultTrack["keyframes"]![0]!.AsObject();
        var resultKeyframeValue = resultKeyframe["value"]!.AsObject();

        Assert.Equal("keep-effect", resultEffect["futureEffect"]!["mode"]!.GetValue<string>());
        Assert.Equal(17, resultParameter["futureParameter"]!.GetValue<int>());
        Assert.Equal("keep-value", resultParameterValue["futureValue"]!.GetValue<string>());
        Assert.True(resultParameterValue["colorValue"]!["futureColor"]!.GetValue<bool>());
        Assert.Equal(3, resultTrack["futureTrack"]!.AsArray().Count);
        Assert.Equal("keep-keyframe", resultKeyframe["futureKeyframe"]!.GetValue<string>());
        Assert.Equal(91, resultKeyframeValue["futureAnimatedValue"]!.GetValue<int>());
        Assert.Equal("keep-color", resultKeyframeValue["colorValue"]!["futureAnimatedColor"]!.GetValue<string>());
    }

    [Fact]
    public void Schema4Project_MigratesToSchema5WithEmptyEffectGraphs()
    {
        var entries = SaveToEntries(new PixelProject(PixelDocumentFactory.CreateBlank(2, 2)));
        var manifest = Parse(entries, PixelProjectFormat.ManifestEntry);
        var documentEntry = manifest["documentEntry"]!.GetValue<string>();
        var document = Parse(entries, documentEntry);
        manifest["schemaVersion"] = 4;
        foreach (var cel in document["cels"]!.AsArray()) cel!.AsObject().Remove("effects");
        entries[documentEntry] = Encoding.UTF8.GetBytes(document.ToJsonString(ProjectJson.Options));
        Rehash(entries, manifest);

        using var oldProject = WriteEntries(entries);
        var loaded = PixelProjectFile.Load(oldProject);
        Assert.All(loaded.Document.Cels, cel => Assert.Empty(cel.Effects.EffectOrder));

        using var savedAgain = new MemoryStream();
        PixelProjectFile.Save(savedAgain, loaded);
        savedAgain.Position = 0;
        var migratedEntries = ReadEntries(savedAgain);
        var migratedManifest = Parse(migratedEntries, PixelProjectFormat.ManifestEntry);
        var migratedDocument = Parse(migratedEntries, migratedManifest["documentEntry"]!.GetValue<string>());
        Assert.Equal(5, migratedManifest["schemaVersion"]!.GetValue<int>());
        Assert.All(migratedDocument["cels"]!.AsArray(), item => Assert.Empty(item!["effects"]!.AsArray()));
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
