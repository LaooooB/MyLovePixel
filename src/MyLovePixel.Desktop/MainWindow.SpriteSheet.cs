using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using SkiaSharp;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private readonly TextBlock _spriteSheetStatus = new() { TextWrapping = TextWrapping.Wrap };
    private bool _spriteSheetBusy;
    private bool _spriteSheetUpdatingGridControls;

    private Control BuildSpriteSheetImportCard()
    {
        var autoDetect = new CheckBox
        {
            Content = "Auto detect grid",
            IsChecked = true,
        };
        ToolTip.SetTip(autoDetect, "Uses the current canvas as a hint. Changing Columns or Rows switches to manual grid mode.");

        var columns = Number(2, 1, 32);
        var rows = Number(2, 1, 32);
        var duration = Number(100, 1, 60_000);
        columns.ValueChanged += (_, _) =>
        {
            if (!_spriteSheetUpdatingGridControls) autoDetect.IsChecked = false;
        };
        rows.ValueChanged += (_, _) =>
        {
            if (!_spriteSheetUpdatingGridControls) autoDetect.IsChecked = false;
        };

        var target = new ComboBox
        {
            ItemsSource = new[] { "New animation", "Append timeline" },
            SelectedIndex = 0,
            MinWidth = 150,
        };

        var explanation = new TextBlock
        {
            Text = "Frames are read left → right, then top → bottom. Example: 4 × 4 always creates 16 frames.",
            TextWrapping = TextWrapping.Wrap,
        };
        explanation.Classes.Add("subtle");

        var manualHint = new TextBlock
        {
            Text = "Changing Columns / Rows turns Auto detect off. Uneven image dimensions are supported; every grid cell is fitted to the current canvas pixel size using the same conversion as Photo → Pixel.",
            TextWrapping = TextWrapping.Wrap,
        };
        manualHint.Classes.Add("subtle");

        _spriteSheetStatus.Text = "Choose a sprite sheet. The current canvas size is the output size for every imported frame.";
        _spriteSheetStatus.Classes.Add("subtle");

        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(explanation);
        body.Children.Add(autoDetect);
        body.Children.Add(Labeled("Columns", columns));
        body.Children.Add(Labeled("Rows", rows));
        body.Children.Add(Labeled("Duration", Icons(duration, new TextBlock { Text = "ms", VerticalAlignment = VerticalAlignment.Center })));
        body.Children.Add(Labeled("Import to", target));
        body.Children.Add(manualHint);
        body.Children.Add(TextIconButton(
            "⇥",
            "Choose Sprite Sheet",
            "Split a sprite sheet into animation frames",
            async () => await ChooseSpriteSheetAsync(autoDetect, columns, rows, duration, target)));
        body.Children.Add(_spriteSheetStatus);

        var dropZone = new Border
        {
            Padding = new Thickness(8),
            CornerRadius = EditorThemeTokens.CardRadius,
            BorderBrush = EditorThemeTokens.StrongBorder,
            BorderThickness = new Thickness(1),
            Child = body,
        };
        DragDrop.SetAllowDrop(dropZone, true);
        DragDrop.AddDragOverHandler(dropZone, (_, e) =>
        {
            e.DragEffects = !_spriteSheetBusy && e.DataTransfer.Formats.Contains(DataFormat.File)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
        });
        DragDrop.AddDropHandler(dropZone, async (_, e) =>
        {
            e.Handled = true;
            if (_spriteSheetBusy || !e.DataTransfer.Formats.Contains(DataFormat.File))
            {
                e.DragEffects = DragDropEffects.None;
                return;
            }

            var file = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().FirstOrDefault();
            if (file is null)
            {
                SetSpriteSheetStatus("Drop an image file, not a folder.", error: true);
                e.DragEffects = DragDropEffects.None;
                return;
            }

            e.DragEffects = DragDropEffects.Copy;
            await ImportSpriteSheetAsync(
                file.Path.LocalPath,
                autoDetect,
                columns,
                rows,
                duration,
                target);
        });

        return SectionCard(
            "Sprite Sheet → Frames",
            "Split a sprite sheet into Timeline frames and convert every cell to the current canvas pixel resolution.",
            dropZone);
    }

    private async Task ChooseSpriteSheetAsync(
        CheckBox autoDetect,
        NumericUpDown columns,
        NumericUpDown rows,
        NumericUpDown duration,
        ComboBox target)
    {
        if (_spriteSheetBusy) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Sprite Sheet → Frames",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif"],
                },
            ],
        });
        if (files.Count == 0) return;
        await ImportSpriteSheetAsync(files[0].Path.LocalPath, autoDetect, columns, rows, duration, target);
    }

    private async Task ImportSpriteSheetAsync(
        string path,
        CheckBox autoDetect,
        NumericUpDown columns,
        NumericUpDown rows,
        NumericUpDown duration,
        ComboBox target)
    {
        if (_spriteSheetBusy) return;

        var existingSession = Current();
        if (existingSession is null)
        {
            SetSpriteSheetStatus("Create or open a canvas first. Its pixel dimensions define the size of every imported frame.", error: true);
            return;
        }

        var sourceSnapshot = existingSession.CaptureSnapshot();
        var canvasSize = sourceSnapshot.Canvas.Size;
        var append = target.SelectedIndex == 1;
        var manualColumns = Math.Max(1, (int)(columns.Value ?? 1));
        var manualRows = Math.Max(1, (int)(rows.Value ?? 1));
        var durationMs = Math.Max(1, (int)(duration.Value ?? 100));
        var useAutoDetect = autoDetect.IsChecked == true;
        var originalFrameCount = sourceSnapshot.FrameOrder.Count;

        _spriteSheetBusy = true;
        SetSpriteSheetStatus(
            $"Analyzing {Path.GetFileName(path)} and converting frames to {canvasSize.Width} × {canvasSize.Height}…");
        try
        {
            var decoded = await Task.Run(() => DecodeSpriteSheet(
                path,
                useAutoDetect,
                manualColumns,
                manualRows,
                canvasSize));

            _spriteSheetUpdatingGridControls = true;
            try
            {
                columns.Value = decoded.Columns;
                rows.Value = decoded.Rows;
            }
            finally
            {
                _spriteSheetUpdatingGridControls = false;
            }

            if (!ReferenceEquals(Current(), existingSession) || existingSession.CaptureSnapshot().Canvas.Size != canvasSize)
                throw new InvalidOperationException("The active canvas changed while the sprite sheet was being analyzed.");

            DocumentSession targetSession;
            if (append)
            {
                targetSession = existingSession;
            }
            else
            {
                targetSession = _workspace.NewDocument(canvasSize.Width, canvasSize.Height);
            }

            var imported = targetSession.ImportSpriteSheetFrames(
                decoded.Frames,
                canvasSize,
                durationMs,
                append,
                "Import Sprite Sheet");

            _selectionMode = false;
            _timelineStart = append ? Math.Max(0, originalFrameCount - 1) : 0;
            RefreshAll();
            SetSpriteSheetStatus(
                $"{Path.GetFileName(path)}: {decoded.Columns} × {decoded.Rows} → {imported.Count} frames, " +
                $"each converted to canvas {canvasSize.Width} × {canvasSize.Height}, {durationMs} ms. " +
                $"{decoded.DetectionReason} Ctrl+Z undoes the import.");
        }
        catch (Exception ex)
        {
            SetSpriteSheetStatus($"Sprite-sheet import failed: {ex.Message}", error: true);
            SetError(ex.Message);
        }
        finally
        {
            _spriteSheetBusy = false;
        }
    }

    private void SetSpriteSheetStatus(string text, bool error = false)
    {
        _spriteSheetStatus.Text = text;
        _spriteSheetStatus.Foreground = error ? EditorThemeTokens.Danger : EditorThemeTokens.TextSecondary;
    }

    private static DecodedSpriteSheet DecodeSpriteSheet(
        string path,
        bool autoDetect,
        int manualColumns,
        int manualRows,
        IntSize targetFrameSize)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Image path is empty.", nameof(path));
        using var source = SKBitmap.Decode(path) ?? throw new InvalidOperationException("The file could not be decoded as an image.");
        if (source.Width <= 0 || source.Height <= 0) throw new InvalidOperationException("The image has no pixels.");

        int columns;
        int rows;
        string reason;
        if (autoDetect)
        {
            var suggestion = SpriteSheetGrid.SuggestGrid(source.Width, source.Height, targetFrameSize);
            columns = suggestion.Columns;
            rows = suggestion.Rows;
            reason = suggestion.Reason;
        }
        else
        {
            columns = manualColumns;
            rows = manualRows;
            reason = "Manual grid.";
        }

        var slices = SpriteSheetGrid.BuildSlices(
            source.Width,
            source.Height,
            columns,
            rows,
            SpriteSheetTraversalOrder.LeftToRightTopToBottom);
        var frames = new byte[slices.Count][];
        var paletteCache = new Dictionary<int, Rgba32>();
        var totalOutputPixels = checked((long)targetFrameSize.Width * targetFrameSize.Height * slices.Count);
        var maxSamplesPerAxis = totalOutputPixels <= 262_144
            ? 4
            : totalOutputPixels <= 1_048_576
                ? 2
                : 1;

        foreach (var slice in slices)
        {
            frames[slice.Index] = ConvertBitmapRegionToPixelRgba(
                source,
                slice.Bounds,
                targetFrameSize.Width,
                targetFrameSize.Height,
                paletteCache,
                maxSamplesPerAxis);
        }

        return new DecodedSpriteSheet(
            columns,
            rows,
            frames,
            reason);
    }

    private sealed record DecodedSpriteSheet(
        int Columns,
        int Rows,
        byte[][] Frames,
        string DetectionReason);
}
