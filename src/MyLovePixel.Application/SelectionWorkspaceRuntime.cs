using System.Runtime.CompilerServices;
using MyLovePixel.Animation;
using MyLovePixel.Commands.Color;
using MyLovePixel.Commands.Document;
using MyLovePixel.Commands.Effects;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Commands.Resources;
using MyLovePixel.Commands.Tiles;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;
using MyLovePixel.Effects;
using MyLovePixel.Export;
using MyLovePixel.Persistence;
using MyLovePixel.Render;
using MyLovePixel.Selection;

namespace MyLovePixel.Application;

public sealed class SelectionWorkspaceRuntime
{
    private sealed record State(ResourceId SurfaceId, IntPoint Origin, SelectionMask Mask);
    private readonly ConditionalWeakTable<DocumentSession, Holder> _states = new();
    private sealed class Holder { public State? Value; }

    public SelectionOverlayPresentation? GetOverlay(DocumentSession session)
    {
        if (!_states.TryGetValue(session, out var holder) || holder.Value is not { } state || state.Mask.IsEmpty) return null;
        var b = state.Mask.Bounds;
        var pixels = state.Mask.EnumerateSelected()
            .Select(point => new IntPoint(point.X + state.Origin.X, point.Y + state.Origin.Y))
            .ToArray();
        return new SelectionOverlayPresentation(
            new IntRect(b.X + state.Origin.X, b.Y + state.Origin.Y, b.Width, b.Height),
            pixels);
    }

    public void SelectRectangle(DocumentSession session, int ax, int ay, int bx, int by)
    {
        var target = Resolve(session);
        var left = Math.Min(ax, bx) - target.Origin.X;
        var top = Math.Min(ay, by) - target.Origin.Y;
        var right = Math.Max(ax, bx) - target.Origin.X;
        var bottom = Math.Max(ay, by) - target.Origin.Y;
        var bounds = new IntRect(left, top, right - left + 1, bottom - top + 1);
        var mask = SelectionFactory.Rectangle(target.Surface.Size, bounds);
        _states.GetOrCreateValue(session).Value = new State(target.Cel.SurfaceId, target.Origin, mask);
    }

    public void SelectEllipse(DocumentSession session, int ax, int ay, int bx, int by)
    {
        var target = Resolve(session);
        var left = Math.Min(ax, bx) - target.Origin.X;
        var top = Math.Min(ay, by) - target.Origin.Y;
        var right = Math.Max(ax, bx) - target.Origin.X;
        var bottom = Math.Max(ay, by) - target.Origin.Y;
        var mask = SelectionFactory.Ellipse(target.Surface.Size, new IntRect(left, top, right - left + 1, bottom - top + 1));
        _states.GetOrCreateValue(session).Value = new State(target.Cel.SurfaceId, target.Origin, mask);
    }

    public void SelectLasso(DocumentSession session, IReadOnlyList<IntPoint> canvasVertices)
    {
        ArgumentNullException.ThrowIfNull(canvasVertices);
        if (canvasVertices.Count < 3) return;
        var target = Resolve(session);
        var local = canvasVertices.Select(point => new IntPoint(point.X - target.Origin.X, point.Y - target.Origin.Y)).ToArray();
        var mask = SelectionFactory.Lasso(target.Surface.Size, local);
        _states.GetOrCreateValue(session).Value = new State(target.Cel.SurfaceId, target.Origin, mask);
    }

    public void SelectByColor(DocumentSession session, int canvasX, int canvasY)
    {
        var target = Resolve(session);
        var x = canvasX - target.Origin.X;
        var y = canvasY - target.Origin.Y;
        if ((uint)x >= (uint)target.Surface.Size.Width || (uint)y >= (uint)target.Surface.Size.Height) return;
        var reference = target.Surface.GetPixel(x, y);
        var mask = SelectionFactory.ByColor(target.Surface, reference);
        _states.GetOrCreateValue(session).Value = new State(target.Cel.SurfaceId, target.Origin, mask);
    }

    public void SelectAll(DocumentSession session)
    {
        var target = Resolve(session);
        var mask = SelectionFactory.Rectangle(target.Surface.Size, new IntRect(0, 0, target.Surface.Size.Width, target.Surface.Size.Height));
        _states.GetOrCreateValue(session).Value = new State(target.Cel.SurfaceId, target.Origin, mask);
    }

    public void Clear(DocumentSession session) => _states.GetOrCreateValue(session).Value = null;

    public void Invert(DocumentSession session)
    {
        var state = Require(session);
        _states.GetOrCreateValue(session).Value = state with { Mask = SelectionMaskOperations.Invert(state.Mask) };
    }

    public void Move(DocumentSession session, int dx, int dy)
    {
        var state = Require(session);
        var delta = new IntPoint(dx, dy);
        if (delta == default) return;
        var surface = session.Document.Resources.GetSurface(state.SurfaceId).Snapshot();
        var patch = FloatingContentComposer.BuildMovePatch(surface, state.Mask, delta);
        if (!patch.IsEmpty) session.Execute(new PixelPatchCommand(state.SurfaceId, patch.Writes, "Move Selection"));
        _states.GetOrCreateValue(session).Value = state with { Mask = SelectionTransforms.Translate(state.Mask, delta) };
    }

    public void Scale(DocumentSession session, int width, int height)
    {
        var overlay = GetOverlay(session) ?? throw new InvalidOperationException("No selection.");
        ScaleToBounds(session, new IntRect(overlay.Bounds.X, overlay.Bounds.Y, width, height));
    }

    public void ScaleToBounds(DocumentSession session, IntRect targetCanvasBounds)
    {
        if (targetCanvasBounds.Width <= 0 || targetCanvasBounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetCanvasBounds));

        var state = Require(session);
        var surface = session.Document.Resources.GetSurface(state.SurfaceId).Snapshot();
        var floating = FloatingContent.Capture(surface, state.Mask);
        var scaled = FloatingContentTransforms.ScaleNearest(
            floating,
            new IntSize(targetCanvasBounds.Width, targetCanvasBounds.Height));
        var transformed = FloatingContentTransforms.Place(
            scaled,
            new IntPoint(
                checked(targetCanvasBounds.X - state.Origin.X),
                checked(targetCanvasBounds.Y - state.Origin.Y)));
        ApplyFloatingTransform(session, state, surface, transformed, "Scale Selection");
    }

    public void Rotate(DocumentSession session, double degrees)
    {
        if (!double.IsFinite(degrees)) throw new ArgumentOutOfRangeException(nameof(degrees));
        if (Math.Abs(degrees) < 0.000001d) return;

        var state = Require(session);
        var surface = session.Document.Resources.GetSurface(state.SurfaceId).Snapshot();
        var floating = FloatingContent.Capture(surface, state.Mask);
        var transformed = FloatingContentTransforms.RotateNearest(floating, degrees);
        ApplyFloatingTransform(session, state, surface, transformed, "Rotate Selection");
    }

    public void FlipHorizontal(DocumentSession session) => Transform(session, FloatingContentTransforms.FlipHorizontal, SelectionTransforms.FlipHorizontal, "Flip Selection H");
    public void FlipVertical(DocumentSession session) => Transform(session, FloatingContentTransforms.FlipVertical, SelectionTransforms.FlipVertical, "Flip Selection V");
    public void RotateClockwise(DocumentSession session) => Transform(session, value => FloatingContentTransforms.Rotate90(value, QuarterTurn.Clockwise), value => SelectionTransforms.Rotate90(value, QuarterTurn.Clockwise), "Rotate Selection");

    private void ApplyFloatingTransform(
        DocumentSession session,
        State state,
        PixelSurfaceSnapshot surface,
        FloatingContent transformed,
        string name)
    {
        var patch = FloatingContentComposer.BuildTransformPatch(surface, state.Mask, transformed);
        if (!patch.IsEmpty) session.Execute(new PixelPatchCommand(state.SurfaceId, patch.Writes, name));
        var mask = BuildSurfaceMask(surface.Size, state.Mask.Format, transformed);
        _states.GetOrCreateValue(session).Value = state with { Mask = mask };
    }

    private static SelectionMask BuildSurfaceMask(
        IntSize surfaceSize,
        SelectionMaskFormat format,
        FloatingContent transformed)
    {
        var coverage = new byte[checked(surfaceSize.Width * surfaceSize.Height)];
        for (var localY = 0; localY < transformed.Size.Height; localY++)
        for (var localX = 0; localX < transformed.Size.Width; localX++)
        {
            var value = transformed.Mask.GetCoverage(localX, localY);
            if (value == 0) continue;
            var targetX = (long)transformed.Position.X + localX;
            var targetY = (long)transformed.Position.Y + localY;
            if ((ulong)targetX >= (ulong)surfaceSize.Width || (ulong)targetY >= (ulong)surfaceSize.Height) continue;
            coverage[((int)targetY * surfaceSize.Width) + (int)targetX] = value;
        }
        return SelectionMask.FromCoverage(surfaceSize, format, coverage);
    }

    private void Transform(DocumentSession session, Func<FloatingContent, FloatingContent> contentTransform, Func<SelectionMask, SelectionMask> maskTransform, string name)
    {
        var state = Require(session);
        var surface = session.Document.Resources.GetSurface(state.SurfaceId).Snapshot();
        var floating = FloatingContent.Capture(surface, state.Mask);
        var transformed = contentTransform(floating);
        var patch = FloatingContentComposer.BuildTransformPatch(surface, state.Mask, transformed);
        if (!patch.IsEmpty) session.Execute(new PixelPatchCommand(state.SurfaceId, patch.Writes, name));
        _states.GetOrCreateValue(session).Value = state with { Mask = maskTransform(state.Mask) };
    }

    private State Require(DocumentSession session) => _states.TryGetValue(session, out var holder) && holder.Value is { } state
        ? state
        : throw new InvalidOperationException("No selection.");

    private static (CelSnapshot Cel, PixelSurfaceSnapshot Surface, IntPoint Origin) Resolve(DocumentSession session)
    {
        var snapshot = session.CaptureSnapshot();
        var cel = snapshot.Cels.FirstOrDefault(v => v.LayerId == session.CurrentLayerId && v.FrameId == session.CurrentFrameId)
            ?? throw new InvalidOperationException("Current Layer/Frame has no Cel.");
        return (cel, snapshot.GetSurface(cel.SurfaceId), cel.Position);
    }
}
