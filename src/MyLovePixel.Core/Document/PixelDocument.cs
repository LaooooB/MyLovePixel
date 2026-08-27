using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Resources;

namespace MyLovePixel.Core.Document;

public sealed class PixelDocument
{
    private readonly Dictionary<LayerId, Layer> _layers = [];
    private readonly List<LayerId> _layerOrder = [];
    private readonly Dictionary<FrameId, Frame> _frames = [];
    private readonly List<FrameId> _frameOrder = [];
    private readonly Dictionary<CelId, Cel> _cels = [];

    public PixelDocument(DocumentId id, CanvasSpec canvas)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("DocumentId cannot be empty.", nameof(id));
        Id = id;
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
    }

    public DocumentId Id { get; }
    public CanvasSpec Canvas { get; }
    public ResourceStore Resources { get; } = new();
    public IReadOnlyList<LayerId> LayerOrder => _layerOrder;
    public IReadOnlyList<FrameId> FrameOrder => _frameOrder;
    public IReadOnlyCollection<Cel> Cels => _cels.Values;

    public Layer GetLayer(LayerId id) => _layers.TryGetValue(id, out var value)
        ? value
        : throw new KeyNotFoundException($"Layer '{id}' does not exist.");

    public Frame GetFrame(FrameId id) => _frames.TryGetValue(id, out var value)
        ? value
        : throw new KeyNotFoundException($"Frame '{id}' does not exist.");

    public Cel GetCel(CelId id) => _cels.TryGetValue(id, out var value)
        ? value
        : throw new KeyNotFoundException($"Cel '{id}' does not exist.");

    public Cel? FindCel(LayerId layerId, FrameId frameId) =>
        _cels.Values.FirstOrDefault(c => c.LayerId == layerId && c.FrameId == frameId);

    internal void AddLayer(Layer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (!_layers.TryAdd(layer.Id, layer)) throw new InvalidOperationException($"Layer '{layer.Id}' already exists.");
        _layerOrder.Add(layer.Id);
    }

    internal void AddFrame(Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!_frames.TryAdd(frame.Id, frame)) throw new InvalidOperationException($"Frame '{frame.Id}' already exists.");
        _frameOrder.Add(frame.Id);
    }

    internal void AddCel(Cel cel)
    {
        ArgumentNullException.ThrowIfNull(cel);
        if (!_layers.ContainsKey(cel.LayerId)) throw new InvalidOperationException("Cel references a missing layer.");
        if (!_frames.ContainsKey(cel.FrameId)) throw new InvalidOperationException("Cel references a missing frame.");
        if (!Resources.ContainsSurface(cel.SurfaceId)) throw new InvalidOperationException("Cel references a missing surface.");
        if (_cels.Values.Any(x => x.LayerId == cel.LayerId && x.FrameId == cel.FrameId))
            throw new InvalidOperationException("Only one Cel may occupy a Layer/Frame slot.");
        if (!_cels.TryAdd(cel.Id, cel)) throw new InvalidOperationException($"Cel '{cel.Id}' already exists.");
    }
}

public static class PixelDocumentFactory
{
    public static PixelDocument CreateBlank(int width, int height)
    {
        var document = new PixelDocument(DocumentId.New(), new CanvasSpec(new IntSize(width, height)));
        var layer = new PixelLayer(LayerId.New(), "Layer 1");
        var frame = new Frame(FrameId.New());
        document.AddLayer(layer);
        document.AddFrame(frame);

        var surfaceId = document.Resources.AddSurface(new PixelSurface(new IntSize(width, height)));
        document.AddCel(new Cel(CelId.New(), layer.Id, frame.Id, surfaceId));
        return document;
    }
}
