using System.Buffers.Binary;
using System.IO.Compression;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Export;

public static class PngCodec
{
    private static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];

    public static byte[] Encode(ExportImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        using var output = new MemoryStream();
        output.Write(Signature);
        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr[..4], checked((uint)image.Size.Width));
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.Slice(4, 4), checked((uint)image.Size.Height));
        ihdr[8] = 8;
        ihdr[9] = 6;
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        WriteChunk(output, "IHDR"u8, ihdr);

        using var raw = new MemoryStream();
        var bytes = image.Bytes.Span;
        for (var y = 0; y < image.Size.Height; y++)
        {
            raw.WriteByte(0);
            raw.Write(bytes.Slice(y * image.Size.Width * 4, image.Size.Width * 4));
        }
        raw.Position = 0;
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true)) raw.CopyTo(zlib);
        WriteChunk(output, "IDAT"u8, compressed.ToArray());
        WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
        return output.ToArray();
    }

    public static ExportImage Decode(ReadOnlySpan<byte> png)
    {
        if (png.Length < Signature.Length || !png[..Signature.Length].SequenceEqual(Signature))
            throw new InvalidDataException("Invalid PNG signature.");

        var offset = Signature.Length;
        var width = 0;
        var height = 0;
        byte bitDepth = 0;
        byte colorType = 0;
        byte interlace = 0;
        byte[]? palette = null;
        byte[]? transparency = null;
        using var idat = new MemoryStream();
        var sawHeader = false;
        var sawEnd = false;

        while (offset < png.Length)
        {
            if (png.Length - offset < 12) throw new InvalidDataException("Truncated PNG chunk.");
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.Slice(offset, 4)));
            offset += 4;
            var type = png.Slice(offset, 4);
            offset += 4;
            if (length < 0 || png.Length - offset < checked(length + 4)) throw new InvalidDataException("Truncated PNG chunk data.");
            var data = png.Slice(offset, length);
            offset += length;
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(png.Slice(offset, 4));
            offset += 4;
            if (expectedCrc != Crc32.Compute(type, data)) throw new InvalidDataException("PNG chunk CRC mismatch.");

            if (type.SequenceEqual("IHDR"u8))
            {
                if (sawHeader || length != 13) throw new InvalidDataException("Invalid PNG IHDR.");
                width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data[..4]));
                height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4)));
                bitDepth = data[8];
                colorType = data[9];
                if (data[10] != 0 || data[11] != 0) throw new NotSupportedException("Unsupported PNG compression/filter method.");
                interlace = data[12];
                sawHeader = true;
            }
            else if (type.SequenceEqual("PLTE"u8))
            {
                if (!sawHeader) throw new InvalidDataException("PNG PLTE appears before IHDR.");
                if (data.Length is 0 or > 768 || data.Length % 3 != 0) throw new InvalidDataException("PNG palette length is invalid.");
                palette = data.ToArray();
            }
            else if (type.SequenceEqual("tRNS"u8))
            {
                if (!sawHeader) throw new InvalidDataException("PNG tRNS appears before IHDR.");
                transparency = data.ToArray();
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                if (!sawHeader) throw new InvalidDataException("PNG IDAT appears before IHDR.");
                idat.Write(data);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                sawEnd = true;
                break;
            }
        }

        if (!sawHeader || !sawEnd) throw new InvalidDataException("PNG is missing required chunks.");
        if (width <= 0 || height <= 0) throw new InvalidDataException("PNG dimensions must be positive.");
        if (interlace != 0) throw new NotSupportedException("Interlaced PNG images are not supported.");
        ValidateFormat(colorType, bitDepth, palette, transparency);

        var bitsPerPixel = colorType switch
        {
            0 => bitDepth,
            2 => 24,
            3 => bitDepth,
            4 => 16,
            6 => 32,
            _ => throw new NotSupportedException($"Unsupported PNG color type '{colorType}'."),
        };
        var filterBytesPerPixel = Math.Max(1, checked((bitsPerPixel + 7) / 8));
        var stride = checked((width * bitsPerPixel + 7) / 8);

        idat.Position = 0;
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionMode.Decompress, leaveOpen: true)) zlib.CopyTo(inflated);
        var scanlines = inflated.ToArray();
        var expectedLength = checked(height * (stride + 1));
        if (scanlines.Length != expectedLength) throw new InvalidDataException("PNG scanline data length is invalid.");

        var decodedRows = new byte[checked(height * stride)];
        var previous = new byte[stride];
        var current = new byte[stride];
        var inputOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var filter = scanlines[inputOffset++];
            scanlines.AsSpan(inputOffset, stride).CopyTo(current);
            inputOffset += stride;
            Unfilter(current, previous, filterBytesPerPixel, filter);
            current.CopyTo(decodedRows.AsSpan(y * stride, stride));
            (previous, current) = (current, previous);
        }

        var rgba = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++)
        {
            var row = decodedRows.AsSpan(y * stride, stride);
            for (var x = 0; x < width; x++)
                DecodePixel(row, x, bitDepth, colorType, palette, transparency, rgba.AsSpan(((y * width) + x) * 4, 4));
        }
        return new ExportImage(new IntSize(width, height), rgba);
    }

    private static void ValidateFormat(byte colorType, byte bitDepth, byte[]? palette, byte[]? transparency)
    {
        switch (colorType)
        {
            case 0 when bitDepth == 8:
                if (transparency is { Length: not 2 }) throw new InvalidDataException("Grayscale PNG tRNS must contain one 16-bit sample.");
                return;
            case 2 when bitDepth == 8:
                if (transparency is { Length: not 6 }) throw new InvalidDataException("RGB PNG tRNS must contain three 16-bit samples.");
                return;
            case 3 when bitDepth is 1 or 2 or 4 or 8:
            {
                if (palette is null) throw new InvalidDataException("Indexed PNG is missing PLTE.");
                var paletteCount = palette.Length / 3;
                var maximumEntries = 1 << bitDepth;
                if (paletteCount > maximumEntries) throw new InvalidDataException("Indexed PNG palette exceeds bit-depth capacity.");
                if (transparency is { } alpha && alpha.Length > paletteCount)
                    throw new InvalidDataException("Indexed PNG tRNS has more entries than PLTE.");
                return;
            }
            case 4 when bitDepth == 8:
            case 6 when bitDepth == 8:
                if (transparency is not null) throw new InvalidDataException("PNG color types with alpha cannot contain tRNS.");
                return;
            default:
                throw new NotSupportedException($"PNG color type {colorType} with bit depth {bitDepth} is not supported.");
        }
    }

    private static void DecodePixel(
        ReadOnlySpan<byte> row,
        int x,
        byte bitDepth,
        byte colorType,
        byte[]? palette,
        byte[]? transparency,
        Span<byte> rgba)
    {
        switch (colorType)
        {
            case 0:
            {
                var gray = row[x];
                rgba[0] = gray;
                rgba[1] = gray;
                rgba[2] = gray;
                rgba[3] = transparency is not null && Read16(transparency, 0) == gray ? (byte)0 : byte.MaxValue;
                return;
            }
            case 2:
            {
                var offset = x * 3;
                var red = row[offset];
                var green = row[offset + 1];
                var blue = row[offset + 2];
                rgba[0] = red;
                rgba[1] = green;
                rgba[2] = blue;
                rgba[3] = transparency is not null &&
                          Read16(transparency, 0) == red &&
                          Read16(transparency, 2) == green &&
                          Read16(transparency, 4) == blue
                    ? (byte)0
                    : byte.MaxValue;
                return;
            }
            case 3:
            {
                var index = ReadPackedSample(row, x, bitDepth);
                var paletteOffset = index * 3;
                if (palette is null || paletteOffset + 2 >= palette.Length)
                    throw new InvalidDataException($"Indexed PNG pixel references missing palette index {index}.");
                rgba[0] = palette[paletteOffset];
                rgba[1] = palette[paletteOffset + 1];
                rgba[2] = palette[paletteOffset + 2];
                rgba[3] = transparency is not null && index < transparency.Length ? transparency[index] : byte.MaxValue;
                return;
            }
            case 4:
            {
                var offset = x * 2;
                rgba[0] = row[offset];
                rgba[1] = row[offset];
                rgba[2] = row[offset];
                rgba[3] = row[offset + 1];
                return;
            }
            case 6:
                row.Slice(x * 4, 4).CopyTo(rgba);
                return;
            default:
                throw new NotSupportedException($"Unsupported PNG color type '{colorType}'.");
        }
    }

    private static int ReadPackedSample(ReadOnlySpan<byte> row, int x, byte bitDepth)
    {
        if (bitDepth == 8) return row[x];
        var bitOffset = checked(x * bitDepth);
        var byteIndex = bitOffset / 8;
        var withinByte = bitOffset % 8;
        var shift = 8 - bitDepth - withinByte;
        var mask = (1 << bitDepth) - 1;
        return (row[byteIndex] >> shift) & mask;
    }

    private static ushort Read16(ReadOnlySpan<byte> values, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(values.Slice(offset, 2));

    private static void Unfilter(Span<byte> row, ReadOnlySpan<byte> previous, int bytesPerPixel, byte filter)
    {
        for (var index = 0; index < row.Length; index++)
        {
            var left = index >= bytesPerPixel ? row[index - bytesPerPixel] : (byte)0;
            var up = previous[index];
            var upperLeft = index >= bytesPerPixel ? previous[index - bytesPerPixel] : (byte)0;
            row[index] = filter switch
            {
                0 => row[index],
                1 => unchecked((byte)(row[index] + left)),
                2 => unchecked((byte)(row[index] + up)),
                3 => unchecked((byte)(row[index] + ((left + up) >> 1))),
                4 => unchecked((byte)(row[index] + Paeth(left, up, upperLeft))),
                _ => throw new InvalidDataException($"Unsupported PNG filter '{filter}'."),
            };
        }
    }

    private static byte Paeth(byte a, byte b, byte c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(header[..4], checked((uint)data.Length));
        type.CopyTo(header.Slice(4, 4));
        output.Write(header);
        output.Write(data);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32.Compute(type, data));
        output.Write(crc);
    }

    private static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        public static uint Compute(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
        {
            var crc = uint.MaxValue;
            foreach (var value in first) crc = Table[(crc ^ value) & 0xff] ^ (crc >> 8);
            foreach (var value in second) crc = Table[(crc ^ value) & 0xff] ^ (crc >> 8);
            return ~crc;
        }

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint index = 0; index < table.Length; index++)
            {
                var value = index;
                for (var bit = 0; bit < 8; bit++) value = (value & 1) != 0 ? 0xedb88320u ^ (value >> 1) : value >> 1;
                table[index] = value;
            }
            return table;
        }
    }
}
