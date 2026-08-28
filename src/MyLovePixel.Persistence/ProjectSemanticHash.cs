using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

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
            AppendPoint(hash, cel.Position);
            AppendByte(hash, cel.Opacity);
        }

        AppendAnimation(hash, document);

        var paletteIds = document.Resources.PaletteIds.OrderBy(id => id.Value).ToArray();
        AppendInt32(hash, paletteIds.Length);
        foreach (var paletteId in paletteIds)
        {
            AppendGuid(hash, paletteId.Value);
            var palette = document.Resources.GetPalette(paletteId).Snapshot();
            AppendInt32(hash, palette.Count);
            AppendByte(hash, palette.TransparentIndex.HasValue ? (byte)1 : (byte)0);
            if (palette.TransparentIndex is { } transparentIndex)
                AppendByte(hash, transparentIndex);
            foreach (var color in palette.Colors)
                AppendColor(hash, color);
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
            AppendByte(hash, snapshot.PaletteId.HasValue ? (byte)1 : (byte)0);
            if (snapshot.PaletteId is { } paletteId)
                AppendGuid(hash, paletteId.Value);
            AppendInt32(hash, snapshot.Bytes.Length);
            hash.AppendData(snapshot.Bytes.Span);
        }

        return "sha256:" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendAnimation(IncrementalHash hash, PixelDocument document)
    {
        var animation = document.Animation;

        AppendInt32(hash, animation.ClipOrder.Count);
        foreach (var clipId in animation.ClipOrder)
        {
            var clip = animation.GetClip(clipId);
            AppendGuid(hash, clip.Id.Value);
            AppendString(hash, clip.Name);
            AppendGuid(hash, clip.StartFrameId.Value);
            AppendGuid(hash, clip.EndFrameId.Value);
            AppendInt32(hash, (int)clip.LoopMode);
        }

        AppendInt32(hash, animation.TagOrder.Count);
        foreach (var tagId in animation.TagOrder)
        {
            var tag = animation.GetTag(tagId);
            AppendGuid(hash, tag.Id.Value);
            AppendString(hash, tag.Name);
            AppendGuid(hash, tag.StartFrameId.Value);
            AppendGuid(hash, tag.EndFrameId.Value);
        }

        AppendInt32(hash, animation.SliceOrder.Count);
        foreach (var sliceId in animation.SliceOrder)
        {
            var slice = animation.GetSlice(sliceId);
            AppendGuid(hash, slice.Id.Value);
            AppendString(hash, slice.Name);
            AppendRect(hash, slice.Bounds);
            AppendPoint(hash, slice.Pivot);
            AppendByte(hash, slice.NineSlice.HasValue ? (byte)1 : (byte)0);
            if (slice.NineSlice is { } insets)
            {
                AppendInt32(hash, insets.Left);
                AppendInt32(hash, insets.Top);
                AppendInt32(hash, insets.Right);
                AppendInt32(hash, insets.Bottom);
            }
        }

        var frameIndex = document.FrameOrder
            .Select((id, index) => (id, index))
            .ToDictionary(item => item.id, item => item.index);
        AppendTrack(hash, animation.PivotTrack, frameIndex, AppendPoint);
        AppendTrack(hash, animation.HitboxTrack, frameIndex, AppendBoxFrame);
        AppendTrack(hash, animation.HurtboxTrack, frameIndex, AppendBoxFrame);
        AppendTrack(hash, animation.SocketTrack, frameIndex, AppendSocketFrame);
        AppendTrack(hash, animation.EventTrack, frameIndex, AppendEventFrame);
    }

    private static void AppendTrack<T>(
        IncrementalHash hash,
        AnimationTrack<T> track,
        IReadOnlyDictionary<FrameId, int> frameIndex,
        Action<IncrementalHash, T> appendValue)
    {
        AppendGuid(hash, track.Id.Value);
        AppendString(hash, track.Name);
        var values = track.Values.OrderBy(pair => frameIndex[pair.Key]).ToArray();
        AppendInt32(hash, values.Length);
        foreach (var pair in values)
        {
            AppendGuid(hash, pair.Key.Value);
            appendValue(hash, pair.Value);
        }
    }

    private static void AppendBoxFrame(IncrementalHash hash, BoxFrameValue value)
    {
        AppendInt32(hash, value.Boxes.Count);
        foreach (var box in value.Boxes)
        {
            AppendString(hash, box.Name);
            AppendRect(hash, box.Bounds);
        }
    }

    private static void AppendSocketFrame(IncrementalHash hash, SocketFrameValue value)
    {
        AppendInt32(hash, value.Sockets.Count);
        foreach (var socket in value.Sockets)
        {
            AppendString(hash, socket.Name);
            AppendPoint(hash, socket.Position);
        }
    }

    private static void AppendEventFrame(IncrementalHash hash, EventFrameValue value)
    {
        AppendInt32(hash, value.Events.Count);
        foreach (var marker in value.Events)
        {
            AppendString(hash, marker.Name);
            AppendString(hash, marker.Payload);
        }
    }

    private static void AppendColor(IncrementalHash hash, Rgba32 color)
    {
        AppendByte(hash, color.R);
        AppendByte(hash, color.G);
        AppendByte(hash, color.B);
        AppendByte(hash, color.A);
    }

    private static void AppendPoint(IncrementalHash hash, IntPoint value)
    {
        AppendInt32(hash, value.X);
        AppendInt32(hash, value.Y);
    }

    private static void AppendRect(IncrementalHash hash, IntRect value)
    {
        AppendInt32(hash, value.X);
        AppendInt32(hash, value.Y);
        AppendInt32(hash, value.Width);
        AppendInt32(hash, value.Height);
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
