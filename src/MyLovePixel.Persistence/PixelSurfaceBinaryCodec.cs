using System.Buffers.Binary;
using System.Security.Cryptography;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Persistence;

internal static class PixelSurfaceBinaryCodec
{
    private const int HeaderSize = 52;
    private const ushort CodecVersion = 1;
    private static ReadOnlySpan<byte> Magic => "MLPX"u8;

    public static byte[] Encode(PixelSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var snapshot = surface.Snapshot();
        var payload = snapshot.Bytes.Span;
        var result = GC.AllocateUninitializedArray<byte>(checked(HeaderSize + payload.Length));
        var header = result.AsSpan(0, HeaderSize);

        Magic.CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header[4..6], CodecVersion);
        header[6] = (byte)snapshot.Format;
        header[7] = 0;
        BinaryPrimitives.WriteInt32LittleEndian(header[8..12], snapshot.Size.Width);
        BinaryPrimitives.WriteInt32LittleEndian(header[12..16], snapshot.Size.Height);
        BinaryPrimitives.WriteInt32LittleEndian(header[16..20], payload.Length);
        SHA256.HashData(payload, header[20..52]);
        payload.CopyTo(result.AsSpan(HeaderSize));
        return result;
    }

    public static PixelSurface Decode(
        ReadOnlySpan<byte> encoded,
        string entryName,
        PaletteId? paletteId = null)
    {
        if (encoded.Length < HeaderSize)
            throw Invalid(entryName, "Surface entry is shorter than the MLPX header.");
        if (!encoded[..4].SequenceEqual(Magic))
            throw Invalid(entryName, "Surface entry has an invalid MLPX magic value.");

        var version = BinaryPrimitives.ReadUInt16LittleEndian(encoded[4..6]);
        if (version != CodecVersion)
            throw Invalid(entryName, $"Unsupported MLPX codec version {version}.");

        var format = (PixelFormat)encoded[6];
        if (format is not (PixelFormat.Rgba32 or PixelFormat.Indexed8))
            throw Invalid(entryName, $"Unsupported pixel format value {(byte)format}.");
        if (encoded[7] != 0)
            throw Invalid(entryName, "Reserved MLPX header byte must be zero.");

        var width = BinaryPrimitives.ReadInt32LittleEndian(encoded[8..12]);
        var height = BinaryPrimitives.ReadInt32LittleEndian(encoded[12..16]);
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(encoded[16..20]);
        if (width <= 0 || height <= 0 || payloadLength < 0)
            throw Invalid(entryName, "Surface dimensions or payload length are invalid.");

        int expectedPayloadLength;
        try
        {
            var bytesPerPixel = format == PixelFormat.Rgba32 ? 4 : 1;
            expectedPayloadLength = checked(width * height * bytesPerPixel);
        }
        catch (OverflowException ex)
        {
            throw new PixelProjectException(
                PixelProjectErrorCode.InvalidSurface,
                "Surface dimensions overflow pixel storage.",
                entryName,
                ex);
        }

        if (payloadLength != expectedPayloadLength || encoded.Length != HeaderSize + payloadLength)
            throw Invalid(entryName, "Surface payload length does not match dimensions and format.");

        var payload = encoded[HeaderSize..];
        Span<byte> actualHash = stackalloc byte[32];
        SHA256.HashData(payload, actualHash);
        if (!CryptographicOperations.FixedTimeEquals(encoded[20..52], actualHash))
            throw Invalid(entryName, "Surface payload SHA-256 does not match its header.");

        try
        {
            var size = new IntSize(width, height);
            if (format == PixelFormat.Rgba32)
            {
                if (paletteId is not null)
                    throw new ArgumentException("RGBA32 surface descriptors cannot reference a palette.", nameof(paletteId));
                return PixelSurface.FromRgbaBytes(size, payload);
            }

            if (paletteId is not { } indexedPaletteId)
                throw new ArgumentException("Indexed8 surface descriptors must reference a palette.", nameof(paletteId));
            return PixelSurface.FromIndexedBytes(size, indexedPaletteId, payload);
        }
        catch (ArgumentException ex)
        {
            throw new PixelProjectException(
                PixelProjectErrorCode.InvalidSurface,
                "Surface payload could not be reconstructed.",
                entryName,
                ex);
        }
    }

    private static PixelProjectException Invalid(string entryName, string message) =>
        new(PixelProjectErrorCode.InvalidSurface, message, entryName);
}
