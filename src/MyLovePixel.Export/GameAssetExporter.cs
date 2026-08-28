using System.Text.Json;
using System.Text.Json.Serialization;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Render;

namespace MyLovePixel.Export;

public sealed class GameAssetExporter : IExporter
{
    private readonly FrameRenderer _renderer;
    private readonly AtlasPackerRegistry _packers;

    public GameAssetExporter(FrameRenderer? renderer = null, AtlasPackerRegistry? packers = null)
    {
        _renderer = renderer ?? new FrameRenderer();
        _packers = packers ?? AtlasPackerRegistry.CreateDefault();
    }

    public string Id => BuiltinExporterIds.GameAssets;

    public ExportBundle Export(ExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var snapshot = request.Snapshot;
        var preset = request.Preset;
        preset.Validate();
        var frameIds = ResolveFrames(snapshot, preset.Selection);
        var processed = frameIds.Select(frameId => ProcessFrame(snapshot, frameId, preset)).ToArray();
        if (processed.Length == 0) throw new InvalidOperationException("Export selection contains no frames.");

        return preset.Layout switch
        {
            ExportLayout.SeparateFrames => ExportSeparate(snapshot, preset, processed),
            ExportLayout.SpriteSheet => ExportSheet(snapshot, preset, processed),
            ExportLayout.Atlas => ExportAtlas(snapshot, preset, processed),
            _ => throw new NotSupportedException($"Export layout '{preset.Layout}' is not supported."),
        };
    }

    private ProcessedFrame ProcessFrame(DocumentSnapshot snapshot, FrameId frameId, ExportPreset preset)
    {
        var rendered = _renderer.Render(snapshot, new FrameRenderRequest(frameId)).Surface;
        var canvas = ExportImage.FromRenderSurface(rendered);
        var canvasBounds = new IntRect(0, 0, snapshot.Canvas.Size.Width, snapshot.Canvas.Size.Height);
        var cropRect = preset.Crop ?? canvasBounds;
        if (!Contains(canvasBounds, cropRect)) throw new InvalidOperationException("Export crop must be fully inside the document canvas.");
        var cropped = canvas.Crop(cropRect);
        var localContent = new IntRect(0, 0, cropped.Size.Width, cropped.Size.Height);
        var isEmpty = false;
        ExportImage image;
        if (preset.Trim)
        {
            var trimmed = cropped.TrimAlpha();
            image = trimmed.Image;
            localContent = trimmed.ContentRect;
            isEmpty = trimmed.IsEmpty;
        }
        else
        {
            image = cropped;
        }

        var sourceRect = new IntRect(
            checked(cropRect.X + localContent.X),
            checked(cropRect.Y + localContent.Y),
            localContent.Width,
            localContent.Height);
        image = image.ScaleNearest(preset.Scale);
        return new ProcessedFrame(frameId, snapshot.GetFrame(frameId).DurationTicks, image, sourceRect, snapshot.Canvas.Size, isEmpty);
    }

    private static ExportBundle ExportSeparate(DocumentSnapshot snapshot, ExportPreset preset, IReadOnlyList<ProcessedFrame> frames)
    {
        var artifacts = new List<ExportArtifact>();
        var metadataFrames = new List<FrameMetadata>();
        for (var index = 0; index < frames.Count; index++)
        {
            var frame = frames[index];
            var path = $"{preset.ImageBaseName}_{index:D4}_{frame.FrameId}.png";
            artifacts.Add(new ExportArtifact(path, "image/png", PngCodec.Encode(frame.Image)));
            metadataFrames.Add(BuildFrameMetadata(snapshot, frame, path, new IntRect(0, 0, frame.Image.Size.Width, frame.Image.Size.Height)));
        }
        artifacts.Add(CreateMetadataArtifact(snapshot, preset, metadataFrames, artifacts.Where(item => item.MediaType == "image/png").Select(item => item.RelativePath)));
        return new ExportBundle(artifacts);
    }

    private static ExportBundle ExportSheet(DocumentSnapshot snapshot, ExportPreset preset, IReadOnlyList<ProcessedFrame> frames)
    {
        var columns = preset.SpriteSheetColumns > 0
            ? Math.Min(preset.SpriteSheetColumns, frames.Count)
            : checked((int)Math.Ceiling(Math.Sqrt(frames.Count)));
        var rows = checked((frames.Count + columns - 1) / columns);
        var cellWidth = frames.Max(frame => frame.Image.Size.Width) + (preset.Extrude * 2);
        var cellHeight = frames.Max(frame => frame.Image.Size.Height) + (preset.Extrude * 2);
        var width = checked((columns * cellWidth) + (Math.Max(0, columns - 1) * preset.Padding));
        var height = checked((rows * cellHeight) + (Math.Max(0, rows - 1) * preset.Padding));
        var placements = new List<ImagePlacement>();
        var metadata = new List<FrameMetadata>();
        for (var index = 0; index < frames.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var x = checked((column * (cellWidth + preset.Padding)) + preset.Extrude);
            var y = checked((row * (cellHeight + preset.Padding)) + preset.Extrude);
            var frame = frames[index];
            placements.Add(new ImagePlacement(frame.Image, x, y));
            metadata.Add(BuildFrameMetadata(snapshot, frame, $"{preset.ImageBaseName}.png", new IntRect(x, y, frame.Image.Size.Width, frame.Image.Size.Height)));
        }
        var image = ExportImage.Compose(new IntSize(width, height), placements, preset.Extrude);
        var pngPath = $"{preset.ImageBaseName}.png";
        return new ExportBundle([
            new ExportArtifact(pngPath, "image/png", PngCodec.Encode(image)),
            CreateMetadataArtifact(snapshot, preset, metadata, [pngPath]),
        ]);
    }

    private ExportBundle ExportAtlas(DocumentSnapshot snapshot, ExportPreset preset, IReadOnlyList<ProcessedFrame> frames)
    {
        var items = frames.Select(frame => new AtlasItem(
            frame.FrameId.ToString(),
            new IntSize(
                checked(frame.Image.Size.Width + preset.Extrude * 2),
                checked(frame.Image.Size.Height + preset.Extrude * 2)))).ToArray();
        var packing = _packers.Get(preset.AtlasPackerId).Pack(
            items,
            new AtlasPackingOptions(preset.MaxAtlasWidth, preset.MaxAtlasHeight, preset.Padding, preset.PowerOfTwoAtlas));
        var artifacts = new List<ExportArtifact>();
        var metadata = new List<FrameMetadata>();

        foreach (var page in packing.Pages)
        {
            var placements = new List<ImagePlacement>();
            foreach (var packed in page.Placements)
            {
                var frame = frames.Single(item => string.Equals(item.FrameId.ToString(), packed.Key, StringComparison.Ordinal));
                var contentX = checked(packed.Rect.X + preset.Extrude);
                var contentY = checked(packed.Rect.Y + preset.Extrude);
                placements.Add(new ImagePlacement(frame.Image, contentX, contentY));
                var pagePath = packing.Pages.Count == 1
                    ? $"{preset.ImageBaseName}.png"
                    : $"{preset.ImageBaseName}_{page.PageIndex:D2}.png";
                metadata.Add(BuildFrameMetadata(snapshot, frame, pagePath, new IntRect(contentX, contentY, frame.Image.Size.Width, frame.Image.Size.Height)));
            }
            var pageImage = ExportImage.Compose(page.Size, placements, preset.Extrude);
            var path = packing.Pages.Count == 1
                ? $"{preset.ImageBaseName}.png"
                : $"{preset.ImageBaseName}_{page.PageIndex:D2}.png";
            artifacts.Add(new ExportArtifact(path, "image/png", PngCodec.Encode(pageImage)));
        }

        artifacts.Add(CreateMetadataArtifact(snapshot, preset, metadata, artifacts.Select(item => item.RelativePath)));
        return new ExportBundle(artifacts);
    }

    private static ExportArtifact CreateMetadataArtifact(
        DocumentSnapshot snapshot,
        ExportPreset preset,
        IReadOnlyList<FrameMetadata> frames,
        IEnumerable<string> images)
    {
        var frameSet = frames.Select(frame => frame.Id).ToHashSet(StringComparer.Ordinal);
        var dto = new ExportMetadata
        {
            Version = 1,
            DocumentId = snapshot.Id.ToString(),
            Scale = preset.Scale,
            Images = images.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            Frames = frames.ToArray(),
            Clips = snapshot.Animation.Clips
                .Where(clip => frameSet.Contains(clip.StartFrameId.ToString()) || frameSet.Contains(clip.EndFrameId.ToString()))
                .Select(clip => new ClipMetadata(clip.Id.ToString(), clip.Name, clip.StartFrameId.ToString(), clip.EndFrameId.ToString(), clip.LoopMode.ToString()))
                .ToArray(),
            Tags = snapshot.Animation.Tags
                .Where(tag => frameSet.Contains(tag.StartFrameId.ToString()) || frameSet.Contains(tag.EndFrameId.ToString()))
                .Select(tag => new TagMetadata(tag.Id.ToString(), tag.Name, tag.StartFrameId.ToString(), tag.EndFrameId.ToString()))
                .ToArray(),
            Slices = snapshot.Animation.Slices
                .Select(slice => new SliceMetadata(
                    slice.Id.ToString(),
                    slice.Name,
                    RectMetadata.From(slice.Bounds),
                    PointMetadata.From(slice.Pivot),
                    slice.NineSlice is { } nine ? new NineSliceMetadata(nine.Left, nine.Top, nine.Right, nine.Bottom) : null))
                .ToArray(),
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(dto, MetadataJson.Options);
        return new ExportArtifact(preset.MetadataFileName, "application/json", json);
    }

    private static FrameMetadata BuildFrameMetadata(DocumentSnapshot snapshot, ProcessedFrame frame, string image, IntRect atlasRect)
    {
        snapshot.Animation.PivotTrack.Values.TryGetValue(frame.FrameId, out var pivot);
        snapshot.Animation.HitboxTrack.Values.TryGetValue(frame.FrameId, out var hitboxes);
        snapshot.Animation.HurtboxTrack.Values.TryGetValue(frame.FrameId, out var hurtboxes);
        snapshot.Animation.SocketTrack.Values.TryGetValue(frame.FrameId, out var sockets);
        snapshot.Animation.EventTrack.Values.TryGetValue(frame.FrameId, out var events);
        return new FrameMetadata
        {
            Id = frame.FrameId.ToString(),
            DurationTicks = frame.DurationTicks,
            Image = image,
            Rect = RectMetadata.From(atlasRect),
            SourceRect = RectMetadata.From(frame.SourceRect),
            SourceSize = SizeMetadata.From(frame.SourceSize),
            Empty = frame.IsEmpty,
            Pivot = snapshot.Animation.PivotTrack.Values.ContainsKey(frame.FrameId) ? PointMetadata.From(pivot) : null,
            Hitboxes = hitboxes?.Boxes.Select(box => new NamedRectMetadata(box.Name, RectMetadata.From(box.Bounds))).ToArray() ?? [],
            Hurtboxes = hurtboxes?.Boxes.Select(box => new NamedRectMetadata(box.Name, RectMetadata.From(box.Bounds))).ToArray() ?? [],
            Sockets = sockets?.Sockets.Select(socket => new SocketMetadata(socket.Name, PointMetadata.From(socket.Position))).ToArray() ?? [],
            Events = events?.Events.Select(marker => new EventMetadata(marker.Name, marker.Payload)).ToArray() ?? [],
        };
    }

    private static FrameId[] ResolveFrames(DocumentSnapshot snapshot, ExportFrameSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var order = snapshot.FrameOrder;
        return selection.Mode switch
        {
            ExportFrameSelectionMode.All => order.ToArray(),
            ExportFrameSelectionMode.Clip when selection.ClipId is { } clipId => Range(
                order,
                snapshot.Animation.Clips.Single(clip => clip.Id == clipId).StartFrameId,
                snapshot.Animation.Clips.Single(clip => clip.Id == clipId).EndFrameId),
            ExportFrameSelectionMode.Tag when selection.TagId is { } tagId => Range(
                order,
                snapshot.Animation.Tags.Single(tag => tag.Id == tagId).StartFrameId,
                snapshot.Animation.Tags.Single(tag => tag.Id == tagId).EndFrameId),
            ExportFrameSelectionMode.Explicit => order.Where(selection.FrameIds.Contains).ToArray(),
            _ => throw new InvalidOperationException("Export frame selection is incomplete."),
        };
    }

    private static FrameId[] Range(IReadOnlyList<FrameId> order, FrameId start, FrameId end)
    {
        var startIndex = IndexOf(order, start);
        var endIndex = IndexOf(order, end);
        if (startIndex > endIndex) throw new InvalidOperationException("Animation range start appears after end.");
        return order.Skip(startIndex).Take(endIndex - startIndex + 1).ToArray();
    }

    private static int IndexOf(IReadOnlyList<FrameId> order, FrameId id)
    {
        for (var index = 0; index < order.Count; index++) if (order[index] == id) return index;
        throw new InvalidOperationException($"Frame '{id}' is not present in the snapshot frame order.");
    }

    private static bool Contains(IntRect outer, IntRect inner) =>
        inner.X >= outer.X && inner.Y >= outer.Y &&
        checked(inner.X + inner.Width) <= checked(outer.X + outer.Width) &&
        checked(inner.Y + inner.Height) <= checked(outer.Y + outer.Height);

    private sealed record ProcessedFrame(
        FrameId FrameId,
        long DurationTicks,
        ExportImage Image,
        IntRect SourceRect,
        IntSize SourceSize,
        bool IsEmpty);
}

internal static class MetadataJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

internal sealed class ExportMetadata
{
    public int Version { get; init; }
    public string DocumentId { get; init; } = string.Empty;
    public int Scale { get; init; }
    public string[] Images { get; init; } = [];
    public FrameMetadata[] Frames { get; init; } = [];
    public ClipMetadata[] Clips { get; init; } = [];
    public TagMetadata[] Tags { get; init; } = [];
    public SliceMetadata[] Slices { get; init; } = [];
}

internal sealed class FrameMetadata
{
    public string Id { get; init; } = string.Empty;
    public long DurationTicks { get; init; }
    public string Image { get; init; } = string.Empty;
    public RectMetadata Rect { get; init; } = new(0, 0, 1, 1);
    public RectMetadata SourceRect { get; init; } = new(0, 0, 1, 1);
    public SizeMetadata SourceSize { get; init; } = new(1, 1);
    public bool Empty { get; init; }
    public PointMetadata? Pivot { get; init; }
    public NamedRectMetadata[] Hitboxes { get; init; } = [];
    public NamedRectMetadata[] Hurtboxes { get; init; } = [];
    public SocketMetadata[] Sockets { get; init; } = [];
    public EventMetadata[] Events { get; init; } = [];
}

internal sealed record RectMetadata(int X, int Y, int Width, int Height)
{
    public static RectMetadata From(IntRect value) => new(value.X, value.Y, value.Width, value.Height);
}
internal sealed record PointMetadata(int X, int Y)
{
    public static PointMetadata From(IntPoint value) => new(value.X, value.Y);
}
internal sealed record SizeMetadata(int Width, int Height)
{
    public static SizeMetadata From(IntSize value) => new(value.Width, value.Height);
}
internal sealed record NamedRectMetadata(string Name, RectMetadata Bounds);
internal sealed record SocketMetadata(string Name, PointMetadata Position);
internal sealed record EventMetadata(string Name, string Payload);
internal sealed record ClipMetadata(string Id, string Name, string StartFrameId, string EndFrameId, string LoopMode);
internal sealed record TagMetadata(string Id, string Name, string StartFrameId, string EndFrameId);
internal sealed record NineSliceMetadata(int Left, int Top, int Right, int Bottom);
internal sealed record SliceMetadata(string Id, string Name, RectMetadata Bounds, PointMetadata Pivot, NineSliceMetadata? NineSlice);
