using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using MyLovePixel.Application;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Export;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private Control BuildPluginExtensionControls(DocumentSession? session)
    {
        var root = new StackPanel { Spacing = 6 };

        if (_plugins.Commands.Count > 0)
        {
            var commands = new StackPanel { Spacing = 5 };
            foreach (var command in _plugins.Commands)
            {
                var item = command;
                var run = TextIconButton("▶", "Run", $"Run {item.DisplayName}", () => RunPluginCommand(item.Id));
                run.IsEnabled = session is not null;
                commands.Children.Add(ListRow(item.DisplayName, run));
            }
            root.Children.Add(Expander("Commands", commands));
        }

        if (_plugins.Importers.Count > 0)
        {
            var importers = new StackPanel { Spacing = 5 };
            foreach (var importer in _plugins.Importers)
            {
                var item = importer;
                importers.Children.Add(ListRow(item.DisplayName,
                    TextIconButton("", "Import…", $"Import with {item.DisplayName}", async () => await ImportWithPluginAsync(item))));
            }
            root.Children.Add(Expander("Importers", importers));
        }

        var pluginExporters = _plugins.Exporters.Where(value => value.Id != BuiltinExporterIds.GameAssets).ToArray();
        if (pluginExporters.Length > 0)
        {
            var exporters = new StackPanel { Spacing = 5 };
            foreach (var exporter in pluginExporters)
            {
                var item = exporter;
                var export = TextIconButton("", "Export…", $"Export with {item.DisplayName}", async () => await ExportWithPluginAsync(item));
                export.IsEnabled = session is not null;
                exporters.Children.Add(ListRow(item.DisplayName, export));
            }
            root.Children.Add(Expander("Exporters", exporters));
        }

        if (_plugins.PaletteAlgorithms.Count > 0)
        {
            var algorithms = new StackPanel { Spacing = 5 };
            foreach (var algorithm in _plugins.PaletteAlgorithms)
            {
                var item = algorithm;
                var apply = TextIconButton("", "Apply", $"Apply {item.DisplayName} to selected palette", () => ApplyPluginPaletteAlgorithm(item.Id));
                apply.IsEnabled = session is not null;
                algorithms.Children.Add(ListRow(item.DisplayName, apply));
            }
            root.Children.Add(Expander("Palette Algorithms", algorithms));
        }

        if (_plugins.DitherAlgorithms.Count > 0)
        {
            var algorithms = new StackPanel { Spacing = 5 };
            foreach (var algorithm in _plugins.DitherAlgorithms)
            {
                var item = algorithm;
                var apply = TextIconButton("", "Apply", $"Apply {item.DisplayName} using selected palette", () => ApplyPluginDither(item.Id));
                apply.IsEnabled = session is not null;
                algorithms.Children.Add(ListRow(item.DisplayName, apply));
            }
            root.Children.Add(Expander("Dither Algorithms", algorithms));
        }

        if (_plugins.AutoTileRules.Count > 0)
        {
            var rules = new StackPanel { Spacing = 5 };
            foreach (var rule in _plugins.AutoTileRules)
            {
                var item = rule;
                var apply = TextIconButton("", "Apply to Viewport", $"Apply {item.DisplayName} to visible 8×8 tile viewport", () => ApplyPluginAutoTile(item.Id));
                apply.IsEnabled = session is not null;
                rules.Children.Add(ListRow(item.DisplayName, apply));
            }
            root.Children.Add(Expander("AutoTile Rules", rules));
        }

        return root;
    }

    private void RunPluginCommand(string commandId)
    {
        var session = Current();
        if (session is null) return;
        try
        {
            var result = _plugins.ExecuteCommand(session, commandId);
            if (!result.Succeeded) SetError(result.Error ?? result.Message ?? "Plugin command failed.");
            else if (!string.IsNullOrWhiteSpace(result.Message)) _status.Text = result.Message;
        }
        catch (Exception ex) { SetError(ex.Message); }
        RefreshAll();
    }

    private async Task ImportWithPluginAsync(PluginExtensionPresentation importer)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Import · {importer.DisplayName}",
            AllowMultiple = false,
        });
        if (files.Count == 0) return;
        try
        {
            _plugins.ImportFile(importer.Id, files[0].Path.LocalPath);
            _selectionMode = false;
        }
        catch (Exception ex) { SetError(ex.Message); }
        RefreshAll();
    }

    private async Task ExportWithPluginAsync(PluginExtensionPresentation exporter)
    {
        var session = Current();
        if (session is null) return;
        var preset = await new ExportDialog().ShowDialog<ExportPreset?>(this);
        if (preset is null) return;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = $"Export · {exporter.DisplayName}",
            AllowMultiple = false,
        });
        if (folders.Count == 0) return;
        try
        {
            _plugins.Export(session, preset with { ExporterId = exporter.Id, Name = exporter.DisplayName }, folders[0].Path.LocalPath);
        }
        catch (Exception ex) { SetError(ex.Message); }
    }

    private void ApplyPluginPaletteAlgorithm(string algorithmId)
    {
        var session = Current();
        if (session is null) return;
        var palette = ResolveSelectedPalette(session);
        if (palette is null) { SetError("Create or select a palette first."); return; }
        try
        {
            _plugins.ApplyPaletteAlgorithm(session, palette.Value, algorithmId);
            _selectedPaletteIndex = null;
        }
        catch (Exception ex) { SetError(ex.Message); }
        RefreshAll();
    }

    private void ApplyPluginDither(string algorithmId)
    {
        var session = Current();
        if (session is null) return;
        var palette = ResolveSelectedPalette(session);
        if (palette is null) { SetError("Create or select a palette first."); return; }
        try { _plugins.ApplyDitherAlgorithm(session, palette.Value, algorithmId); }
        catch (Exception ex) { SetError(ex.Message); }
        RefreshAll();
    }

    private void ApplyPluginAutoTile(string ruleId)
    {
        var session = Current();
        if (session is null) return;
        if (_selectedTilemap is not { } tilemapId) { SetError("Create or select a tilemap first."); return; }
        try
        {
            _plugins.ApplyAutoTileRule(session, tilemapId, new IntRect(_tileViewportX, _tileViewportY, 8, 8), ruleId);
        }
        catch (Exception ex) { SetError(ex.Message); }
        RefreshAll();
    }

    private PaletteId? ResolveSelectedPalette(DocumentSession session)
    {
        var palettes = session.GetPaletteEditors();
        if (_selectedPalette is { } selected && palettes.Any(value => value.Id == selected)) return selected;
        return palettes.Count == 0 ? null : palettes[0].Id;
    }
}
