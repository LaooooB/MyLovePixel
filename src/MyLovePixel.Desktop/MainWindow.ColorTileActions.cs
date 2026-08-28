using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private async Task QuantizeCurrentAsync()
    {
        var session = Current();
        if (session is null || session.GetCurrentSurfaceFormat() != PixelFormat.Rgba32) return;
        var result = await new QuantizeDialog().ShowDialog<QuantizeDialogResult?>(this);
        if (result is null) return;
        Safe(() =>
        {
            _selectedPalette = session.QuantizeCurrentSurface(result.MaxColors, result.ReserveTransparent);
            _selectedPaletteIndex = 0;
        });
        RefreshAll();
    }

    private async Task DitherCurrentAsync()
    {
        var session = Current();
        if (session is null || session.GetCurrentSurfaceFormat() != PixelFormat.Rgba32) return;
        var palettes = session.GetPaletteEditors();
        if (palettes.Count == 0) return;
        var result = await new DitherDialog(palettes).ShowDialog<DitherDialogResult?>(this);
        if (result is null) return;
        Safe(() => session.DitherCurrentSurface(result.PaletteId, result.Bayer4x4, result.Strength));
        _selectedPalette = result.PaletteId;
        _selectedPaletteIndex = 0;
        RefreshAll();
    }

    private async Task ShadeCurrentAsync()
    {
        var session = Current();
        if (session is null || session.GetCurrentSurfaceFormat() != PixelFormat.Indexed8) return;
        var snapshot = session.CaptureSnapshot();
        var cel = snapshot.Cels.FirstOrDefault(value => value.LayerId == session.CurrentLayerId && value.FrameId == session.CurrentFrameId);
        if (cel is null) return;
        var surface = snapshot.GetSurface(cel.SurfaceId);
        if (surface.PaletteId is not { } paletteId) return;
        var palette = session.GetPaletteEditors().First(value => value.Id == paletteId);
        var result = await new ShadeDialog(palette).ShowDialog<ShadeDialogResult?>(this);
        if (result is null) return;
        Safe(() => session.ShadeCurrentIndexedSurface(result.RampIndices, result.StepDelta));
        RefreshAll();
    }

    private void ConvertIndexedCurrentToRgba()
    {
        var session = Current();
        if (session is null || session.GetCurrentSurfaceFormat() != PixelFormat.Indexed8) return;
        Safe(session.ConvertCurrentIndexedSurfaceToRgba);
        RefreshAll();
    }

    private async Task AutoTileAsync(TilesetId tilesetId, TilemapId tilemapId)
    {
        var session = Current();
        if (session is null) return;
        var tiles = session.GetTiles(tilesetId, _selectedTile);
        if (tiles.Count == 0) return;
        var result = await new AutoTileDialog(tiles, _tileViewportX, _tileViewportY).ShowDialog<AutoTileDialogResult?>(this);
        if (result is null) return;
        Safe(() => session.ApplyAutoTile(tilemapId, result.Area, result.NeighborMode, result.Mappings, result.FallbackTileId));
        RefreshAll();
    }
}
