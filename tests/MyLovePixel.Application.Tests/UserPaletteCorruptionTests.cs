using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class UserPaletteCorruptionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CorruptionAfterLoadingCannotOverwriteDiskOrTheLastGoodSnapshot(bool remove)
    {
        var root = Path.Combine(Path.GetTempPath(), "MyLovePixel-palette-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "palette.json");
        try
        {
            var store = new UserPaletteStore(path);
            var brown = new Rgba32(101, 67, 33);
            store.Add(brown);
            const string damaged = "{\"schemaVersion\":1,\"colors\":[\"invalid\"]}";
            File.WriteAllText(path, damaged);
            if (remove)
                Assert.Throws<InvalidDataException>(() => store.Remove(brown));
            else
                Assert.Throws<InvalidDataException>(() => store.Add(new Rgba32(1, 2, 3)));
            Assert.Equal(damaged, File.ReadAllText(path));
            Assert.Equal(new[] { brown }, store.Colors.ToArray());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
