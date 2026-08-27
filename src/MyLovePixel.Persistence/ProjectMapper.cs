using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Validation;

namespace MyLovePixel.Persistence;

internal static class ProjectMapper
{
    public static DocumentDto ToDto(PixelDocument document, DocumentDto? template)
    {
        ArgumentNullException.ThrowIfNull(document);
        ThrowIfRuntimeInvalid(document);

        var layerTemplates = IndexById(template?.Layers);
        var frameTemplates = IndexById(template?.Frames);
        var celTemplates = IndexById(template?.Cels);
        var surfaceTemplates = IndexById(template?.Surfaces);

        var dto = new DocumentDto
        {
            Id = document.Id.ToString(),
            Canvas = new CanvasDto
            {
                Width = document.Canvas.Size.Width,
                Height = document.Canvas.Size.Height,
                ExtensionData = ExtensionData.Clone(template?.Canvas.ExtensionData),
            },
            ExtensionData = ExtensionData.Clone(template?.ExtensionData),
        };

        foreach (var layerId in document.LayerOrder)
        {
            var layer = document.GetLayer(layerId);
            if (layer is not PixelLayer)
                throw new PixelProjectException(PixelProjectErrorCode.ValidationFailed, $"Layer type '{layer.GetType().Name}' has no Batch 03 persistence mapping.");

            layerTemplates.TryGetValue(layer.Id.ToString(), out var previous);
            dto.Layers.Add(new LayerDto
            {
                Id = layer.Id.ToString(),
                Kind = "pixel",
                Name = layer.Name,
                Visible = layer.Visible,
                Locked = layer.Locked,
                Opacity = layer.Opacity,
                ExtensionData = ExtensionData.Clone(previous?.ExtensionData),
            });
        }

        foreach (var frameId in document.FrameOrder)
        {
            var frame = document.GetFrame(frameId);
            frameTemplates.TryGetValue(frame.Id.ToString(), out var previous);
            dto.Frames.Add(new FrameDto
            {
                Id = frame.Id.ToString(),
                DurationTicks = frame.DurationTicks,
                ExtensionData = ExtensionData.Clone(previous?.ExtensionData),
            });
        }

        var layerIndex = document.LayerOrder.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        var frameIndex = document.FrameOrder.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        foreach (var cel in document.Cels
                     .OrderBy(c => layerIndex[c.LayerId])
                     .ThenBy(c => frameIndex[c.FrameId])
                     .ThenBy(c => c.Id.Value))
        {
            celTemplates.TryGetValue(cel.Id.ToString(), out var previous);
            dto.Cels.Add(new CelDto
            {
                Id = cel.Id.ToString(),
                LayerId = cel.LayerId.ToString(),
                FrameId = cel.FrameId.ToString(),
                SurfaceId = cel.SurfaceId.ToString(),
                X = cel.Position.X,
                Y = cel.Position.Y,
                Opacity = cel.Opacity,
                ExtensionData = ExtensionData.Clone(previous?.ExtensionData),
            });
        }

        foreach (var surfaceId in document.Resources.SurfaceIds.OrderBy(id => id.Value))
        {
            var surface = document.Resources.GetSurface(surfaceId);
            surfaceTemplates.TryGetValue(surfaceId.ToString(), out var previous);
            var entryName = string.IsNullOrWhiteSpace(previous?.Entry)
                ? PixelProjectFormat.GetSurfaceEntry(surfaceId.ToString())
                : previous.Entry;
            ProjectEntryName.Validate(entryName);

            dto.Surfaces.Add(new SurfaceDto
            {
                Id = surfaceId.ToString(),
                Entry = entryName,
                Width = surface.Size.Width,
                Height = surface.Size.Height,
                Format = surface.Format switch
                {
                    PixelFormat.Rgba32 => "rgba32",
                    _ => throw new PixelProjectException(PixelProjectErrorCode.ValidationFailed, $"Pixel format '{surface.Format}' is not persistable."),
                },
                ExtensionData = ExtensionData.Clone(previous?.ExtensionData),
            });
        }

        return dto;
    }

    public static PixelDocument FromDto(DocumentDto dto, IReadOnlyDictionary<string, byte[]> entries)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(entries);

        try
        {
            var document = new PixelDocument(
                new DocumentId(ParseGuid(dto.Id, "document.id")),
                new CanvasSpec(new IntSize(dto.Canvas.Width, dto.Canvas.Height)));

            var layerIds = new HashSet<LayerId>();
            foreach (var item in dto.Layers)
            {
                if (!string.Equals(item.Kind, "pixel", StringComparison.Ordinal))
                    throw InvalidJson($"Unsupported layer kind '{item.Kind}'.");

                var id = new LayerId(ParseGuid(item.Id, "layer.id"));
                if (!layerIds.Add(id)) throw InvalidReference($"Duplicate layer id '{item.Id}'.");
                var layer = new PixelLayer(id, item.Name)
                {
                    Visible = item.Visible,
                    Locked = item.Locked,
                    Opacity = item.Opacity,
                };
                document.AddLayer(layer);
            }

            var frameIds = new HashSet<FrameId>();
            foreach (var item in dto.Frames)
            {
                var id = new FrameId(ParseGuid(item.Id, "frame.id"));
                if (!frameIds.Add(id)) throw InvalidReference($"Duplicate frame id '{item.Id}'.");
                document.AddFrame(new Frame(id, item.DurationTicks));
            }

            var surfaceIds = new HashSet<ResourceId>();
            var surfaceEntries = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in dto.Surfaces)
            {
                var id = new ResourceId(ParseGuid(item.Id, "surface.id"));
                if (!surfaceIds.Add(id)) throw InvalidReference($"Duplicate surface id '{item.Id}'.");
                ProjectEntryName.Validate(item.Entry);
                if (!surfaceEntries.Add(item.Entry)) throw InvalidReference($"Multiple surfaces reference entry '{item.Entry}'.");
                if (!entries.TryGetValue(item.Entry, out var encoded))
                    throw new PixelProjectException(PixelProjectErrorCode.MissingEntry, $"Surface '{item.Id}' references missing entry '{item.Entry}'.", item.Entry);
                if (!string.Equals(item.Format, "rgba32", StringComparison.Ordinal))
                    throw InvalidJson($"Unsupported surface format '{item.Format}'.");

                var surface = PixelSurfaceBinaryCodec.Decode(encoded, item.Entry);
                if (surface.Size.Width != item.Width || surface.Size.Height != item.Height)
                    throw new PixelProjectException(PixelProjectErrorCode.InvalidSurface, $"Surface descriptor dimensions do not match '{item.Entry}'.", item.Entry);
                document.Resources.AddSurface(id, surface);
            }

            var celIds = new HashSet<CelId>();
            foreach (var item in dto.Cels)
            {
                var id = new CelId(ParseGuid(item.Id, "cel.id"));
                if (!celIds.Add(id)) throw InvalidReference($"Duplicate cel id '{item.Id}'.");

                var layerId = new LayerId(ParseGuid(item.LayerId, "cel.layerId"));
                var frameId = new FrameId(ParseGuid(item.FrameId, "cel.frameId"));
                var surfaceId = new ResourceId(ParseGuid(item.SurfaceId, "cel.surfaceId"));
                if (!layerIds.Contains(layerId)) throw InvalidReference($"Cel '{item.Id}' references missing layer '{item.LayerId}'.");
                if (!frameIds.Contains(frameId)) throw InvalidReference($"Cel '{item.Id}' references missing frame '{item.FrameId}'.");
                if (!surfaceIds.Contains(surfaceId)) throw InvalidReference($"Cel '{item.Id}' references missing surface '{item.SurfaceId}'.");

                var cel = new Cel(id, layerId, frameId, surfaceId)
                {
                    Position = new IntPoint(item.X, item.Y),
                    Opacity = item.Opacity,
                };
                document.AddCel(cel);
            }

            var issues = DocumentValidator.Validate(document);
            if (issues.Count > 0)
                throw new PixelProjectException(
                    PixelProjectErrorCode.ValidationFailed,
                    string.Join(Environment.NewLine, issues.Select(issue => $"[{issue.Code}] {issue.Message}")));

            return document;
        }
        catch (PixelProjectException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            throw new PixelProjectException(PixelProjectErrorCode.InvalidJson, "Project document contains an invalid value.", PixelProjectFormat.DocumentEntry, ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new PixelProjectException(PixelProjectErrorCode.InvalidReference, "Project document contains inconsistent references.", PixelProjectFormat.DocumentEntry, ex);
        }
    }

    private static Dictionary<string, T> IndexById<T>(IEnumerable<T>? values) where T : class
    {
        if (values is null) return new Dictionary<string, T>(StringComparer.Ordinal);
        return values.ToDictionary(
            item => item switch
            {
                LayerDto x => x.Id,
                FrameDto x => x.Id,
                CelDto x => x.Id,
                SurfaceDto x => x.Id,
                _ => throw new NotSupportedException($"DTO type '{typeof(T).Name}' is not indexable."),
            },
            StringComparer.Ordinal);
    }

    private static Guid ParseGuid(string value, string field)
    {
        if (!Guid.TryParseExact(value, "N", out var id) || id == Guid.Empty)
            throw InvalidJson($"Field '{field}' must be a non-empty 32-digit Guid.");
        return id;
    }

    private static void ThrowIfRuntimeInvalid(PixelDocument document)
    {
        var issues = DocumentValidator.Validate(document);
        if (issues.Count == 0) return;
        throw new PixelProjectException(
            PixelProjectErrorCode.ValidationFailed,
            string.Join(Environment.NewLine, issues.Select(issue => $"[{issue.Code}] {issue.Message}")));
    }

    private static PixelProjectException InvalidJson(string message) =>
        new(PixelProjectErrorCode.InvalidJson, message, PixelProjectFormat.DocumentEntry);

    private static PixelProjectException InvalidReference(string message) =>
        new(PixelProjectErrorCode.InvalidReference, message, PixelProjectFormat.DocumentEntry);
}

internal static class ProjectEntryName
{
    public static void Validate(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
            throw new PixelProjectException(PixelProjectErrorCode.InvalidContainer, "ZIP entry name cannot be empty.", entryName);
        if (entryName.StartsWith('/', StringComparison.Ordinal) || entryName.Contains('\\', StringComparison.Ordinal))
            throw new PixelProjectException(PixelProjectErrorCode.InvalidContainer, $"Entry '{entryName}' is not a normalized project path.", entryName);
        if (entryName.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new PixelProjectException(PixelProjectErrorCode.InvalidContainer, $"Entry '{entryName}' contains an unsafe path segment.", entryName);
    }
}
