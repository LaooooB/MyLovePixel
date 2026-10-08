using System.Text.Json;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class ColorLibraryOrganizationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "MyLovePixel-library", Guid.NewGuid().ToString("N"), "user-palette.json");
    private static readonly Rgba32 Brown = new(101, 67, 33);
    private static readonly Rgba32 Alpha = new(1, 2, 3, 0);

    [Fact]
    public void FolderMovesAndNamesSurviveRestartAndDeletingFolderKeepsColors()
    {
        Assert.NotNull(typeof(UserPaletteStore).GetMethod("CreateFolder"));
        dynamic store = new UserPaletteStore(_path);
        string folder = store.CreateFolder("  Skin  ");
        Assert.True((bool)store.Add(Brown, folder, "Warm brown"));
        store.Add(Alpha);
        Assert.Equal(1, (int)store.Query("WARM", folder, false).Count);
        Assert.Equal(1, (int)store.Query("6543", null, false).Count);
        Assert.Equal(1, (int)store.Query("skin", null, false).Count);
        store.MoveColor(Alpha, folder);
        store.RenameFolder(folder, "人物");
        store.RenameColor(Alpha, "透明色");
        dynamic reopened = new UserPaletteStore(_path);
        Assert.Equal(2, (int)reopened.Query("人物", null, false).Count);
        Assert.Equal(1, (int)reopened.Query("透明", folder, false).Count);
        reopened.DeleteFolder(folder);
        dynamic final = new UserPaletteStore(_path);
        Assert.Equal(2, (int)final.Query("", null, true).Count);
        Assert.Equal(0, (int)final.Folders.Count);
        Assert.Equal(new[] { Brown, Alpha }, ((UserPaletteStore)final).Colors.ToArray());
    }

    [Fact]
    public void DuplicateFoldersAndInvalidMovesNeverRewriteData()
    {
        Assert.NotNull(typeof(UserPaletteStore).GetMethod("CreateFolder"));
        dynamic store = new UserPaletteStore(_path);
        string folder = store.CreateFolder("Skin");
        store.Add(Brown, folder, "Brown");
        var before = File.ReadAllBytes(_path);
        Assert.Throws<ArgumentException>(() => { store.CreateFolder("skin"); });
        Assert.Throws<ArgumentException>(() => { store.CreateFolder("   "); });
        Assert.Throws<ArgumentException>(() => { store.MoveColor(Brown, "missing-folder"); });
        Assert.Equal(before, File.ReadAllBytes(_path));
    }

    [Fact]
    public void TemporarySwatchesKeepAlphaOrderAndSurviveOtherWindowWrites()
    {
        Assert.NotNull(typeof(UserPaletteStore).GetMethod("AddTemporary"));
        dynamic first = new UserPaletteStore(_path);
        dynamic second = new UserPaletteStore(_path);
        Assert.True((bool)first.AddTemporary(Brown));
        Assert.True((bool)second.AddTemporary(Alpha));
        Assert.False((bool)first.AddTemporary(Brown));
        first.Add(Brown);
        dynamic reopened = new UserPaletteStore(_path);
        Assert.Equal(Brown, (Rgba32)reopened.TemporaryColors[0]);
        Assert.Equal(Alpha, (Rgba32)reopened.TemporaryColors[1]);
        reopened.RemoveTemporary(Brown);
        Assert.Equal(new[] { Alpha }, ((IEnumerable<Rgba32>)((dynamic)new UserPaletteStore(_path)).TemporaryColors).ToArray());
        reopened.ClearTemporary();
        Assert.Empty((IEnumerable<Rgba32>)((dynamic)new UserPaletteStore(_path)).TemporaryColors);
        Assert.Equal(new[] { Brown }, new UserPaletteStore(_path).Colors.ToArray());
    }

    [Fact]
    public void TwoWindowsMergeFolderEditsWithoutResurrectingDeletedFolder()
    {
        Assert.NotNull(typeof(UserPaletteStore).GetMethod("CreateFolder"));
        dynamic first = new UserPaletteStore(_path);
        dynamic second = new UserPaletteStore(_path);
        string folder = first.CreateFolder("Palette");
        second.Add(Brown, folder, "Brown");
        first.AddTemporary(Alpha);
        first.DeleteFolder(folder);
        Assert.Throws<ArgumentException>(() => { second.Add(Alpha, folder, "Invalid"); });
        Assert.Equal(new[] { Brown }, new UserPaletteStore(_path).Colors.ToArray());
        Assert.Single((IEnumerable<Rgba32>)((dynamic)new UserPaletteStore(_path)).TemporaryColors);
    }

    [Fact]
    public void InvalidV2MetadataIsNeverOverwritten()
    {
        Assert.NotNull(typeof(UserPaletteStore).GetMethod("CreateFolder"));
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(new { schemaVersion = 2, entries = new[] { new { hex = "#654321", name = "Brown", folderId = "missing" } }, folders = Array.Empty<object>(), temporaryColors = Array.Empty<string>() });
        File.WriteAllText(_path, json);
        var store = new UserPaletteStore(_path);
        Assert.NotNull(store.LoadError);
        Assert.Throws<InvalidOperationException>(() => store.Add(Alpha));
        Assert.Equal(json, File.ReadAllText(_path));
    }

    [Fact]
    public void EditingNameAndFolderIsOneAtomicOperation()
    {
        Assert.NotNull(typeof(UserPaletteStore).GetMethod("UpdateColor"));
        dynamic store = new UserPaletteStore(_path);
        store.Add(Brown);
        var before = File.ReadAllBytes(_path);
        Assert.Throws<ArgumentException>(() => { store.UpdateColor(Brown, "New name", "missing"); });
        Assert.Equal(before, File.ReadAllBytes(_path));
        string folder = store.CreateFolder("Skin");
        store.UpdateColor(Brown, "Warm brown", folder);
        Assert.Equal(1, (int)store.Query("Warm brown", folder, false).Count);
    }

    public void Dispose() { if (Directory.Exists(Path.GetDirectoryName(_path))) Directory.Delete(Path.GetDirectoryName(_path)!, true); }
}
