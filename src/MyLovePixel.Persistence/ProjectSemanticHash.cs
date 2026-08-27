using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using MyLovePixel.Core.Document;

namespace MyLovePixel.Persistence;

public static class ProjectSemanticHash
{
    public static string Compute(PixelDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AppendGuid(hash, document.Id.Value);
        AppendInt32(hash, document.Canvas.Size.Width);
        AppendInt32(hash, document.Canvas.Size.Height);

        AppendInt32(hash, document.LayerOrder.Count);
        foreach (var layerId in document.LayerOrder)
        {
            var layer = document.GetLayer(layerId);
            AppendGuid(hash, layer.Id.Value);
            AppendString(hash, layer switch
            {
                PixelLayer => "pixel",
                _ => layer.GetType().FullName ?? layer.GetType().Name,
            });
            AppendString(hash, layer.Name);
            AppendByte(hash, layer.Visible ? (byte)1 : (byte)0);
            AppendByte(hash, layer.Locked ? (byte)1 : (byte)0);
            AppendByte(hash, layer.Opacity);
        }

        AppendInt32(hash, document.FrameOrder.Count);
        foreach (var frameId in document.FrameOrder)
        {
            var frame = document.GetFrame(frameId);
            AppendGuid(hash, frame.Id.Value);
            AppendInt64(hash, frame.DurationTicks);
        }

        var cels = document.Cels.OrderBy(c => c.Id.Value).ToArray();
        AppendInt32(hash, cels.Length);
        foreach (var cel in cels)
        {
            AppendGuid(hash, cel.Id.Value);
            AppendGuid(hash, cel.LayerId.Value);
            AppendGuid(hash, cel.FrameId.Value);
            AppendGuid(hash, cel.SurfaceId.Value);
            AppendInt32(hash, cel.Position.X);
            AppendInt32(hash, cel.Position.Y);
            AppendByte(hash, cel.Opacity);
        }

        var surfaceIds = document.Resources.SurfaceIds.OrderBy(id => id.Value).ToArray();
        AppendInt32(hash, surfaceIds.Length);
        foreach (var surfaceId in surfaceIds)
        {
            AppendGuid(hash, surfaceId.Value);
            var snapshot = document.Resources.GetSurface(surfaceId).Snapshot();
            AppendInt32(hash, snapshot.Size.Width);
            AppendInt32(hash, snapshot.Size.Height);
            AppendInt32(hash, (int)snapshot.Format);
            AppendInt32(hash, snapshot.Bytes.Length);
            hash.AppendData(snapshot.Bytes.Span);
        }

        return "sha256:" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendGuid(IncrementalHash hash, Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        if (!value.TryWriteBytes(bytes)) throw new InvalidOperationException("Unable to encode Guid.");
        hash.AppendData(bytes);
    }

    private static void AppendString(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        AppendInt32(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendInt64(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendByte(IncrementalHash hash, byte value)
    {
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value;
        hash.AppendData(bytes);
    }
}
