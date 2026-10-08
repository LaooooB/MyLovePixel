using System.Text.Json;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using Xunit;

namespace MyLovePixel.Application.Tests;

public sealed class PaletteMigrationV3Tests
{
    [Fact]
    public void NamedPaletteUpgradePreservesEveryColorAndBacksUpOriginalBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "mlpx-migration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "user-palette.json");
        var legacy = JsonSerializer.Serialize(new { schemaVersion = 2, colors = Enumerable.Range(0, 512)
            .Select(i => new { hex = HexColor.Format(new Rgba32((byte)i, (byte)(i >> 8), 0, (byte)(i % 255 + 1))), name = "已保存颜色 " + i }) });
        File.WriteAllText(path, legacy);
        try
        {
            var store = new UserPaletteStore(path);
            var original = store.Swatches.ToArray();
            Assert.Equal(512, original.Length);
            Assert.Equal(legacy, File.ReadAllText(path));
            store.Rename(original[0].Color, "更新名称");
            Assert.True(File.Exists(path + ".v2.bak"), "An update must back up the existing named palette before migration.");
            Assert.Equal(legacy, File.ReadAllText(path + ".v2.bak"));
            var reopened = new UserPaletteStore(path);
            Assert.Equal(512, reopened.Swatches.Count);
            Assert.Equal(original.Skip(1), reopened.Swatches.Skip(1));
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(3, json.RootElement.GetProperty("schemaVersion").GetInt32());
        }
        finally { Directory.Delete(root, true); }
    }
}
