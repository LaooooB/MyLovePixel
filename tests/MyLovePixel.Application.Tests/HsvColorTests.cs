using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class HsvColorTests
{
    [Fact]
    public void PickerMathRoundTripsRgbAndInvisibleAlphaWithoutDrift()
    {
        var type = typeof(UserPaletteStore).Assembly.GetType("MyLovePixel.Application.HsvColor");
        Assert.NotNull(type);
        var method = type.GetMethod("FromRgba");
        Assert.NotNull(method);
        for (var i = 0; i < 2048; i++)
        {
            var original = new Rgba32((byte)(i * 11), (byte)(i * 19), (byte)(i * 47), (byte)i);
            dynamic hsv = method.Invoke(null, new object[] { original })!;
            Assert.Equal(original, (Rgba32)hsv.ToRgba());
        }
    }

    [Fact]
    public void PickerCornersAndHueWrapProduceExpectedColors()
    {
        var type = typeof(UserPaletteStore).Assembly.GetType("MyLovePixel.Application.HsvColor");
        Assert.NotNull(type);
        Rgba32 Convert(double h, double s, double v) => (Rgba32)((dynamic)Activator.CreateInstance(type, h, s, v, (byte)80)!).ToRgba();
        Assert.Equal(new Rgba32(255, 0, 0, 80), Convert(360, 1, 1));
        Assert.Equal(new Rgba32(0, 255, 0, 80), Convert(120, 1, 1));
        Assert.Equal(new Rgba32(0, 0, 255, 80), Convert(-120, 1, 1));
        Assert.Equal(new Rgba32(255, 255, 255, 80), Convert(100, 0, 1));
        Assert.Equal(new Rgba32(0, 0, 0, 80), Convert(100, 1, 0));
    }
}
