using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyLovePixel.Export;

/// <summary>
/// Final delivery layer for game-facing assets. It keeps the existing deterministic
/// layout/metadata exporter, then normalizes PNG artifacts to an engine-safe sprite
/// contract and emits import guidance for common engines.
/// </summary>
public sealed class GameReadyExporter : IExporter
{
    private readonly IExporter _inner;

    public GameReadyExporter(IExporter? inner = null)
    {
        _inner = inner ?? new GameAssetExporter();
        if (!string.Equals(_inner.Id, BuiltinExporterIds.GameAssets, StringComparison.Ordinal))
            throw new ArgumentException("Game-ready exporter must wrap the built-in game asset exporter.", nameof(inner));
    }

    public string Id => _inner.Id;

    public ExportBundle Export(ExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = _inner.Export(request);
        var artifacts = new List<ExportArtifact>(source.Artifacts.Count + 1);
        var images = new List<GameImageInfo>();

        foreach (var artifact in source.Artifacts)
        {
            if (!string.Equals(artifact.MediaType, "image/png", StringComparison.Ordinal))
            {
                artifacts.Add(artifact);
                continue;
            }

            var decoded = PngCodec.Decode(artifact.Content.Span);
            var normalized = NormalizeTransparentPixels(decoded);
            var png = GamePngNormalizer.EncodeSrgb(normalized);
            artifacts.Add(new ExportArtifact(artifact.RelativePath, artifact.MediaType, png));
            images.Add(Inspect(artifact.RelativePath, normalized));
        }

        var manifestPath = ResolveManifestPath(request.Preset.ImageBaseName, artifacts);
        artifacts.Add(new ExportArtifact(
            manifestPath,
            "application/vnd.mylovepixel.game-import+json",
            BuildImportManifest(request.Preset, images)));
        return new ExportBundle(artifacts);
    }

    private static ExportImage NormalizeTransparentPixels(ExportImage image)
    {
        var rgba = image.Bytes.ToArray();
        for (var offset = 0; offset < rgba.Length; offset += 4)
        {
            if (rgba[offset + 3] != 0) continue;
            // Game delivery contract: fully transparent texels are exactly RGBA(0,0,0,0).
            // This guarantees that no editor preview/checker/background color is baked in.
            rgba[offset] = 0;
            rgba[offset + 1] = 0;
            rgba[offset + 2] = 0;
        }
        return new ExportImage(image.Size, rgba);
    }

    private static GameImageInfo Inspect(string path, ExportImage image)
    {
        var hasAlpha = false;
        var hasPartialAlpha = false;
        var transparentPixels = 0;
        var rgba = image.Bytes.Span;
        for (var offset = 3; offset < rgba.Length; offset += 4)
        {
            var alpha = rgba[offset];
            if (alpha < 255) hasAlpha = true;
            if (alpha == 0) transparentPixels++;
            else if (alpha < 255) hasPartialAlpha = true;
        }

        return new GameImageInfo(
            path,
            image.Size.Width,
            image.Size.Height,
            IsPowerOfTwo(image.Size.Width) && IsPowerOfTwo(image.Size.Height),
            hasAlpha,
            hasPartialAlpha,
            transparentPixels);
    }

    private static byte[] BuildImportManifest(ExportPreset preset, IReadOnlyList<GameImageInfo> images)
    {
        var warnings = new List<string>();
        foreach (var image in images)
        {
            if (image.Width > 8192 || image.Height > 8192)
                warnings.Add($"{image.Path}: {image.Width}x{image.Height} exceeds the conservative 8192px cross-GPU texture target.");
        }
        if (preset.Layout == ExportLayout.Atlas && !preset.PowerOfTwoAtlas)
            warnings.Add("Unreal texture streaming prefers power-of-two textures. Enable Power-of-two atlas when streaming or mipmaps are required.");
        if (preset.Extrude > 0)
            warnings.Add("Extrude duplicates edge texels outside each metadata rect. This is intentional atlas guard data; unused atlas background remains transparent.");

        var dto = new GameImportManifest
        {
            Version = 1,
            Profile = "pixel-art-game-resource",
            Images = images.ToArray(),
            Source = new SourceContract
            {
                Container = "PNG",
                PixelFormat = "RGBA8",
                Lossless = true,
                ColorSpace = "sRGB",
                AlphaMode = "straight-unassociated",
                Background = "transparent",
                FullyTransparentTexel = "RGBA(0,0,0,0)",
                Interlaced = false,
            },
            Sampling = new SamplingContract
            {
                Filter = "nearest/point",
                Mipmaps = false,
                Wrap = "clamp",
                IntegerScale = preset.Scale,
            },
            Layout = new LayoutContract
            {
                Kind = preset.Layout.ToString(),
                Trimmed = preset.Trim,
                Padding = preset.Padding,
                Extrude = preset.Extrude,
                PowerOfTwoAtlas = preset.PowerOfTwoAtlas,
                Metadata = preset.MetadataFileName,
            },
            Engines = EngineImportContracts.Create(preset.Layout),
            Warnings = warnings.ToArray(),
        };

        return JsonSerializer.SerializeToUtf8Bytes(dto, ManifestJson.Options);
    }

    private static string ResolveManifestPath(string imageBaseName, IReadOnlyList<ExportArtifact> artifacts)
    {
        var occupied = artifacts.Select(item => item.RelativePath).ToHashSet(StringComparer.Ordinal);
        var preferred = $"{imageBaseName}.game-import.json";
        if (!occupied.Contains(preferred)) return preferred;
        for (var index = 1; index < 1000; index++)
        {
            var candidate = $"{imageBaseName}.game-import-{index:D2}.json";
            if (!occupied.Contains(candidate)) return candidate;
        }
        throw new InvalidOperationException("Could not allocate a unique game import manifest path.");
    }

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
}

internal static class GamePngNormalizer
{
    private static ReadOnlySpan<byte> PngSignature => [137, 80, 78, 71, 13, 10, 26, 10];

    public static byte[] EncodeSrgb(ExportImage image)
    {
        var png = PngCodec.Encode(image);
        // PngCodec always writes signature + IHDR first. Insert the standard sRGB
        // ancillary chunk immediately after IHDR so engines do not have to guess
        // the color space of sprite color data.
        const int afterIhdr = 8 + 4 + 4 + 13 + 4;
        if (png.Length < afterIhdr || !png.AsSpan(0, 8).SequenceEqual(PngSignature))
            throw new InvalidDataException("PNG encoder returned an invalid stream.");

        var chunk = BuildSrgbChunk();
        var output = new byte[checked(png.Length + chunk.Length)];
        png.AsSpan(0, afterIhdr).CopyTo(output);
        chunk.CopyTo(output.AsSpan(afterIhdr));
        png.AsSpan(afterIhdr).CopyTo(output.AsSpan(afterIhdr + chunk.Length));
        return output;
    }

    private static byte[] BuildSrgbChunk()
    {
        // PNG sRGB chunk: one-byte rendering intent. 0 = perceptual.
        var result = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(0, 4), 1);
        "sRGB"u8.CopyTo(result.AsSpan(4, 4));
        result[8] = 0;
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(9, 4), Crc32.Compute(result.AsSpan(4, 5)));
        return result;
    }

    private static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        public static uint Compute(ReadOnlySpan<byte> data)
        {
            var crc = uint.MaxValue;
            foreach (var value in data) crc = Table[(crc ^ value) & 0xff] ^ (crc >> 8);
            return ~crc;
        }

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint index = 0; index < table.Length; index++)
            {
                var value = index;
                for (var bit = 0; bit < 8; bit++) value = (value & 1) != 0 ? 0xedb88320u ^ (value >> 1) : value >> 1;
                table[index] = value;
            }
            return table;
        }
    }
}

internal static class EngineImportContracts
{
    public static EngineContracts Create(ExportLayout layout) => new()
    {
        Unity = new UnityImportContract
        {
            TextureType = "Sprite (2D and UI)",
            SpriteMode = layout == ExportLayout.SeparateFrames ? "Single" : "Multiple",
            AlphaSource = "Input Texture Alpha",
            AlphaIsTransparency = true,
            Srgb = true,
            FilterMode = "Point",
            GenerateMipMaps = false,
            Compression = "None for exact pixel art",
        },
        Godot = new GodotImportContract
        {
            ImportType = "Texture2D",
            Compression = "Lossless",
            TextureFilter = "Nearest",
            Mipmaps = false,
            FixAlphaBorder = true,
        },
        Unreal = new UnrealImportContract
        {
            SourceFormat = "PNG with alpha",
            Srgb = true,
            Filter = "Point",
            MipGenSettings = "NoMipMaps for exact pixel art",
            PowerOfTwoGuidance = "Use power-of-two dimensions when texture streaming or regular mipmaps are required.",
        },
    };
}

internal static class ManifestJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

internal sealed class GameImportManifest
{
    public int Version { get; init; }
    public string Profile { get; init; } = string.Empty;
    public SourceContract Source { get; init; } = new();
    public SamplingContract Sampling { get; init; } = new();
    public LayoutContract Layout { get; init; } = new();
    public GameImageInfo[] Images { get; init; } = [];
    public EngineContracts Engines { get; init; } = new();
    public string[] Warnings { get; init; } = [];
}

internal sealed class SourceContract
{
    public string Container { get; init; } = string.Empty;
    public string PixelFormat { get; init; } = string.Empty;
    public bool Lossless { get; init; }
    public string ColorSpace { get; init; } = string.Empty;
    public string AlphaMode { get; init; } = string.Empty;
    public string Background { get; init; } = string.Empty;
    public string FullyTransparentTexel { get; init; } = string.Empty;
    public bool Interlaced { get; init; }
}

internal sealed class SamplingContract
{
    public string Filter { get; init; } = string.Empty;
    public bool Mipmaps { get; init; }
    public string Wrap { get; init; } = string.Empty;
    public int IntegerScale { get; init; }
}

internal sealed class LayoutContract
{
    public string Kind { get; init; } = string.Empty;
    public bool Trimmed { get; init; }
    public int Padding { get; init; }
    public int Extrude { get; init; }
    public bool PowerOfTwoAtlas { get; init; }
    public string Metadata { get; init; } = string.Empty;
}

internal sealed record GameImageInfo(
    string Path,
    int Width,
    int Height,
    bool PowerOfTwo,
    bool HasAlpha,
    bool HasPartialAlpha,
    int TransparentPixels);

internal sealed class EngineContracts
{
    public UnityImportContract Unity { get; init; } = new();
    public GodotImportContract Godot { get; init; } = new();
    public UnrealImportContract Unreal { get; init; } = new();
}

internal sealed class UnityImportContract
{
    public string TextureType { get; init; } = string.Empty;
    public string SpriteMode { get; init; } = string.Empty;
    public string AlphaSource { get; init; } = string.Empty;
    public bool AlphaIsTransparency { get; init; }
    public bool Srgb { get; init; }
    public string FilterMode { get; init; } = string.Empty;
    public bool GenerateMipMaps { get; init; }
    public string Compression { get; init; } = string.Empty;
}

internal sealed class GodotImportContract
{
    public string ImportType { get; init; } = string.Empty;
    public string Compression { get; init; } = string.Empty;
    public string TextureFilter { get; init; } = string.Empty;
    public bool Mipmaps { get; init; }
    public bool FixAlphaBorder { get; init; }
}

internal sealed class UnrealImportContract
{
    public string SourceFormat { get; init; } = string.Empty;
    public bool Srgb { get; init; }
    public string Filter { get; init; } = string.Empty;
    public string MipGenSettings { get; init; } = string.Empty;
    public string PowerOfTwoGuidance { get; init; } = string.Empty;
}
