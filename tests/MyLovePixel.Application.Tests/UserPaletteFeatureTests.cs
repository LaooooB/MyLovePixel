using MyLovePixel.Application;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class UserPaletteFeatureTests
{
    [Fact]
    public void PersonalPaletteStorageIsAvailableAtTheApplicationBoundary()
    {
        // Personal palette persistence belongs to workspace/application state,
        // not the pixel document or the Avalonia controls.
        var type = typeof(EditorWorkspace).Assembly.GetType("MyLovePixel.Application.UserPaletteStore");
        Assert.NotNull(type);
    }
}
