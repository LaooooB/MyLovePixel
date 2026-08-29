using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Application;

public static partial class AdvancedEditingExtensions
{
    public static void ReplaceCurrentCanvasWithRgba(
        this DocumentSession session,
        ReadOnlyMemory<byte> rgba,
        string name = "Photo to Pixel")
    {
        ArgumentNullException.ThrowIfNull(session);

        var before = session.CaptureSnapshot();
        var canvasSize = before.Canvas.Size;
        var expected = checked(canvasSize.Width * canvasSize.Height * 4);
        if (rgba.Length != expected)
            throw new ArgumentException("RGBA byte length does not match the current canvas size.", nameof(rgba));

        using var transaction = session.Commands.BeginTransaction(name);
        try
        {
            session.EnsureEditableCel();
            var snapshot = session.CaptureSnapshot();
            var cel = snapshot.Cels.FirstOrDefault(value =>
                value.LayerId == session.CurrentLayerId && value.FrameId == session.CurrentFrameId)
                ?? throw new InvalidOperationException("Current Layer/Frame has no editable Cel.");
            var surface = snapshot.GetSurface(cel.SurfaceId);
            if (surface.Size != canvasSize)
                throw new InvalidOperationException("Current Cel surface must match the canvas size for photo conversion.");

            session.Execute(new ReplacePixelSurfaceCommand(
                cel.SurfaceId,
                PixelFormat.Rgba32,
                null,
                rgba,
                name));
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
