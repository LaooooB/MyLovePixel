using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class ColorLibraryStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MyLovePixel-library-tests", Guid.NewGuid().ToString("N"));
    private string LibraryPath => Path.Combine(_root, "color-library.json");
    private string LegacyPath => Path.Combine(_root, "user-palette.json");
    private ColorLibraryStore Open() => new(LibraryPath, LegacyPath);

    [Fact]
    public void MigratesEveryLegacyColorWithoutChangingTheOriginalFile()
    {
        var legacy = new UserPaletteStore(LegacyPath);
        for (var i = 0; i < UserPaletteStore.MaxColors; i++)
            legacy.Add(new Rgba32((byte)i, (byte)(i / 256), 33, (byte)(i % 251)));
        var original = File.ReadAllBytes(LegacyPath);
        var library = Open();
        Assert.Null(library.LoadError);
        Assert.Equal(legacy.Colors.Count, library.Colors.Count);
        Assert.Equal(legacy.Colors.ToArray(), library.Colors.Select(c => ColorLibraryStore.ParseColor(c.Hex)).ToArray());
        Assert.Equal(original, File.ReadAllBytes(LegacyPath));
        Assert.Equal(library.Colors.ToArray(), Open().Colors.ToArray());
    }

    [Fact]
    public void NamesFoldersAndTemporaryColorsSurviveRestart()
    {
        var store = Open();
        var folder = store.CreateFolder("角色肤色");
        var color = new Rgba32(101, 67, 33, 128);
        var id = store.SaveColor(color, "阴影", folder);
        store.AddTemporary(color);
        var reopened = Open();
        Assert.Null(reopened.LoadError);
        Assert.Equal("角色肤色", Assert.Single(reopened.Folders).Name);
        var saved = Assert.Single(reopened.Colors);
        Assert.Equal(id, saved.Id);
        Assert.Equal("阴影", saved.Name);
        Assert.Equal(folder, saved.FolderId);
        Assert.Equal(color, ColorLibraryStore.ParseColor(saved.Hex));
        Assert.Equal(color, ColorLibraryStore.ParseColor(Assert.Single(reopened.TemporaryColors)));
    }

    [Fact]
    public void RemovingAFolderUnfilesItsColorsWithoutDeletingThem()
    {
        var store = Open();
        var folder = store.CreateFolder("Terrain");
        var id = store.SaveColor(new Rgba32(1, 2, 3), "Grass", folder);
        store.DeleteFolder(folder);
        var saved = Assert.Single(Open().Colors);
        Assert.Equal(id, saved.Id);
        Assert.Null(saved.FolderId);
        Assert.Empty(store.Folders);
    }

    [Fact]
    public void FolderRenameAndColorMoveUseStableIds()
    {
        var store = Open();
        var a = store.CreateFolder("A");
        var b = store.CreateFolder("B");
        var id = store.SaveColor(new Rgba32(4, 5, 6), "Old", a);
        store.RenameFolder(b, "角色");
        store.UpdateColor(id, "New", b);
        var reopened = Open();
        Assert.Equal("New", Assert.Single(reopened.Colors).Name);
        Assert.Equal(b, Assert.Single(reopened.Colors).FolderId);
        Assert.Equal("角色", reopened.Folders.Single(f => f.Id == b).Name);
    }

    [Fact]
    public void SearchMatchesNameHexAndFolderWithoutCaseSensitivity()
    {
        var store = Open();
        var folder = store.CreateFolder("Terrain");
        store.SaveColor(new Rgba32(101, 67, 33), "深棕 Shadow", folder);
        store.SaveColor(new Rgba32(250, 250, 250), "White");
        Assert.Single(store.Search("654321"));
        Assert.Single(store.Search("shadow TERRAIN"));
        Assert.Single(store.Search("深棕"));
        Assert.Single(store.Search("", folder));
        Assert.Single(store.Search("", unfiledOnly: true));
        Assert.Empty(store.Search("unknown"));
    }

    [Fact]
    public void DuplicateSaveDoesNotUnexpectedlyRenameOrMoveAColor()
    {
        var store = Open();
        var color = new Rgba32(9, 8, 7);
        var id = store.SaveColor(color, "Keep");
        var folder = store.CreateFolder("Other");
        Assert.Equal(id, store.SaveColor(color, "Replace?", folder));
        Assert.Equal("Keep", Assert.Single(store.Colors).Name);
        Assert.Null(Assert.Single(store.Colors).FolderId);
    }

    [Fact]
    public void CorruptLegacyFileBlocksMigrationAndRemainsUntouched()
    {
        Directory.CreateDirectory(_root);
        const string bad = "{\"schemaVersion\":1,\"colors\":[\"bad HEX\"]}";
        File.WriteAllText(LegacyPath, bad);
        var store = Open();
        Assert.NotNull(store.LoadError);
        Assert.Throws<InvalidOperationException>(() => store.SaveColor(new Rgba32(1, 2, 3)));
        Assert.Equal(bad, File.ReadAllText(LegacyPath));
        Assert.False(File.Exists(LibraryPath));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":99,\"colors\":[],\"folders\":[],\"temporaryColors\":[]}")]
    [InlineData("{not json")]
    public void InvalidOrFutureFormatBlocksWritesWithoutReplacingTheFile(string bad)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(LibraryPath, bad);
        var store = Open();
        Assert.NotNull(store.LoadError);
        Assert.Throws<InvalidOperationException>(() => store.AddTemporary(new Rgba32(1, 2, 3)));
        Assert.Equal(bad, File.ReadAllText(LibraryPath));
    }

    [Fact]
    public void CorruptionAfterLoadingDoesNotReplaceDiskOrInMemorySnapshot()
    {
        var store = Open();
        store.SaveColor(new Rgba32(10, 20, 30));
        var snapshot = store.Colors.ToArray();
        File.WriteAllText(LibraryPath, "{bad");
        Assert.ThrowsAny<Exception>(() => store.SaveColor(new Rgba32(4, 5, 6)));
        Assert.Equal("{bad", File.ReadAllText(LibraryPath));
        Assert.Equal(snapshot, store.Colors.ToArray());
    }

    [Fact]
    public void EachReplacementRetainsThePreviousValidatedFileAsBackup()
    {
        var store = Open();
        store.SaveColor(new Rgba32(1, 2, 3));
        var previous = File.ReadAllBytes(LibraryPath);
        store.SaveColor(new Rgba32(4, 5, 6));
        Assert.Equal(previous, File.ReadAllBytes(store.BackupPath));
    }

    [Fact]
    public void TwoInstancesReReadBeforeChangingTheLibrary()
    {
        var first = Open();
        var second = Open();
        first.SaveColor(new Rgba32(1, 2, 3));
        second.SaveColor(new Rgba32(4, 5, 6));
        Assert.Equal(2, Open().Colors.Count);
    }

    [Fact]
    public void MissingPrimaryFileWithBackupDoesNotSilentlyCreateAnEmptyLibrary()
    {
        var store = Open();
        store.SaveColor(new Rgba32(1, 2, 3));
        store.SaveColor(new Rgba32(4, 5, 6));
        File.Delete(LibraryPath);
        var reopened = Open();
        Assert.NotNull(reopened.LoadError);
        Assert.False(File.Exists(LibraryPath));
        reopened.RestoreBackup();
        Assert.Null(reopened.LoadError);
        Assert.Single(reopened.Colors);
    }

    [Fact]
    public void ExplicitBackupRestorePreservesTheDamagedPrimaryForRecovery()
    {
        var store = Open();
        store.SaveColor(new Rgba32(1, 2, 3));
        store.SaveColor(new Rgba32(4, 5, 6));
        File.WriteAllText(LibraryPath, "DAMAGED");
        var reopened = Open();
        reopened.RestoreBackup();
        Assert.Single(reopened.Colors);
        var damaged = Assert.Single(Directory.GetFiles(_root, "color-library.json.damaged-*"));
        Assert.Equal("DAMAGED", File.ReadAllText(damaged));
        Assert.Single(Open().Colors);
    }

    [Fact]
    public void StaleRestoreRequestCannotOverwriteAPrimaryThatHasAlreadyBeenRepaired()
    {
        var first = Open();
        first.SaveColor(new Rgba32(1, 2, 3));
        first.SaveColor(new Rgba32(4, 5, 6));
        var valid = File.ReadAllBytes(LibraryPath);
        File.WriteAllText(LibraryPath, "DAMAGED");
        var failed = Open();
        Assert.NotNull(failed.LoadError);
        File.WriteAllBytes(LibraryPath, valid);
        Assert.Throws<InvalidOperationException>(() => failed.RestoreBackup());
        Assert.Equal(valid, File.ReadAllBytes(LibraryPath));
        Assert.Equal(2, Open().Colors.Count);
    }

    [Fact]
    public void TemporarySlotsAreNotReorderedEvictedOrClearedOnRestart()
    {
        var store = Open();
        var a = new Rgba32(1, 2, 3, 4);
        var b = new Rgba32(5, 6, 7, 8);
        Assert.True(store.AddTemporary(a));
        Assert.True(store.AddTemporary(b));
        Assert.False(store.AddTemporary(a));
        Assert.Equal(new[] { a, b }, Open().TemporaryColors.Select(ColorLibraryStore.ParseColor).ToArray());
        store.RemoveTemporary(HexColor.Format(a));
        Assert.Equal(b, ColorLibraryStore.ParseColor(Assert.Single(Open().TemporaryColors)));
        store.ClearTemporary();
        Assert.Empty(Open().TemporaryColors);
    }

    [Fact]
    public void InvalidFolderReferenceCannotBeSaved()
    {
        var store = Open();
        Assert.Throws<InvalidOperationException>(() => store.SaveColor(new Rgba32(1, 2, 3), folderId: Guid.NewGuid()));
        Assert.Empty(Open().Colors);
    }

    [Fact]
    public void DeletedColorsAreNotResurrectedFromTheUnchangedLegacyFile()
    {
        new UserPaletteStore(LegacyPath).Add(new Rgba32(1, 2, 3));
        var store = Open();
        store.RemoveColor(Assert.Single(store.Colors).Id);
        Assert.Empty(Open().Colors);
        Assert.Single(new UserPaletteStore(LegacyPath).Colors);
    }

    [Theory]
    [InlineData(0, 1, 1, 255, 0, 0)]
    [InlineData(120, 1, 1, 0, 255, 0)]
    [InlineData(240, 1, 1, 0, 0, 255)]
    [InlineData(359, 0, 1, 255, 255, 255)]
    [InlineData(0, 1, 0, 0, 0, 0)]
    public void PickerProducesExpectedRgb(double hue, double saturation, double value, int r, int g, int b)
    {
        Assert.Equal(new Rgba32((byte)r, (byte)g, (byte)b, 123), ColorPickerMath.FromHsv(hue, saturation, value, 123));
    }

    [Fact]
    public void PickerRoundTripsRgbAndAlpha()
    {
        for (var r = 0; r < 256; r += 17)
        for (var g = 0; g < 256; g += 17)
        for (var b = 0; b < 256; b += 17)
        {
            var color = new Rgba32((byte)r, (byte)g, (byte)b, 100);
            var hsv = ColorPickerMath.ToHsv(color);
            Assert.Equal(color, ColorPickerMath.FromHsv(hsv.Hue, hsv.Saturation, hsv.Value, color.A));
        }
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
