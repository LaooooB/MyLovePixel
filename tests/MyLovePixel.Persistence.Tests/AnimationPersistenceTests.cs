using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using MyLovePixel.Commands;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Persistence.Tests;

public sealed class AnimationPersistenceTests
{
    [Fact]
    public void Schema2RoundTrip_PreservesAnimationMetadataAndSemanticHash()
    {
        var document = PixelDocumentFactory.CreateBlank(16, 16);
        var bus = new CommandBus(document);
        var frame0 = document.FrameOrder[0];
        var copy = new CopyFrameCommand(frame0, FrameCopyMode.Linked);
        bus.Execute(copy);
        var frame1 = copy.NewFrameId;
        var clipId = AnimationClipId.New();
        var tagId = AnimationTagId.New();
        var sliceId = SliceId.New();
        var pivot = new IntPoint(4, 7);
        var hitboxes = new BoxFrameValue([new NamedBox("attack", new IntRect(8, 2, 4, 3))]);
        var hurtboxes = new BoxFrameValue([new NamedBox("body", new IntRect(3, 1, 6, 10))]);
        var sockets = new SocketFrameValue([new SocketPose("weapon", new IntPoint(12, 5))]);
        var events = new EventFrameValue([new AnimationEventMarker("impact", "heavy")]);

        bus.Execute(new UpsertAnimationClipCommand(
            new AnimationClip(clipId, "Attack", frame0, frame1, AnimationLoopMode.Once)));
        bus.Execute(new UpsertAnimationTagCommand(
            new AnimationTag(tagId, "Active", frame0, frame1)));
        bus.Execute(new UpsertSpriteSliceCommand(
            new SpriteSlice(
                sliceId,
                "Body",
                new IntRect(1, 2, 12, 10),
                new IntPoint(6, 9),
                new NineSliceInsets(2, 1, 2, 1))));
        bus.Execute(new SetPivotKeyframeCommand(frame0, pivot));
        bus.Execute(new SetHitboxesKeyframeCommand(frame0, hitboxes));
        bus.Execute(new SetHurtboxesKeyframeCommand(frame0, hurtboxes));
        bus.Execute(new SetSocketsKeyframeCommand(frame0, sockets));
        bus.Execute(new SetAnimationEventsKeyframeCommand(frame0, events));

        var before = DocumentSnapshot.Capture(document);
        var expectedHash = ProjectSemanticHash.Compute(document);
        using var stream = new MemoryStream();
        PixelProjectFile.Save(stream, new PixelProject(document));
        stream.Position = 0;

        var loaded = PixelProjectFile.Load(stream).Document;
        var after = DocumentSnapshot.Capture(loaded);

        Assert.Equal(expectedHash, ProjectSemanticHash.Compute(loaded));
        Assert.Equal(before.Animation.PivotTrack.Id, after.Animation.PivotTrack.Id);
        Assert.Equal(before.Animation.HitboxTrack.Id, after.Animation.HitboxTrack.Id);
        Assert.Equal(before.Animation.HurtboxTrack.Id, after.Animation.HurtboxTrack.Id);
        Assert.Equal(before.Animation.SocketTrack.Id, after.Animation.SocketTrack.Id);
        Assert.Equal(before.Animation.EventTrack.Id, after.Animation.EventTrack.Id);

        var loadedClip = loaded.Animation.GetClip(clipId);
        Assert.Equal("Attack", loadedClip.Name);
        Assert.Equal(AnimationLoopMode.Once, loadedClip.LoopMode);
        Assert.Equal(frame0, loadedClip.StartFrameId);
        Assert.Equal(frame1, loadedClip.EndFrameId);
        Assert.Equal("Active", loaded.Animation.GetTag(tagId).Name);

        var loadedSlice = loaded.Animation.GetSlice(sliceId);
        Assert.Equal(new IntRect(1, 2, 12, 10), loadedSlice.Bounds);
        Assert.Equal(new IntPoint(6, 9), loadedSlice.Pivot);
        Assert.Equal(new NineSliceInsets(2, 1, 2, 1), loadedSlice.NineSlice);

        Assert.True(loaded.Animation.PivotTrack.TryGetValue(frame0, out var loadedPivot));
        Assert.Equal(pivot, loadedPivot);
        Assert.True(loaded.Animation.HitboxTrack.TryGetValue(frame0, out var loadedHitboxes));
        Assert.Equal(hitboxes, loadedHitboxes);
        Assert.True(loaded.Animation.HurtboxTrack.TryGetValue(frame0, out var loadedHurtboxes));
        Assert.Equal(hurtboxes, loadedHurtboxes);
        Assert.True(loaded.Animation.SocketTrack.TryGetValue(frame0, out var loadedSockets));
        Assert.Equal(sockets, loadedSockets);
        Assert.True(loaded.Animation.EventTrack.TryGetValue(frame0, out var loadedEvents));
        Assert.Equal(events, loadedEvents);
    }

    [Fact]
    public void Schema1Migration_CreatesStableAnimationTrackIdsDeterministically()
    {
        var entries = SaveToEntries(new PixelProject(PixelDocumentFactory.CreateBlank(2, 2)));
        var manifest = ParseEntry(entries, PixelProjectFormat.ManifestEntry);
        var documentEntry = RequireString(manifest, "documentEntry");
        var document = ParseEntry(entries, documentEntry);
        document.Remove("animation");
        manifest["schemaVersion"] = 1;
        entries[documentEntry] = Encoding.UTF8.GetBytes(document.ToJsonString(ProjectJson.Options));
        Rehash(entries, manifest);

        using var firstInput = WriteEntries(entries);
        using var secondInput = WriteEntries(entries);
        var first = DocumentSnapshot.Capture(PixelProjectFile.Load(firstInput).Document);
        var second = DocumentSnapshot.Capture(PixelProjectFile.Load(secondInput).Document);

        Assert.Empty(first.Animation.Clips);
        Assert.Empty(first.Animation.Tags);
        Assert.Empty(first.Animation.Slices);
        Assert.Equal(first.Animation.PivotTrack.Id, second.Animation.PivotTrack.Id);
        Assert.Equal(first.Animation.HitboxTrack.Id, second.Animation.HitboxTrack.Id);
        Assert.Equal(first.Animation.HurtboxTrack.Id, second.Animation.HurtboxTrack.Id);
        Assert.Equal(first.Animation.SocketTrack.Id, second.Animation.SocketTrack.Id);
        Assert.Equal(first.Animation.EventTrack.Id, second.Animation.EventTrack.Id);
        Assert.NotEqual(Guid.Empty, first.Animation.PivotTrack.Id.Value);
    }

    [Fact]
    public void UnknownAnimationJson_RoundTripsWithoutLoss()
    {
        var document = PixelDocumentFactory.CreateBlank(4, 4);
        var bus = new CommandBus(document);
        var frame = document.FrameOrder[0];
        bus.Execute(new UpsertAnimationClipCommand(
            new AnimationClip(AnimationClipId.New(), "Idle", frame, frame)));
        bus.Execute(new SetPivotKeyframeCommand(frame, new IntPoint(1, 2)));

        var entries = SaveToEntries(new PixelProject(document));
        var manifest = ParseEntry(entries, PixelProjectFormat.ManifestEntry);
        var documentEntry = RequireString(manifest, "documentEntry");
        var documentJson = ParseEntry(entries, documentEntry);
        var animation = documentJson["animation"]!.AsObject();
        animation["futureAnimation"] = new JsonObject { ["mode"] = "preserve" };
        animation["clips"]![0]!["futureClip"] = 123;
        animation["pivotTrack"]!["futureTrack"] = true;
        animation["pivotTrack"]!["keyframes"]![0]!["futureKeyframe"] = "keep";
        entries[documentEntry] = Encoding.UTF8.GetBytes(documentJson.ToJsonString(ProjectJson.Options));
        Rehash(entries, manifest);

        using var injected = WriteEntries(entries);
        var loaded = PixelProjectFile.Load(injected);
        using var savedAgain = new MemoryStream();
        PixelProjectFile.Save(savedAgain, loaded);
        savedAgain.Position = 0;
        var resultEntries = ReadEntries(savedAgain);
        var resultManifest = ParseEntry(resultEntries, PixelProjectFormat.ManifestEntry);
        var resultDocument = ParseEntry(resultEntries, RequireString(resultManifest, "documentEntry"));
        var resultAnimation = resultDocument["animation"]!.AsObject();

        Assert.Equal("preserve", resultAnimation["futureAnimation"]!["mode"]!.GetValue<string>());
        Assert.Equal(123, resultAnimation["clips"]![0]!["futureClip"]!.GetValue<int>());
        Assert.True(resultAnimation["pivotTrack"]!["futureTrack"]!.GetValue<bool>());
        Assert.Equal("keep", resultAnimation["pivotTrack"]!["keyframes"]![0]!["futureKeyframe"]!.GetValue<string>());
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

    private static string RequireString(JsonObject node, string propertyName) =>
        node[propertyName]!.GetValue<string>();

    private static void Rehash(Dictionary<string, byte[]> entries, JsonObject manifest)
    {
        manifest["contentHash"] = ProjectContentHash.Compute(
            entries.Where(pair => !string.Equals(pair.Key, PixelProjectFormat.ManifestEntry, StringComparison.Ordinal)));
        entries[PixelProjectFormat.ManifestEntry] = Encoding.UTF8.GetBytes(manifest.ToJsonString(ProjectJson.Options));
    }
}
