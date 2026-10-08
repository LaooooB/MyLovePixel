using System.Text.Json;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class NamedPaletteTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MyLovePixel-named-palette", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_root, "user-palette.json");
    private static readonly Rgba32 Brown = new(101, 67, 33);
    private static readonly Rgba32 Blue = new(30, 100, 200, 128);
    private UserPaletteStore Open() => new(FilePath);

    [Theory]
    [InlineData("#654321", 101, 67, 33, 255)]
    [InlineData(" 654321 ", 101, 67, 33, 255)]
    [InlineData("#AbCdEf80", 171, 205, 239, 128)]
    [InlineData("#00000000", 0, 0, 0, 0)]
    public void HexInputAndFormattingRoundTrip(string input, int r, int g, int b, int a)
    {
        Assert.True(HexColor.TryParse(input, out var color));
        Assert.Equal(new Rgba32((byte)r, (byte)g, (byte)b, (byte)a), color);
        Assert.True(HexColor.TryParse(HexColor.Format(color), out var again));
        Assert.Equal(color, again);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("#123")] [InlineData("#GG4433")]
    [InlineData("# 1 2 3")] [InlineData("#1234567")] [InlineData("0x123456")]
    public void InvalidHexRejected(string? input) => Assert.False(HexColor.TryParse(input, out _));

    [Fact]
    public void NewPaletteDoesNotWriteUntilSaved()
    { var store = Open(); Assert.Empty(store.Swatches); Assert.Null(store.LoadError); Assert.False(File.Exists(FilePath)); }

    [Fact]
    public void UnicodeNamesOrderAndAlphaSurviveRestart()
    {
        var store = Open(); store.Add(Brown, "  木头阴影  "); store.Add(Blue, "Water 💧");
        var next = Open();
        Assert.Equal(new[] { new SavedColor(Brown, "木头阴影"), new SavedColor(Blue, "Water 💧") }, next.Swatches.ToArray());
        Assert.True(Assert.IsAssignableFrom<IList<SavedColor>>(next.Swatches).IsReadOnly);
    }

    [Fact]
    public void RenameKeepsColorOrderAndPersists()
    {
        var store = Open(); store.Add(Brown, "Wood"); store.Add(Blue, "Water");
        Assert.True(store.Rename(Brown, "Deep wood"));
        Assert.Equal(new SavedColor(Brown, "Deep wood"), Open().Swatches[0]);
        Assert.Equal(Blue, Open().Swatches[1].Color);
    }

    [Fact]
    public void DuplicateColorDoesNotSilentlyRenameOrRewrite()
    {
        var store = Open(); store.Add(Brown, "Wood"); var before = File.ReadAllBytes(FilePath);
        Assert.False(store.Add(Brown, "Other name"));
        Assert.Equal("Wood", store.Swatches.Single().Name);
        Assert.Equal(before, File.ReadAllBytes(FilePath));
    }

    [Fact]
    public void BlankNamesHaveVisibleHexFallback()
    { var store = Open(); store.Add(Brown, "  "); Assert.Equal("#654321", Open().Swatches.Single().Name); }

    [Fact]
    public void DeleteLastEntryPersistsEmptyPalette()
    { var store = Open(); store.Add(Brown, "Wood"); Assert.True(store.Remove(Brown)); Assert.Empty(Open().Swatches); Assert.False(store.Remove(Brown)); }

    [Fact]
    public void LegacyColorsLoadWithoutWriteThenMigrateWithBackup()
    {
        Directory.CreateDirectory(_root);
        const string legacy = "{\"schemaVersion\":1,\"colors\":[\"#654321\",\"#1E64C880\"]}";
        File.WriteAllText(FilePath, legacy); var store = Open();
        Assert.Equal("#654321", store.Swatches[0].Name); Assert.Equal(Blue, store.Swatches[1].Color);
        Assert.Equal(legacy, File.ReadAllText(FilePath));
        store.Rename(Brown, "木头");
        Assert.Equal(legacy, File.ReadAllText(FilePath + ".v1.bak"));
        Assert.Equal("木头", Open().Swatches[0].Name);
        using var json = JsonDocument.Parse(File.ReadAllText(FilePath)); Assert.Equal(3, json.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void SeparateWindowsKeepEachOthersChanges()
    {
        var a = Open(); var b = Open(); a.Add(Brown, "Wood"); b.Add(Blue, "Water");
        a.Rename(Brown, "Dark wood"); b.Remove(Blue);
        Assert.Equal(new SavedColor(Brown, "Dark wood"), Open().Swatches.Single());
    }

    [Theory]
    [InlineData("not json")] [InlineData("null")] [InlineData("{}")] [InlineData("{\"schemaVersion\":99,\"colors\":[]}")]
    [InlineData("{\"schemaVersion\":2,\"colors\":[{\"hex\":\"#654321\",\"name\":12}]}")]
    public void CorruptOrNewerFilesArePreserved(string content)
    {
        Directory.CreateDirectory(_root); File.WriteAllText(FilePath, content);
        var store = Open(); Assert.NotNull(store.LoadError); Assert.Empty(store.Swatches);
        Assert.ThrowsAny<Exception>(() => store.Add(Brown, "Wood")); Assert.Equal(content, File.ReadAllText(FilePath));
    }

    [Fact]
    public void CorruptionAfterLoadDoesNotPublishOrOverwrite()
    {
        var store = Open(); store.Add(Brown, "Wood"); File.WriteAllText(FilePath, "broken");
        Assert.ThrowsAny<Exception>(() => store.Rename(Brown, "Changed"));
        Assert.Equal("Wood", store.Swatches.Single().Name); Assert.Equal("broken", File.ReadAllText(FilePath));
    }

    [Theory]
    [InlineData("line\nbreak")] [InlineData("tab\tname")]
    public void InvalidNameCannotModifyPalette(string name)
    { var store = Open(); Assert.Throws<ArgumentException>(() => store.Add(Brown, name)); Assert.False(File.Exists(FilePath)); }

    [Fact]
    public void NameLengthLimitAndCapacityAreEnforced()
    {
        var store = Open(); Assert.Throws<ArgumentException>(() => store.Add(Brown, new string('a', 65)));
        Directory.CreateDirectory(_root);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new { schemaVersion = 2, colors = Enumerable.Range(0, UserPaletteStore.MaxColors).Select(i => new { hex = HexColor.Format(new Rgba32((byte)i, (byte)(i >> 8), 0)), name = "Color" + i }) }));
        store = Open(); Assert.Equal(UserPaletteStore.MaxColors, store.Swatches.Count);
        Assert.Throws<InvalidOperationException>(() => store.Add(Brown, "Full"));
        Assert.Equal(UserPaletteStore.MaxColors, Open().Swatches.Count);
    }

    [Fact]
    public void BusyStorageCannotPublishChanges()
    {
        var store = Open(); store.Add(Brown, "Wood");
        using var held = new FileStream(FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Assert.Throws<IOException>(() => store.Add(Blue, "Water")); Assert.Single(store.Swatches);
        Assert.Equal(new SavedColor(Brown, "Wood"), Open().Swatches.Single());
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
