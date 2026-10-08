using MyLovePixel.Application;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class CanvasComfortContractTests
{
    [Fact]
    public void CanvasComfortHasAnApplicationOwnedDisplayOnlySettingsContract()
    {
        var type = typeof(EditorWorkspace).Assembly.GetType("MyLovePixel.Application.CanvasDisplaySettings");
        Assert.NotNull(type);
        Assert.NotNull(type.GetProperty("BackgroundBrightness"));
        Assert.NotNull(type.GetProperty("CheckerLight"));
        Assert.NotNull(type.GetProperty("CheckerDark"));
        Assert.NotNull(type.GetMethod("CopyRgbaForDisplay"));
    }
}
