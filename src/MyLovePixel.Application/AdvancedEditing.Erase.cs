using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Raster.Geometry;

namespace MyLovePixel.Application;

public static partial class AdvancedEditingExtensions
{
    public static void EraseCanvasSegment(this DocumentSession session, IntPoint from, IntPoint to)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.DrawingBlockedReason is { } blocked) throw new InvalidOperationException(blocked);
        var snapshot = session.CaptureSnapshot();
        var size = snapshot.Canvas.Size;
        if ((uint)from.X >= (uint)size.Width || (uint)from.Y >= (uint)size.Height ||
            (uint)to.X >= (uint)size.Width || (uint)to.Y >= (uint)size.Height) return;
        var cel = snapshot.Cels.FirstOrDefault(c => c.LayerId == session.CurrentLayerId && c.FrameId == session.CurrentFrameId);
        if (cel is null) return;
        var surface = snapshot.GetSurface(cel.SurfaceId);
        var writes = new List<PixelWrite>();
        foreach (var point in LineRasterizer.Rasterize(from, to))
        {
            var x = point.X - cel.Position.X;
            var y = point.Y - cel.Position.Y;
            if ((uint)x < (uint)surface.Size.Width && (uint)y < (uint)surface.Size.Height && surface.GetPixel(x, y).A != 0)
                writes.Add(new PixelWrite(x, y, Rgba32.Transparent));
        }
        if (writes.Count > 0) session.Execute(new PixelPatchCommand(cel.SurfaceId, writes, "Erase Pixels"));
    }
}
