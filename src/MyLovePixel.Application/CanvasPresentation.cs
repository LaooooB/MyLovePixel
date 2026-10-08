using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Application;

/// <summary>An immutable canvas snapshot with transient preview decorations.</summary>
public sealed class CanvasPresentation
{
    private readonly ReadOnlyMemory<byte> _rgba;
    private readonly IReadOnlyList<CanvasPreviewPixel> _previewPixels;
    private readonly IReadOnlyList<IntRect> _dirtyRegions;

    public CanvasPresentation(FrameId frameId, IntSize size, ReadOnlyMemory<byte> rgba,
        IEnumerable<CanvasPreviewPixel>? previewPixels = null, IEnumerable<IntRect>? dirtyRegions = null,
        CanvasRenderDiagnostics? diagnostics = null)
        : this(frameId, size, rgba, previewPixels, dirtyRegions, diagnostics, false) { }

    private CanvasPresentation(FrameId frameId, IntSize size, ReadOnlyMemory<byte> rgba,
        IEnumerable<CanvasPreviewPixel>? previewPixels, IEnumerable<IntRect>? dirtyRegions,
        CanvasRenderDiagnostics? diagnostics, bool reuseImmutableStorage)
    {
        if (rgba.Length != checked(size.Width * size.Height * 4))
            throw new ArgumentException("Canvas RGBA length does not match size.", nameof(rgba));
        FrameId = frameId;
        Size = size;
        // Public callers may own mutable arrays. Only the application render path
        // can reuse an already immutable CpuRenderSurface or CanvasPresentation.
        _rgba = reuseImmutableStorage ? rgba : rgba.ToArray();
        _previewPixels = Array.AsReadOnly((previewPixels ?? []).ToArray());
        _dirtyRegions = Array.AsReadOnly((dirtyRegions ?? []).ToArray());
        Diagnostics = diagnostics;
    }

    internal static CanvasPresentation FromImmutableRgba(FrameId frameId, IntSize size,
        ReadOnlyMemory<byte> rgba, IEnumerable<CanvasPreviewPixel>? previewPixels = null,
        IEnumerable<IntRect>? dirtyRegions = null, CanvasRenderDiagnostics? diagnostics = null) =>
        new(frameId, size, rgba, previewPixels, dirtyRegions, diagnostics, true);

    public FrameId FrameId { get; }
    public IntSize Size { get; }
    public ReadOnlyMemory<byte> Rgba => _rgba;
    public IReadOnlyList<CanvasPreviewPixel> PreviewPixels => _previewPixels;
    public IReadOnlyList<IntRect> DirtyRegions => _dirtyRegions;
    public CanvasRenderDiagnostics? Diagnostics { get; }
}
