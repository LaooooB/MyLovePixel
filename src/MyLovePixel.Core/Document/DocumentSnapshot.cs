using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Document;

public sealed record CelSnapshot(
    CelId Id,
    LayerId LayerId,
    FrameId FrameId,
    ResourceId SurfaceId,
    IntPoint Position,
    byte Opacity);

public sealed class DocumentSnapshot
{
    private DocumentSnapshot(
        DocumentId id,
        CanvasSpec canvas,
        IReadOnlyList<LayerId> layerOrder,
        IReadOnlyList<FrameId> frameOrder,
        IReadOnlyList<CelSnapshot> cels,
        IReadOnlyDictionary<ResourceId, PixelSurfaceSnapshot> surfaces)
    {
        Id = id;
        Canvas = canvas;
        LayerOrder = layerOrder;
        FrameOrder = frameOrder;
        Cels = cels;
        Surfaces = surfaces;
    }

    public DocumentId Id { get; }
    public CanvasSpec Canvas { get; }
    public IReadOnlyList<LayerId> LayerOrder { get; }
    public IReadOnlyList<FrameId> FrameOrder { get; }
    public IReadOnlyList<CelSnapshot> Cels { get; }
    public IReadOnlyDictionary<ResourceId, PixelSurfaceSnapshot> Surfaces { get; }

    public static DocumentSnapshot Capture(PixelDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var surfaces = document.Resources.SurfaceIds.ToDictionary(id => id, id => document.Resources.GetSurface(id).Snapshot());
        var cels = document.Cels
            .Select(c => new CelSnapshot(c.Id, c.LayerId, c.FrameId, c.SurfaceId, c.Position, c.Opacity))
            .ToArray();

        return new DocumentSnapshot(
            document.Id,
            document.Canvas,
            document.LayerOrder.ToArray(),
            document.FrameOrder.ToArray(),
            cels,
            surfaces);
    }
}
