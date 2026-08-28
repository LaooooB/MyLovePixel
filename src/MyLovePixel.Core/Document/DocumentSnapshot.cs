using System.Collections.ObjectModel;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;

namespace MyLovePixel.Core.Document;

public enum LayerSnapshotKind
{
    Pixel = 1,
}

public sealed record LayerSnapshot(
    LayerId Id,
    LayerSnapshotKind Kind,
    string Name,
    bool Visible,
    bool Locked,
    byte Opacity);

public sealed record FrameSnapshot(FrameId Id, long DurationTicks);

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
        ulong seed,
        CanvasSpec canvas,
        IReadOnlyList<LayerId> layerOrder,
        IReadOnlyList<FrameId> frameOrder,
        IReadOnlyDictionary<LayerId, LayerSnapshot> layers,
        IReadOnlyDictionary<FrameId, FrameSnapshot> frames,
        IReadOnlyList<CelSnapshot> cels,
        IReadOnlyDictionary<ResourceId, PixelSurfaceSnapshot> surfaces,
        IReadOnlyDictionary<PaletteId, PaletteSnapshot> palettes,
        IReadOnlyDictionary<TilesetId, TilesetSnapshot> tilesets,
        IReadOnlyDictionary<TilemapId, TilemapSnapshot> tilemaps,
        AnimationMetadataSnapshot animation)
    {
        Id = id;
        Seed = seed;
        Canvas = canvas;
        LayerOrder = layerOrder;
        FrameOrder = frameOrder;
        Layers = layers;
        Frames = frames;
        Cels = cels;
        Surfaces = surfaces;
        Palettes = palettes;
        Tilesets = tilesets;
        Tilemaps = tilemaps;
        Animation = animation;
    }

    public DocumentId Id { get; }
    public ulong Seed { get; }
    public CanvasSpec Canvas { get; }
    public IReadOnlyList<LayerId> LayerOrder { get; }
    public IReadOnlyList<FrameId> FrameOrder { get; }
    public IReadOnlyDictionary<LayerId, LayerSnapshot> Layers { get; }
    public IReadOnlyDictionary<FrameId, FrameSnapshot> Frames { get; }
    public IReadOnlyList<CelSnapshot> Cels { get; }
    public IReadOnlyDictionary<ResourceId, PixelSurfaceSnapshot> Surfaces { get; }
    public IReadOnlyDictionary<PaletteId, PaletteSnapshot> Palettes { get; }
    public IReadOnlyDictionary<TilesetId, TilesetSnapshot> Tilesets { get; }
    public IReadOnlyDictionary<TilemapId, TilemapSnapshot> Tilemaps { get; }
    public AnimationMetadataSnapshot Animation { get; }

    public LayerSnapshot GetLayer(LayerId id) => Layers.TryGetValue(id, out var layer)
        ? layer
        : throw new KeyNotFoundException($"Layer snapshot '{id}' does not exist.");

    public FrameSnapshot GetFrame(FrameId id) => Frames.TryGetValue(id, out var frame)
        ? frame
        : throw new KeyNotFoundException($"Frame snapshot '{id}' does not exist.");

    public PixelSurfaceSnapshot GetSurface(ResourceId id) => Surfaces.TryGetValue(id, out var surface)
        ? surface
        : throw new KeyNotFoundException($"Surface snapshot '{id}' does not exist.");

    public PaletteSnapshot GetPalette(PaletteId id) => Palettes.TryGetValue(id, out var palette)
        ? palette
        : throw new KeyNotFoundException($"Palette snapshot '{id}' does not exist.");

    public TilesetSnapshot GetTileset(TilesetId id) => Tilesets.TryGetValue(id, out var tileset)
        ? tileset
        : throw new KeyNotFoundException($"Tileset snapshot '{id}' does not exist.");

    public TilemapSnapshot GetTilemap(TilemapId id) => Tilemaps.TryGetValue(id, out var tilemap)
        ? tilemap
        : throw new KeyNotFoundException($"Tilemap snapshot '{id}' does not exist.");

    public static DocumentSnapshot Capture(PixelDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var layerOrder = document.LayerOrder.ToArray();
        var frameOrder = document.FrameOrder.ToArray();
        var layerIndex = layerOrder
            .Select((id, index) => (id, index))
            .ToDictionary(item => item.id, item => item.index);
        var frameIndex = frameOrder
            .Select((id, index) => (id, index))
            .ToDictionary(item => item.id, item => item.index);

        var layers = new Dictionary<LayerId, LayerSnapshot>(layerOrder.Length);
        foreach (var id in layerOrder)
        {
            var layer = document.GetLayer(id);
            var kind = layer switch
            {
                PixelLayer => LayerSnapshotKind.Pixel,
                _ => throw new NotSupportedException($"Layer type '{layer.GetType().Name}' has no snapshot mapping."),
            };

            layers.Add(id, new LayerSnapshot(
                layer.Id,
                kind,
                layer.Name,
                layer.Visible,
                layer.Locked,
                layer.Opacity));
        }

        var frames = new Dictionary<FrameId, FrameSnapshot>(frameOrder.Length);
        foreach (var id in frameOrder)
        {
            var frame = document.GetFrame(id);
            frames.Add(id, new FrameSnapshot(frame.Id, frame.DurationTicks));
        }

        var palettes = document.Resources.PaletteIds
            .OrderBy(id => id.Value)
            .ToDictionary(
                id => id,
                id => document.Resources.GetPalette(id).Snapshot());

        var surfaces = document.Resources.SurfaceIds
            .OrderBy(id => id.Value)
            .ToDictionary(
                id => id,
                id => document.Resources.GetSurface(id).Snapshot());

        var tilesets = document.Resources.TilesetIds
            .OrderBy(id => id.Value)
            .ToDictionary(
                id => id,
                id => document.Resources.GetTileset(id).Snapshot());

        var tilemaps = document.Resources.TilemapIds
            .OrderBy(id => id.Value)
            .ToDictionary(
                id => id,
                id => document.Resources.GetTilemap(id).Snapshot());

        var cels = document.Cels
            .OrderBy(cel => frameIndex[cel.FrameId])
            .ThenBy(cel => layerIndex[cel.LayerId])
            .ThenBy(cel => cel.Id.Value)
            .Select(cel => new CelSnapshot(
                cel.Id,
                cel.LayerId,
                cel.FrameId,
                cel.SurfaceId,
                cel.Position,
                cel.Opacity))
            .ToArray();

        return new DocumentSnapshot(
            document.Id,
            document.Seed,
            document.Canvas,
            Array.AsReadOnly(layerOrder),
            Array.AsReadOnly(frameOrder),
            new ReadOnlyDictionary<LayerId, LayerSnapshot>(layers),
            new ReadOnlyDictionary<FrameId, FrameSnapshot>(frames),
            Array.AsReadOnly(cels),
            new ReadOnlyDictionary<ResourceId, PixelSurfaceSnapshot>(surfaces),
            new ReadOnlyDictionary<PaletteId, PaletteSnapshot>(palettes),
            new ReadOnlyDictionary<TilesetId, TilesetSnapshot>(tilesets),
            new ReadOnlyDictionary<TilemapId, TilemapSnapshot>(tilemaps),
            document.Animation.Snapshot());
    }
}
