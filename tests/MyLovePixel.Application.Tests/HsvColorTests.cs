using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class HsvColorTests
{
    [Theory]
    [InlineData(0, 255, 0, 0)] [InlineData(120, 0, 255, 0)] [InlineData(240, 0, 0, 255)]
    [InlineData(60, 255, 255, 0)] [InlineData(180, 0, 255, 255)] [InlineData(300, 255, 0, 255)]
    public void HueAnchorsAreExact(double hue, byte r, byte g, byte b)
        => Assert.Equal(new Rgba32(r, g, b, 128), new HsvColor(hue, 1, 1).ToColor(128));
    [Fact]
    public void StandardColorRoundTripsDoNotLoseAlphaOrRgb()
    {
        var random = new Random(37);
        for (var i = 0; i < 5000; i++)
        {
            var color = new Rgba32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
            Assert.Equal(color, HsvColor.FromColor(color).ToColor(color.A));
        }
    }
    [Fact]
    public void SquareCornersAreWhiteHueAndBlack()
    {
        Assert.Equal(new Rgba32(255, 255, 255), new HsvColor(120, 0, 1).ToColor());
        Assert.Equal(new Rgba32(0, 255, 0), new HsvColor(120, 1, 1).ToColor());
        Assert.Equal(new Rgba32(0, 0, 0), new HsvColor(120, 1, 0).ToColor());
    }
}
