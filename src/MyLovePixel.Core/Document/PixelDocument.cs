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
        : this(id, canvas, new AnimationMetadata(), null)
    {
    }

    internal PixelDocument(
        DocumentId id,
        CanvasSpec canvas,
        AnimationMetadata animation,
        ulong? seed = null)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("DocumentId cannot be empty.", nameof(id));
        Id = id;
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        Animation = animation ?? throw new ArgumentNullException(nameof(animation));
        Seed = seed ?? DocumentSeed.Derive(id);
    }

    public DocumentId Id { get; }
    public ulong Seed { get; }
    public CanvasSpec Canvas { get; }
    public ResourceStore Resources { get; } = new();
    public AnimationMetadata Animation { get; }
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

    public int GetFrameIndex(FrameId frameId)
    {
        var index = _frameOrder.IndexOf(frameId);
        return index >= 0 ? index : throw new KeyNotFoundException($"Frame '{frameId}' does not exist.");
    }

    internal void AddLayer(Layer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (!_layers.TryAdd(layer.Id, layer)) throw new InvalidOperationException($"Layer '{layer.Id}' already exists.");
        _layerOrder.Add(layer.Id);
    }

    internal void AddFrame(Frame frame) => InsertFrame(_frameOrder.Count, frame);

    internal void InsertFrame(int index, Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if ((uint)index > (uint)_frameOrder.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (!_frames.TryAdd(frame.Id, frame)) throw new InvalidOperationException($"Frame '{frame.Id}' already exists.");
        _frameOrder.Insert(index, frame.Id);
    }

    internal void MoveFrame(FrameId frameId, int newIndex)
    {
        var oldIndex = GetFrameIndex(frameId);
        if ((uint)newIndex >= (uint)_frameOrder.Count) throw new ArgumentOutOfRangeException(nameof(newIndex));
        if (oldIndex == newIndex) return;

        var proposedOrder = _frameOrder.ToList();
        proposedOrder.RemoveAt(oldIndex);
        proposedOrder.Insert(newIndex, frameId);
        ValidateAnimationRanges(proposedOrder);

        _frameOrder.RemoveAt(oldIndex);
        _frameOrder.Insert(newIndex, frameId);
    }

    internal Frame RemoveFrame(FrameId frameId)
    {
        if (_cels.Values.Any(cel => cel.FrameId == frameId))
            throw new InvalidOperationException("Remove all Cels from a Frame before removing the Frame.");
        if (!_frames.Remove(frameId, out var frame))
            throw new KeyNotFoundException($"Frame '{frameId}' does not exist.");
        _frameOrder.Remove(frameId);
        return frame;
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

    internal Cel RemoveCel(CelId celId)
    {
        if (!_cels.Remove(celId, out var cel))
            throw new KeyNotFoundException($"Cel '{celId}' does not exist.");
        return cel;
    }

    internal bool IsSurfaceReferenced(ResourceId surfaceId) =>
        _cels.Values.Any(cel => cel.SurfaceId == surfaceId) ||
        Resources.IsSurfaceReferencedByTile(surfaceId);

    private void ValidateAnimationRanges(IReadOnlyList<FrameId> proposedOrder)
    {
        var positions = proposedOrder
            .Select((id, index) => (id, index))
            .ToDictionary(item => item.id, item => item.index);

        foreach (var clipId in Animation.ClipOrder)
        {
            var clip = Animation.GetClip(clipId);
            if (!positions.TryGetValue(clip.StartFrameId, out var start) ||
                !positions.TryGetValue(clip.EndFrameId, out var end) ||
                start > end)
                throw new InvalidOperationException(
                    $"Moving the frame would invert animation clip '{clip.Name}' ({clip.Id}).");
        }

        foreach (var tagId in Animation.TagOrder)
        {
            var tag = Animation.GetTag(tagId);
            if (!positions.TryGetValue(tag.StartFrameId, out var start) ||
                !positions.TryGetValue(tag.EndFrameId, out var end) ||
                start > end)
                throw new InvalidOperationException(
                    $"Moving the frame would invert animation tag '{tag.Name}' ({tag.Id}).");
        }
    }
}

public static class DocumentSeed
{
    public static ulong Derive(DocumentId id)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("DocumentId cannot be empty.", nameof(id));
        Span<byte> bytes = stackalloc byte[16];
        if (!id.Value.TryWriteBytes(bytes)) throw new InvalidOperationException("Unable to encode DocumentId.");

        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        foreach (var value in bytes)
        {
            hash ^= value;
            hash *= prime;
        }
        return hash;
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
