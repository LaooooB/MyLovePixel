using System.Text.Json;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class ColorLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mlpx-color-library", Guid.NewGuid().ToString("N"));
    private string PathName => Path.Combine(_root, "user-palette.json");
    private UserPaletteStore Open() => new(PathName);
    private static readonly Rgba32 Brown = new(101, 67, 33, 128);
    private static readonly Rgba32 Blue = new(0, 128, 255);

    [Fact]
    public void FoldersAndNamesSurviveReopening()
    {
        var s = Open(); var id = s.CreateFolder(" 场景色 "); s.Add(Brown, "木头阴影", id);
        var next = Open(); Assert.Equal("场景色", next.Folders.Single().Name);
        Assert.Equal(new SavedColor(Brown, "木头阴影", id), next.Swatches.Single());
        next.RenameFolder(id, "屋内"); Assert.Equal("屋内", Open().Folders.Single().Name);
    }
    [Fact]
    public void DeletingFolderUnfilesColorsAndDoesNotDeleteThem()
    {
        var s = Open(); var id = s.CreateFolder("Characters"); s.Add(Brown, "Skin", id); s.Add(Blue, "Eyes", id); s.Keep(Brown);
        s.DeleteFolder(id); var next = Open(); Assert.Empty(next.Folders); Assert.Equal(2, next.Swatches.Count);
        Assert.All(next.Swatches, c => Assert.Null(c.FolderId)); Assert.Equal(Brown, next.QuickColors.Single());
    }
    [Fact]
    public void SearchCombinesUnicodeHexFolderAndFolderFilter()
    {
        var s = Open(); var id = s.CreateFolder("Ocean"); s.Add(Blue, "海蓝", id); s.Add(Brown, "Wood");
        Assert.Equal(Blue, s.Query("海蓝").Single().Color);
        Assert.Equal(Blue, s.Query("0080ff").Single().Color);
        Assert.Equal(Blue, s.Query("oCeAn 海蓝").Single().Color);
        Assert.Empty(s.Query("Wood", id)); Assert.Equal(Brown, s.Query(unfiledOnly: true).Single().Color);
    }
    [Fact]
    public void MovingAndRenamingAreOneDurableUpdate()
    {
        var s = Open(); s.Add(Brown, "Wood"); var id = s.CreateFolder("Props");
        s.Update(Brown, "Deep wood", id); Assert.Equal(new SavedColor(Brown, "Deep wood", id), Open().Swatches.Single());
        s.Move(Brown, null); Assert.Null(Open().Swatches.Single().FolderId);
    }
    [Fact]
    public void TemporaryColorsSurviveRestartAndOtherWindowWrites()
    {
        var a = Open(); var b = Open(); a.Add(Brown, "Wood"); a.Keep(Brown); b.Keep(Blue); b.CreateFolder("Props"); a.Rename(Brown, "Brown");
        var next = Open(); Assert.Equal(new[] { Brown, Blue }, next.QuickColors);
        Assert.Single(next.Folders); Assert.Equal("Brown", next.Swatches.Single().Name);
        next.RemoveQuick(Brown); Assert.Equal(Blue, Open().QuickColors.Single()); Assert.Single(Open().Swatches);
    }
    [Fact]
    public void KeepingSameColorDoesNotDuplicateOrRenameIt()
    {
        var s = Open(); s.Add(Brown, "Wood"); Assert.True(s.Keep(Brown)); var before = File.ReadAllBytes(PathName);
        Assert.False(s.Keep(Brown)); Assert.Equal(before, File.ReadAllBytes(PathName)); Assert.Equal("Wood", s.Swatches.Single().Name);
    }
    [Fact]
    public void UnknownFolderDoesNotChangeData()
    {
        var s = Open(); s.Add(Brown, "Wood"); var before = File.ReadAllBytes(PathName);
        Assert.Throws<InvalidOperationException>(() => s.Move(Brown, "missing"));
        Assert.Equal(before, File.ReadAllBytes(PathName)); Assert.Null(s.Swatches.Single().FolderId);
    }
    [Theory]
    [InlineData("")] [InlineData("  ")] [InlineData("a\nb")]
    public void BadFolderNamesDoNotWrite(string name)
    { var s = Open(); Assert.Throws<ArgumentException>(() => s.CreateFolder(name)); Assert.False(File.Exists(PathName)); }
    [Fact]
    public void DuplicateFolderNameIsCaseInsensitive()
    { var s = Open(); s.CreateFolder("Characters"); Assert.Throws<ArgumentException>(() => s.CreateFolder("characters")); Assert.Single(Open().Folders); }
    [Fact]
    public void LargeLibrariesDoNotStopAtOld512Limit()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(PathName, JsonSerializer.Serialize(new { schemaVersion = 2, colors = Enumerable.Range(0, 1200)
            .Select(i => new { hex = HexColor.Format(new Rgba32((byte)i, (byte)(i >> 8), 0)), name = $"Paint {i}" }) }));
        var s = Open(); Assert.Null(s.LoadError); Assert.Equal(1200, s.Swatches.Count); s.Keep(Brown);
        Assert.Equal(1200, Open().Swatches.Count); Assert.Single(s.Query("1199"));
        Assert.True(File.Exists(PathName + ".v2.bak"));
    }
    [Fact]
    public void MigrationBackupIsNeverReplacedBySubsequentEdits()
    {
        Directory.CreateDirectory(_root); const string original = "{\"schemaVersion\":2,\"colors\":[{\"hex\":\"#65432180\",\"name\":\"old\"}]}";
        File.WriteAllText(PathName, original); var s = Open(); s.CreateFolder("First"); s.Keep(Blue); s.Rename(Brown, "renamed");
        Assert.Equal(original, File.ReadAllText(PathName + ".v2.bak"));
        Assert.Equal("renamed", Open().Swatches.Single().Name);
    }
    [Fact]
    public void CorruptionCannotBeOverwrittenByFolderOrQuickColorActions()
    {
        var s = Open(); s.Add(Brown, "Wood"); File.WriteAllText(PathName, "broken");
        Assert.ThrowsAny<JsonException>(() => s.Keep(Blue)); Assert.ThrowsAny<JsonException>(() => s.CreateFolder("Props"));
        Assert.Equal("broken", File.ReadAllText(PathName)); Assert.Single(s.Swatches);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
