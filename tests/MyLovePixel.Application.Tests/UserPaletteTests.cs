using System.Text.Json;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class UserPaletteTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MyLovePixel-palette-tests", Guid.NewGuid().ToString("N"));
    private string PalettePath => Path.Combine(_root, "preferences", "user-palette.json");

    [Theory]
    [InlineData("#654321", 101, 67, 33, 255)]
    [InlineData("654321", 101, 67, 33, 255)]
    [InlineData("  #aBcDeF  ", 171, 205, 239, 255)]
    [InlineData("#65432180", 101, 67, 33, 128)]
    [InlineData("#00000000", 0, 0, 0, 0)]
    [InlineData("FFFFFFFF", 255, 255, 255, 255)]
    public void HexInputPreservesRgbAndTrailingAlpha(string text, int r, int g, int b, int a)
    {
        Assert.True(HexColor.TryParse(text, out var color));
        Assert.Equal(new Rgba32((byte)r, (byte)g, (byte)b, (byte)a), color);
        Assert.True(HexColor.TryParse(HexColor.Format(color), out var roundTrip));
        Assert.Equal(color, roundTrip);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#123")]
    [InlineData("#GG0000")]
    [InlineData("#1234567")]
    [InlineData("#123456789")]
    [InlineData("##654321")]
    [InlineData("#65 321")]
    [InlineData("# 1 2 3")]
    [InlineData("#１２３４５６")]
    [InlineData("+654321")]
    [InlineData("0x654321")]
    [InlineData("red")]
    public void InvalidHexInputIsRejected(string? text)
    {
        Assert.False(HexColor.TryParse(text, out _));
    }

    [Fact]
    public void FormatIsCanonicalAndDoesNotLoseTransparency()
    {
        Assert.Equal("#654321", HexColor.Format(new Rgba32(101, 67, 33)));
        Assert.Equal("#65432180", HexColor.Format(new Rgba32(101, 67, 33, 128)));
    }

    [Fact]
    public void MissingPaletteStartsEmptyWithoutWritingFiles()
    {
        var store = new UserPaletteStore(PalettePath);
        Assert.Null(store.LoadError);
        Assert.Empty(store.Colors);
        Assert.False(File.Exists(PalettePath));
    }

    [Fact]
    public void SaveReloadPreservesOrderAndAlphaAndDoesNotExposeMutableState()
    {
        var colors = new[] { new Rgba32(101, 67, 33), new Rgba32(1, 2, 3, 64), Rgba32.Transparent };
        var store = new UserPaletteStore(PalettePath);
        foreach (var color in colors) Assert.True(store.Add(color));
        var reopened = new UserPaletteStore(PalettePath);
        Assert.Null(reopened.LoadError);
        Assert.Equal(colors, reopened.Colors.ToArray());
        Assert.True(Assert.IsAssignableFrom<IList<Rgba32>>(reopened.Colors).IsReadOnly);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(PalettePath)!, "*.tmp"));
    }

    [Fact]
    public void DuplicateDoesNotAddOrRewritePalette()
    {
        var store = new UserPaletteStore(PalettePath);
        var color = new Rgba32(101, 67, 33);
        Assert.True(store.Add(color));
        var before = File.ReadAllBytes(PalettePath);
        Assert.False(store.Add(color));
        Assert.Single(store.Colors);
        Assert.Equal(before, File.ReadAllBytes(PalettePath));
    }

    [Fact]
    public void RemovingLastColorPersistsAnEmptyPalette()
    {
        var store = new UserPaletteStore(PalettePath);
        var color = new Rgba32(101, 67, 33);
        store.Add(color);
        Assert.True(store.Remove(color));
        Assert.False(store.Remove(color));
        Assert.Empty(new UserPaletteStore(PalettePath).Colors);
    }

    [Fact]
    public void SeparateWindowsMergeAdditionsAndRemovalsWithoutLosingColors()
    {
        var first = new UserPaletteStore(PalettePath);
        var second = new UserPaletteStore(PalettePath);
        var brown = new Rgba32(101, 67, 33);
        var blue = new Rgba32(0, 64, 255);
        first.Add(brown);
        second.Add(blue);
        Assert.Equal(new[] { brown, blue }, new UserPaletteStore(PalettePath).Colors.ToArray());
        Assert.True(first.Remove(brown));
        Assert.Equal(new[] { blue }, new UserPaletteStore(PalettePath).Colors.ToArray());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":2,\"colors\":[\"#654321\"]}")]
    [InlineData("{\"schemaVersion\":1,\"colors\":[\"#654321\",\"invalid\"]}")]
    public void UnreadablePaletteIsReportedAndNeverOverwritten(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PalettePath)!);
        File.WriteAllText(PalettePath, content);
        var store = new UserPaletteStore(PalettePath);
        Assert.NotNull(store.LoadError);
        Assert.Empty(store.Colors);
        Assert.Throws<InvalidOperationException>(() => store.Add(new Rgba32(1, 2, 3)));
        Assert.Equal(content, File.ReadAllText(PalettePath));
    }

    [Fact]
    public void BusyFileDoesNotChangeThePersistedOrInMemoryPalette()
    {
        var store = new UserPaletteStore(PalettePath);
        var brown = new Rgba32(101, 67, 33);
        store.Add(brown);
        var before = File.ReadAllBytes(PalettePath);
        using var held = new FileStream(PalettePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Assert.Throws<IOException>(() => store.Add(new Rgba32(1, 2, 3)));
        Assert.Equal(new[] { brown }, store.Colors.ToArray());
        Assert.Equal(before, File.ReadAllBytes(PalettePath));
    }

    [Fact]
    public void CapacityFailureDoesNotRemoveExistingColors()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PalettePath)!);
        var colors = Enumerable.Range(0, UserPaletteStore.MaxColors)
            .Select(i => HexColor.Format(new Rgba32((byte)i, (byte)(i >> 8), 0))).ToArray();
        File.WriteAllText(PalettePath, JsonSerializer.Serialize(new { schemaVersion = 1, colors }));
        var store = new UserPaletteStore(PalettePath);
        Assert.Equal(UserPaletteStore.MaxColors, store.Colors.Count);
        var before = File.ReadAllBytes(PalettePath);
        Assert.Throws<InvalidOperationException>(() => store.Add(new Rgba32(1, 2, 3)));
        Assert.Equal(before, File.ReadAllBytes(PalettePath));
        Assert.Equal(UserPaletteStore.MaxColors, store.Colors.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
