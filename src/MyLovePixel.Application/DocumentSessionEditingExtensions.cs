using MyLovePixel.Commands.Color;
using MyLovePixel.Commands.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Application;

public static class DocumentSessionEditingExtensions
{
    public static void RenameLayer(this DocumentSession session, LayerId layerId, string name)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Layer name cannot be empty.", nameof(name));
        var normalized = name.Trim();
        var current = session.GetLayers().Single(layer => layer.Id == layerId);
        if (string.Equals(current.Name, normalized, StringComparison.Ordinal)) return;
        session.Execute(new SetLayerNameCommand(layerId, normalized));
    }

    public static void SetLayerVisibility(this DocumentSession session, LayerId layerId, bool visible)
    {
        ArgumentNullException.ThrowIfNull(session);
        var current = session.GetLayers().Single(layer => layer.Id == layerId);
        if (current.Visible == visible) return;
        session.Execute(new SetLayerVisibilityCommand(layerId, visible));
    }

    public static void SetLayerLocked(this DocumentSession session, LayerId layerId, bool locked)
    {
        ArgumentNullException.ThrowIfNull(session);
        var current = session.GetLayers().Single(layer => layer.Id == layerId);
        if (current.Locked == locked) return;
        session.Execute(new SetLayerLockCommand(layerId, locked));
    }

    public static void SetLayerOpacity(this DocumentSession session, LayerId layerId, byte opacity)
    {
        ArgumentNullException.ThrowIfNull(session);
        var current = session.GetLayers().Single(layer => layer.Id == layerId);
        if (current.Opacity == opacity) return;
        session.Execute(new SetLayerOpacityCommand(layerId, opacity));
    }

    public static void SetPaletteColor(this DocumentSession session, PaletteId paletteId, byte index, Rgba32 color)
    {
        ArgumentNullException.ThrowIfNull(session);
        var snapshot = session.CaptureSnapshot();
        var palette = snapshot.GetPalette(paletteId);
        if (palette.GetColor(index) == color) return;
        session.Execute(new SetPaletteColorCommand(paletteId, index, color));
    }

    public static IReadOnlyList<PaletteEditorPresentation> GetPaletteEditors(this DocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var snapshot = session.CaptureSnapshot();
        return snapshot.Palettes
            .OrderBy(pair => pair.Key.Value)
            .Select(pair => new PaletteEditorPresentation(
                pair.Key,
                pair.Value.TransparentIndex,
                pair.Value.Revision,
                pair.Value.Colors
                    .Select((color, index) => new PaletteColorPresentation(
                        checked((byte)index),
                        color,
                        pair.Value.TransparentIndex == index))
                    .ToArray()))
            .ToArray();
    }
}

public sealed record PaletteEditorPresentation(
    PaletteId Id,
    byte? TransparentIndex,
    long Revision,
    IReadOnlyList<PaletteColorPresentation> Colors);

public sealed record PaletteColorPresentation(byte Index, Rgba32 Color, bool IsTransparent);
