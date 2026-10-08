using System.Text.Json;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class ColorLibraryUpgradeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MyLovePixel-upgrade-tests", Guid.NewGuid().ToString("N"));
    private string PalettePath => Path.Combine(_root, "user-palette.json");

    [Fact]
    public void LegacyUpgradePreservesEveryColorAndByteExactOriginal()
    {
        Directory.CreateDirectory(_root);
        const string original = "{\n  \"schemaVersion\": 1, \"colors\": [\"#654321\", \"#01020340\", \"#00000000\"]\n}";
        File.WriteAllText(PalettePath, original);
        var bytes = File.ReadAllBytes(PalettePath);
        var store = new UserPaletteStore(PalettePath);
        Assert.Null(store.LoadError);
        Assert.Equal(bytes, File.ReadAllBytes(PalettePath));
        Assert.True(store.Add(new Rgba32(255, 0, 0)));
        Assert.True(File.Exists(PalettePath + ".pre-v2.bak"));
        Assert.Equal(bytes, File.ReadAllBytes(PalettePath + ".pre-v2.bak"));
        using var saved = JsonDocument.Parse(File.ReadAllText(PalettePath));
        Assert.Equal(2, saved.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(new[] { new Rgba32(101, 67, 33), new Rgba32(1, 2, 3, 64), Rgba32.Transparent, new Rgba32(255, 0, 0) },
            new UserPaletteStore(PalettePath).Colors.ToArray());
    }

    [Fact]
    public void LibraryCanGrowBeyondThePrevious512ColorLimit()
    {
        Directory.CreateDirectory(_root);
        var colors = Enumerable.Range(0, 600)
            .Select(i => HexColor.Format(new Rgba32((byte)i, (byte)(i >> 8), 1))).ToArray();
        File.WriteAllText(PalettePath, JsonSerializer.Serialize(new { schemaVersion = 1, colors }));
        var store = new UserPaletteStore(PalettePath);
        Assert.Null(store.LoadError);
        Assert.Equal(600, store.Colors.Count);
        Assert.True(store.Add(new Rgba32(9, 9, 9, 80)));
        Assert.Equal(601, new UserPaletteStore(PalettePath).Colors.Count);
    }

    [Fact]
    public void EveryReplacementKeepsThePreviousValidGeneration()
    {
        var store = new UserPaletteStore(PalettePath);
        store.Add(new Rgba32(1, 2, 3));
        var first = File.ReadAllBytes(PalettePath);
        store.Add(new Rgba32(4, 5, 6));
        Assert.True(File.Exists(PalettePath + ".bak"));
        Assert.Equal(first, File.ReadAllBytes(PalettePath + ".bak"));
        Assert.Single(new UserPaletteStore(PalettePath + ".bak").Colors);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
