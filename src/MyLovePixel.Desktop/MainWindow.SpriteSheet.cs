using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using MyLovePixel.Application;
using MyLovePixel.Core.Primitives;
using SkiaSharp;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private readonly TextBlock _spriteSheetStatus = new() { TextWrapping = TextWrapping.Wrap };
    private bool _spriteSheetBusy;

    private Control BuildSpriteSheetImportCard()
    {
        var autoDetect = new CheckBox
        {
            Content = "Auto detect grid",
            IsChecked = true,
        };
        ToolTip.SetTip(autoDetect, "First match the current canvas size; otherwise guess a regular sprite grid.");

        var columns = Number(2, 1, 32);
        var rows = Number(2, 1, 32);
        var duration = Number(100, 1, 60_000);
        var target = new ComboBox
        {
            ItemsSource = new[] { "New animation", "Append timeline" },
            SelectedIndex = 0,
            MinWidth = 150,
        };

        var explanation = new TextBlock
        {
            Text = "Frames are read left → right, then top → bottom. Example: 2 × 5 creates 10 frames.",
            TextWrapping = TextWrapping.Wrap,
        };
        explanation.Classes.Add("subtle");

        var manualHint = new TextBlock
        {
            Text = "Columns / Rows are used when Auto detect is off. Auto detect updates them after a sheet is analyzed.",
            TextWrapping = TextWrapping.Wrap,
        };
        manualHint.Classes.Add("subtle");

        _spriteSheetStatus.Text = "Choose a sprite sheet to split it directly into Timeline frames.";
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
            "Split a regular sprite sheet into animation frames without photo resampling or palette conversion.",
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

        var append = target.SelectedIndex == 1;
        var existingSession = Current();
        if (append && existingSession is null)
        {
            SetSpriteSheetStatus("Open or create a document before using Append timeline.", error: true);
            return;
        }

        var preferredFrameSize = existingSession?.CaptureSnapshot().Canvas.Size;
        var manualColumns = Math.Max(1, (int)(columns.Value ?? 1));
        var manualRows = Math.Max(1, (int)(rows.Value ?? 1));
        var durationMs = Math.Max(1, (int)(duration.Value ?? 100));
        var useAutoDetect = autoDetect.IsChecked == true;
        var originalFrameCount = existingSession?.CaptureSnapshot().FrameOrder.Count ?? 0;

        _spriteSheetBusy = true;
        SetSpriteSheetStatus($"Analyzing {Path.GetFileName(path)}…");
        try
        {
            var decoded = await Task.Run(() => DecodeSpriteSheet(
                path,
                useAutoDetect,
                manualColumns,
                manualRows,
                preferredFrameSize));

            columns.Value = decoded.Columns;
            rows.Value = decoded.Rows;

            DocumentSession targetSession;
            if (append)
            {
                if (!ReferenceEquals(Current(), existingSession))
                    throw new InvalidOperationException("The active document changed while the sprite sheet was being analyzed.");
                targetSession = existingSession!;
                var currentSize = targetSession.CaptureSnapshot().Canvas.Size;
                if (currentSize != decoded.FrameSize)
                    throw new InvalidOperationException(
                        $"Detected frame size is {decoded.FrameSize.Width} × {decoded.FrameSize.Height}, but the current canvas is {currentSize.Width} × {currentSize.Height}. Use New animation or change the grid.");
            }
            else
            {
                targetSession = _workspace.NewDocument(decoded.FrameSize.Width, decoded.FrameSize.Height);
            }

            var imported = targetSession.ImportSpriteSheetFrames(
                decoded.Frames,
                decoded.FrameSize,
                durationMs,
                append,
                "Import Sprite Sheet");

            _selectionMode = false;
            _timelineStart = append ? Math.Max(0, originalFrameCount - 1) : 0;
            RefreshAll();
            SetSpriteSheetStatus(
                $"{Path.GetFileName(path)}: {decoded.Columns} × {decoded.Rows} → {imported.Count} frames, " +
                $"{decoded.FrameSize.Width} × {decoded.FrameSize.Height} each, {durationMs} ms. {decoded.DetectionReason} Ctrl+Z undoes the import.");
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
        IntSize? preferredFrameSize)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Image path is empty.", nameof(path));
        using var source = SKBitmap.Decode(path) ?? throw new InvalidOperationException("The file could not be decoded as an image.");
        if (source.Width <= 0 || source.Height <= 0) throw new InvalidOperationException("The image has no pixels.");

        int columns;
        int rows;
        string reason;
        if (autoDetect)
        {
            var suggestion = SpriteSheetGrid.SuggestGrid(source.Width, source.Height, preferredFrameSize);
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
        var frameWidth = source.Width / columns;
        var frameHeight = source.Height / rows;
        var frames = new byte[slices.Count][];

        foreach (var slice in slices)
        {
            var rgba = new byte[checked(frameWidth * frameHeight * 4)];
            for (var y = 0; y < frameHeight; y++)
            for (var x = 0; x < frameWidth; x++)
            {
                var color = source.GetPixel(slice.Bounds.X + x, slice.Bounds.Y + y);
                var offset = checked((y * frameWidth + x) * 4);
                rgba[offset] = color.Red;
                rgba[offset + 1] = color.Green;
                rgba[offset + 2] = color.Blue;
                rgba[offset + 3] = color.Alpha;
            }
            frames[slice.Index] = rgba;
        }

        return new DecodedSpriteSheet(
            columns,
            rows,
            new IntSize(frameWidth, frameHeight),
            frames,
            reason);
    }

    private sealed record DecodedSpriteSheet(
        int Columns,
        int Rows,
        IntSize FrameSize,
        byte[][] Frames,
        string DetectionReason);
}
