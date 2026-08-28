using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Export.Tests;

public sealed class PngCodecTests
{
    [Fact]
    public void Decode_OneBitIndexedPaletteWithTransparency_ExpandsToRgba()
    {
        byte[] png =
        [
            137, 80, 78, 71, 13, 10, 26, 10,
            0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 2, 0, 0, 0, 1, 1, 3, 0, 0, 0, 206, 236, 237, 201,
            0, 0, 0, 6, 80, 76, 84, 69, 255, 0, 0, 0, 255, 0, 210, 135, 239, 113,
            0, 0, 0, 2, 116, 82, 78, 83, 255, 128, 8, 15, 179, 106,
            0, 0, 0, 10, 73, 68, 65, 84, 120, 156, 99, 112, 0, 0, 0, 66, 0, 65, 41, 55, 244, 239,
            0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130,
        ];

        var image = PngCodec.Decode(png);

        Assert.Equal(new IntSize(2, 1), image.Size);
        Assert.Equal(new Rgba32(255, 0, 0, 255), image.GetPixel(0, 0));
        Assert.Equal(new Rgba32(0, 255, 0, 128), image.GetPixel(1, 0));
    }
}
