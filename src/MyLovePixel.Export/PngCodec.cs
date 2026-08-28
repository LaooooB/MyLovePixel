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
            var actualCrc = Crc32.Compute(type, data);
            if (expectedCrc != actualCrc) throw new InvalidDataException("PNG chunk CRC mismatch.");

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
        if (bitDepth != 8) throw new NotSupportedException("Only 8-bit PNG images are supported.");
        if (colorType is not (2 or 6)) throw new NotSupportedException("Only RGB and RGBA PNG images are supported.");
        if (interlace != 0) throw new NotSupportedException("Interlaced PNG images are not supported.");

        idat.Position = 0;
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionMode.Decompress, leaveOpen: true)) zlib.CopyTo(inflated);
        var scanlines = inflated.ToArray();
        var channels = colorType == 6 ? 4 : 3;
        var stride = checked(width * channels);
        var expectedLength = checked(height * (stride + 1));
        if (scanlines.Length != expectedLength) throw new InvalidDataException("PNG scanline data length is invalid.");

        var decoded = new byte[checked(width * height * channels)];
        var previous = new byte[stride];
        var current = new byte[stride];
        var inputOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var filter = scanlines[inputOffset++];
            scanlines.AsSpan(inputOffset, stride).CopyTo(current);
            inputOffset += stride;
            Unfilter(current, previous, channels, filter);
            current.CopyTo(decoded.AsSpan(y * stride, stride));
            (previous, current) = (current, previous);
        }

        var rgba = new byte[checked(width * height * 4)];
        if (channels == 4)
        {
            decoded.CopyTo(rgba, 0);
        }
        else
        {
            for (var pixel = 0; pixel < width * height; pixel++)
            {
                var source = pixel * 3;
                var target = pixel * 4;
                rgba[target] = decoded[source];
                rgba[target + 1] = decoded[source + 1];
                rgba[target + 2] = decoded[source + 2];
                rgba[target + 3] = byte.MaxValue;
            }
        }
        return new ExportImage(new IntSize(width, height), rgba);
    }

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
