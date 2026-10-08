using MyLovePixel.Application;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class CanvasDisplaySettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MyLovePixel-comfort-tests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_root, "display-settings.json");
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public void EntireSliderRangeIsLightNeutralLowContrastAndMonotonic()
    {
        byte previous = 0;
        for (var i = 0; i <= 100; i++)
        {
            var value = new CanvasDisplaySettings(i);
            Assert.InRange(value.CheckerLight.R, (byte)190, (byte)240);
            Assert.Equal(value.CheckerLight.R, value.CheckerLight.G);
            Assert.Equal(value.CheckerLight.R, value.CheckerLight.B);
            Assert.Equal(12, value.CheckerLight.R - value.CheckerDark.R);
            Assert.True(value.CheckerLight.R >= previous);
            previous = value.CheckerLight.R;
        }
        Assert.Equal(60, new CanvasDisplaySettings().BackgroundBrightness);
        Assert.Equal(12d, CanvasDisplaySettings.CheckerCellSize);
        Assert.Equal(750, CanvasDisplaySettings.TooltipDelayMilliseconds);
        Assert.Equal(0, CanvasDisplaySettings.TooltipBetweenShowDelayMilliseconds);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void InvalidBrightnessIsRejected(int value) => Assert.Throws<ArgumentOutOfRangeException>(() => new CanvasDisplaySettings(value));

    [Fact]
    public void DisplayBufferNeverMutatesSourceOrAlpha()
    {
        byte[] source = [101, 67, 33, 128, 255, 255, 255, 255, 10, 20, 30, 0];
        var original = source.ToArray();
        var copy = CanvasDisplaySettings.CopyRgbaForDisplay(source);
        Assert.Equal(source, copy);
        copy[0] = 0;
        Assert.Equal(original, source);
        var inverted = CanvasDisplaySettings.CopyRgbaForDisplay(source, true);
        Assert.Equal((byte)154, inverted[0]);
        Assert.Equal((byte)128, inverted[3]);
        Assert.Equal((byte)255, inverted[7]);
        Assert.Equal((byte)0, inverted[11]);
        Assert.Equal(original, source);
        Assert.Throws<ArgumentException>(() => CanvasDisplaySettings.CopyRgbaForDisplay(new byte[3]));
    }

    [Fact]
    public void BrightnessPersistsAndLastGoodVersionIsBackedUp()
    {
        var store = new CanvasDisplaySettingsStore(FilePath);
        Assert.Null(store.LoadError);
        store.Save(new(37));
        Assert.Equal(37, new CanvasDisplaySettingsStore(FilePath).Current.BackgroundBrightness);
        var old = File.ReadAllText(FilePath);
        store.Save(new(80));
        Assert.Equal(old, File.ReadAllText(FilePath + ".bak"));
        Assert.Equal(80, new CanvasDisplaySettingsStore(FilePath).Current.BackgroundBrightness);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"schemaVersion\":999,\"backgroundBrightness\":60}")]
    [InlineData("{\"schemaVersion\":1,\"backgroundBrightness\":101}")]
    public void MalformedOrFutureSettingsAreNotOverwritten(string original)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(FilePath, original);
        var store = new CanvasDisplaySettingsStore(FilePath);
        Assert.NotNull(store.LoadError);
        Assert.Throws<InvalidOperationException>(() => store.Save(new(10)));
        Assert.Equal(original, File.ReadAllText(FilePath));
    }

    [Fact]
    public void CorruptionAfterLoadCannotOverwriteDiskOrLastGoodMemory()
    {
        var store = new CanvasDisplaySettingsStore(FilePath);
        store.Save(new(30));
        File.WriteAllText(FilePath, "{\"schemaVersion\":7}");
        Assert.Throws<InvalidDataException>(() => store.Save(new(40)));
        Assert.Equal(30, store.Current.BackgroundBrightness);
        Assert.Equal("{\"schemaVersion\":7}", File.ReadAllText(FilePath));
    }

    [Fact]
    public void MissingPrimaryWithBackupDoesNotResetPreferences()
    {
        var store = new CanvasDisplaySettingsStore(FilePath);
        store.Save(new(20)); store.Save(new(30));
        File.Delete(FilePath);
        var next = new CanvasDisplaySettingsStore(FilePath);
        Assert.NotNull(next.LoadError);
        Assert.Throws<InvalidOperationException>(() => next.Save(new(60)));
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void BusyStoreDoesNotPublishAnUnsavedValue()
    {
        var store = new CanvasDisplaySettingsStore(FilePath);
        store.Save(new(22));
        using var lease = new FileStream(FilePath + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Throws<IOException>(() => store.Save(new(66)));
        Assert.Equal(22, store.Current.BackgroundBrightness);
    }

    [Fact]
    public void SavingRetainsUnknownFieldsAndUsesStablePerUserPath()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(FilePath, "{\"schemaVersion\":1,\"backgroundBrightness\":50,\"futureField\":{\"keep\":true}}");
        var store = new CanvasDisplaySettingsStore(FilePath);
        store.Save(new(25));
        Assert.Contains("futureField", File.ReadAllText(FilePath));
        Assert.Contains("keep", File.ReadAllText(FilePath));
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyLovePixel", "display-settings.json"), CanvasDisplaySettingsStore.DefaultFilePath);
    }
}
