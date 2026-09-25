using System.Text.Json;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Export;

namespace MyLovePixel.Application;

public static class JsonSpriteImportExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static DocumentSession ImportSpriteMetadata(this EditorWorkspace workspace, string path)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var metadata = JsonSerializer.Deserialize<SpriteMetadata>(File.ReadAllText(fullPath), JsonOptions)
            ?? throw new InvalidDataException("Sprite JSON is empty or invalid.");
        if (metadata.Version <= 0) throw new InvalidDataException("Sprite JSON version is missing or invalid.");
        if (metadata.Frames.Count == 0) throw new InvalidDataException("Sprite JSON contains no frames.");

        var firstSize = metadata.Frames[0].SourceSize;
        if (firstSize.Width <= 0 || firstSize.Height <= 0)
            throw new InvalidDataException("Sprite JSON sourceSize must be positive.");
        if (metadata.Frames.Any(frame =>
                frame.SourceSize.Width != firstSize.Width ||
                frame.SourceSize.Height != firstSize.Height))
            throw new NotSupportedException("All imported JSON frames must use the same sourceSize.");

        var canvasSize = new IntSize(firstSize.Width, firstSize.Height);
        var root = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        var scale = Math.Max(1, metadata.Scale);
        var images = new Dictionary<string, ExportImage>(StringComparer.OrdinalIgnoreCase);
        var rgbaFrames = metadata.Frames
            .Select(frame => RebuildFrame(frame, canvasSize, scale, root, images))
            .ToArray();

        var session = workspace.NewDocument(canvasSize.Width, canvasSize.Height);
        var imported = session.ImportSpriteSheetFrames(
            rgbaFrames,
            canvasSize,
            durationMilliseconds: 100,
            append: false,
            name: "Import Sprite JSON");

        for (var index = 0; index < imported.Count; index++)
        {
            session.SelectFrame(imported[index]);
            session.SetCurrentFrameDuration(Math.Max(1L, metadata.Frames[index].DurationTicks));
        }

        session.SelectFrame(imported[0]);
        session.MarkImported();
        return session;
    }

    private static byte[] RebuildFrame(
        SpriteFrame frame,
        IntSize canvasSize,
        int scale,
        string root,
        IDictionary<string, ExportImage> images)
    {
        var output = new byte[checked(canvasSize.Width * canvasSize.Height * 4)];
        if (frame.Empty) return output;
        if (string.IsNullOrWhiteSpace(frame.Image))
            throw new InvalidDataException("Sprite JSON frame is missing its image path.");

        ValidateSourceRect(frame.SourceRect, canvasSize);
        if (frame.Rect.Width <= 0 || frame.Rect.Height <= 0)
            throw new InvalidDataException("Sprite JSON frame rect must be positive.");

        var expectedWidth = checked(frame.SourceRect.Width * scale);
        var expectedHeight = checked(frame.SourceRect.Height * scale);
        if (frame.Rect.Width != expectedWidth || frame.Rect.Height != expectedHeight)
            throw new InvalidDataException(
                $"Frame rect {frame.Rect.Width}x{frame.Rect.Height} does not match sourceRect {frame.SourceRect.Width}x{frame.SourceRect.Height} at scale {scale}.");

        var image = LoadImage(root, frame.Image, images);
        if (frame.Rect.X < 0 || frame.Rect.Y < 0 ||
            checked(frame.Rect.X + frame.Rect.Width) > image.Size.Width ||
            checked(frame.Rect.Y + frame.Rect.Height) > image.Size.Height)
            throw new InvalidDataException($"Frame rect for '{frame.Image}' is outside the referenced PNG.");

        var source = image.Bytes.Span;
        for (var y = 0; y < frame.SourceRect.Height; y++)
        for (var x = 0; x < frame.SourceRect.Width; x++)
        {
            var sourceX = checked(frame.Rect.X + (x * scale));
            var sourceY = checked(frame.Rect.Y + (y * scale));
            var sourceOffset = checked(((sourceY * image.Size.Width) + sourceX) * 4);
            var targetX = checked(frame.SourceRect.X + x);
            var targetY = checked(frame.SourceRect.Y + y);
            var targetOffset = checked(((targetY * canvasSize.Width) + targetX) * 4);
            source.Slice(sourceOffset, 4).CopyTo(output.AsSpan(targetOffset, 4));
        }

        return output;
    }

    private static ExportImage LoadImage(
        string root,
        string relativePath,
        IDictionary<string, ExportImage> images)
    {
        var normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedRelative))
            throw new InvalidDataException("Sprite JSON image paths must be relative to the JSON file.");

        var imagePath = Path.GetFullPath(Path.Combine(root, normalizedRelative));
        var rootFull = Path.GetFullPath(root);
        var rootPrefix = rootFull.EndsWith(Path.DirectorySeparatorChar)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;
        if (!imagePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Sprite JSON image path escapes the JSON directory.");

        if (!File.Exists(imagePath))
            throw new FileNotFoundException(
                $"Sprite JSON references '{relativePath}', but that PNG was not found. Put the referenced PNG next to the JSON (or in its referenced subfolder) and import again.",
                imagePath);

        if (!images.TryGetValue(imagePath, out var image))
        {
            image = PngCodec.Decode(File.ReadAllBytes(imagePath));
            images.Add(imagePath, image);
        }

        return image;
    }

    private static void ValidateSourceRect(RectDto rect, IntSize canvasSize)
    {
        if (rect.Width <= 0 || rect.Height <= 0 ||
            rect.X < 0 || rect.Y < 0 ||
            checked(rect.X + rect.Width) > canvasSize.Width ||
            checked(rect.Y + rect.Height) > canvasSize.Height)
            throw new InvalidDataException("Sprite JSON sourceRect is outside sourceSize.");
    }

    private sealed class SpriteMetadata
    {
        public int Version { get; init; }
        public int Scale { get; init; } = 1;
        public List<SpriteFrame> Frames { get; init; } = [];
    }

    private sealed class SpriteFrame
    {
        public long DurationTicks { get; init; } = 100000;
        public string Image { get; init; } = string.Empty;
        public RectDto Rect { get; init; } = new();
        public RectDto SourceRect { get; init; } = new();
        public SizeDto SourceSize { get; init; } = new();
        public bool Empty { get; init; }
    }

    private sealed class RectDto
    {
        public int X { get; init; }
        public int Y { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
    }

    private sealed class SizeDto
    {
        public int Width { get; init; }
        public int Height { get; init; }
    }
}
