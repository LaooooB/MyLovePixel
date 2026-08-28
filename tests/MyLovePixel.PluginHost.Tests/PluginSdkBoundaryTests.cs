using MyLovePixel.Core.Document;
using MyLovePixel.PluginSdk;
using Xunit;

namespace MyLovePixel.PluginHost.Tests;

public sealed class PluginSdkBoundaryTests
{
    [Fact]
    public void PublicSdk_HasNoEditorInternalOrUiAssemblyReferences()
    {
        var references = typeof(PluginApi).Assembly.GetReferencedAssemblies()
            .Select(value => value.Name)
            .Where(value => value is not null)
            .ToArray();

        Assert.DoesNotContain("MyLovePixel.Core", references);
        Assert.DoesNotContain("MyLovePixel.Commands", references);
        Assert.DoesNotContain("MyLovePixel.PluginHost", references);
        Assert.DoesNotContain("Avalonia", references);
        Assert.DoesNotContain("SkiaSharp", references);
        Assert.Equal(new PluginApiVersion(1, 0), PluginApi.Current);
        Assert.Equal(new PluginApiVersion(1, 0), PluginApi.MinimumSupported);
    }

    [Fact]
    public void Core_DoesNotReverseDependOnPluginSdkOrHost()
    {
        var references = typeof(PixelDocument).Assembly.GetReferencedAssemblies()
            .Select(value => value.Name)
            .Where(value => value is not null)
            .ToArray();

        Assert.DoesNotContain("MyLovePixel.PluginSdk", references);
        Assert.DoesNotContain("MyLovePixel.PluginHost", references);
    }
}
