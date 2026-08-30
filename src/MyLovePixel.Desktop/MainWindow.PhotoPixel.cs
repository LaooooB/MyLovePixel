using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using MyLovePixel.Application;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using SkiaSharp;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private static readonly Rgba32[] PhotoPixelPalette = BuildStudioPaletteColors().ToArray();
    private readonly TextBlock _photoPixelStatus = new() { TextWrapping = TextWrapping.Wrap };
    private bool _photoPixelBusy;

    private Control BuildPhotoPixelPanel()
    {
        var dropTitle = new TextBlock
        {
            Text = "Drop a photo here",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var dropHint = new TextBlock
        {
            Text = "The image is center-cropped to your current canvas size, reduced to pixel resolution, then mapped to the 128-color palette.",
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 300,
        };
        dropHint.Classes.Add("muted");

        var dropBody = new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16),
        };
        dropBody.Children.Add(dropTitle);
        dropBody.Children.Add(dropHint);
        dropBody.Children.Add(TextIconButton("⇥", "Choose Photo", "Choose an image file", ChoosePhotoPixelAsync));

        var dropZone = new Border
        {
            MinHeight = 190,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = EditorThemeTokens.SurfaceRaised,
            BorderBrush = EditorThemeTokens.StrongBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = EditorThemeTokens.CardRadius,
            Child = dropBody,
        };
        DragDrop.SetAllowDrop(dropZone, true);
        DragDrop.AddDragOverHandler(dropZone, OnPhotoPixelDragOver);
        DragDrop.AddDropHandler(dropZone, OnPhotoPixelDrop);

        _photoPixelStatus.Text = "Drop or choose a photo. The current canvas dimensions are detected automatically.";
        _photoPixelStatus.Classes.Add("subtle");

        return InspectorScroll(
            SectionCard("Photo → Pixel", "Fast photo conversion for the current canvas. The result replaces the current Cel and can be undone with Ctrl+Z.", dropZone),
            new Border
            {
                Padding = new Thickness(10, 4),
                Child = _photoPixelStatus,
            },
            BuildSpriteSheetImportCard());
    }

    private void OnPhotoPixelDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = !_photoPixelBusy && e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnPhotoPixelDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        if (_photoPixelBusy || !e.DataTransfer.Formats.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var file = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().FirstOrDefault();
        if (file is null)
        {
            e.DragEffects = DragDropEffects.None;
            SetPhotoPixelStatus("Drop an image file, not a folder.", error: true);
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        await ConvertPhotoToCurrentCanvasAsync(file.Path.LocalPath);
    }

    private async Task ChoosePhotoPixelAsync()
    {
        if (_photoPixelBusy) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Photo → Pixel",
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
        await ConvertPhotoToCurrentCanvasAsync(files[0].Path.LocalPath);
    }

    private async Task ConvertPhotoToCurrentCanvasAsync(string path)
    {
        if (_photoPixelBusy) return;
        var session = Current();
        if (session is null)
        {
            SetPhotoPixelStatus("Create or open a canvas first.", error: true);
            return;
        }

        var canvasSize = session.CaptureSnapshot().Canvas.Size;
        if (canvasSize.Width <= 0 || canvasSize.Height <= 0)
        {
            SetPhotoPixelStatus("The current canvas size is invalid.", error: true);
            return;
        }

        _photoPixelBusy = true;
        SetPhotoPixelStatus($"Converting {Path.GetFileName(path)} → {canvasSize.Width} × {canvasSize.Height}…");
        try
        {
            var rgba = await Task.Run(() => ConvertPhotoToPixelRgba(path, canvasSize.Width, canvasSize.Height));
            if (!ReferenceEquals(Current(), session) || session.CaptureSnapshot().Canvas.Size != canvasSize)
            {
                SetPhotoPixelStatus("The active canvas changed while converting. Drop the photo again.", error: true);
                return;
            }

            session.ReplaceCurrentCanvasWithRgba(rgba, "Photo to Pixel");
            _selectionMode = false;
            RefreshAll();
            SetPhotoPixelStatus($"{Path.GetFileName(path)} converted to {canvasSize.Width} × {canvasSize.Height} pixel art. Ctrl+Z to undo.");
        }
        catch (Exception ex)
        {
            SetPhotoPixelStatus($"Photo conversion failed: {ex.Message}", error: true);
            SetError(ex.Message);
        }
        finally
        {
            _photoPixelBusy = false;
        }
    }

    private void SetPhotoPixelStatus(string text, bool error = false)
    {
        _photoPixelStatus.Text = text;
        _photoPixelStatus.Foreground = error ? EditorThemeTokens.Danger : EditorThemeTokens.TextSecondary;
    }

    private static byte[] ConvertPhotoToPixelRgba(string path, int targetWidth, int targetHeight)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Image path is empty.", nameof(path));
        if (targetWidth <= 0 || targetHeight <= 0) throw new ArgumentOutOfRangeException(nameof(targetWidth));

        using var source = SKBitmap.Decode(path) ?? throw new InvalidOperationException("The file could not be decoded as an image.");
        if (source.Width <= 0 || source.Height <= 0) throw new InvalidOperationException("The image has no pixels.");

        return ConvertBitmapRegionToPixelRgba(
            source,
            new IntRect(0, 0, source.Width, source.Height),
            targetWidth,
            targetHeight);
    }

    private static byte[] ConvertBitmapRegionToPixelRgba(
        SKBitmap source,
        IntRect sourceBounds,
        int targetWidth,
        int targetHeight,
        Dictionary<int, Rgba32>? paletteCache = null,
        int? maxSamplesPerAxis = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (targetWidth <= 0 || targetHeight <= 0) throw new ArgumentOutOfRangeException(nameof(targetWidth));
        if (sourceBounds.IsEmpty || sourceBounds.X < 0 || sourceBounds.Y < 0 ||
            sourceBounds.Right > source.Width || sourceBounds.Bottom > source.Height)
            throw new ArgumentOutOfRangeException(nameof(sourceBounds), "Source bounds must be inside the decoded image.");

        var targetAspect = targetWidth / (double)targetHeight;
        var sourceAspect = sourceBounds.Width / (double)sourceBounds.Height;
        double cropX = sourceBounds.X;
        double cropY = sourceBounds.Y;
        double cropWidth = sourceBounds.Width;
        double cropHeight = sourceBounds.Height;

        if (sourceAspect > targetAspect)
        {
            cropWidth = sourceBounds.Height * targetAspect;
            cropX = sourceBounds.X + (sourceBounds.Width - cropWidth) * 0.5d;
        }
        else if (sourceAspect < targetAspect)
        {
            cropHeight = sourceBounds.Width / targetAspect;
            cropY = sourceBounds.Y + (sourceBounds.Height - cropHeight) * 0.5d;
        }

        var rgba = new byte[checked(targetWidth * targetHeight * 4)];
        paletteCache ??= new Dictionary<int, Rgba32>();
        var pixels = (long)targetWidth * targetHeight;
        var defaultMaxSamples = pixels <= 262_144 ? 4 : pixels <= 1_048_576 ? 2 : 1;
        var maxSamples = Math.Clamp(maxSamplesPerAxis ?? defaultMaxSamples, 1, 4);
        var sourcePerPixelX = cropWidth / targetWidth;
        var sourcePerPixelY = cropHeight / targetHeight;
        var samplesX = Math.Clamp((int)Math.Ceiling(sourcePerPixelX), 1, maxSamples);
        var samplesY = Math.Clamp((int)Math.Ceiling(sourcePerPixelY), 1, maxSamples);

        for (var y = 0; y < targetHeight; y++)
        {
            var sourceY0 = cropY + y * sourcePerPixelY;
            for (var x = 0; x < targetWidth; x++)
            {
                var sourceX0 = cropX + x * sourcePerPixelX;
                long weightedR = 0;
                long weightedG = 0;
                long weightedB = 0;
                long alphaSum = 0;
                long rawAlphaSum = 0;
                var sampleCount = 0;

                for (var sy = 0; sy < samplesY; sy++)
                for (var sx = 0; sx < samplesX; sx++)
                {
                    var sampleX = Math.Clamp(
                        (int)Math.Floor(sourceX0 + (sx + 0.5d) * sourcePerPixelX / samplesX),
                        sourceBounds.X,
                        sourceBounds.Right - 1);
                    var sampleY = Math.Clamp(
                        (int)Math.Floor(sourceY0 + (sy + 0.5d) * sourcePerPixelY / samplesY),
                        sourceBounds.Y,
                        sourceBounds.Bottom - 1);
                    var color = source.GetPixel(sampleX, sampleY);
                    var alpha = color.Alpha;
                    weightedR += color.Red * (long)alpha;
                    weightedG += color.Green * (long)alpha;
                    weightedB += color.Blue * (long)alpha;
                    alphaSum += alpha;
                    rawAlphaSum += alpha;
                    sampleCount++;
                }

                var offset = (y * targetWidth + x) * 4;
                if (alphaSum == 0 || sampleCount == 0)
                {
                    rgba[offset] = 0;
                    rgba[offset + 1] = 0;
                    rgba[offset + 2] = 0;
                    rgba[offset + 3] = 0;
                    continue;
                }

                var r = (byte)Math.Clamp((int)(weightedR / alphaSum), 0, 255);
                var g = (byte)Math.Clamp((int)(weightedG / alphaSum), 0, 255);
                var b = (byte)Math.Clamp((int)(weightedB / alphaSum), 0, 255);
                var a = (byte)Math.Clamp((int)(rawAlphaSum / sampleCount), 0, 255);
                var mapped = FindNearestPhotoPaletteColor(r, g, b, paletteCache);
                rgba[offset] = mapped.R;
                rgba[offset + 1] = mapped.G;
                rgba[offset + 2] = mapped.B;
                rgba[offset + 3] = a;
            }
        }

        return rgba;
    }

    private static Rgba32 FindNearestPhotoPaletteColor(byte r, byte g, byte b, Dictionary<int, Rgba32> cache)
    {
        var key = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
        if (cache.TryGetValue(key, out var cached)) return cached;

        var best = PhotoPixelPalette[0];
        var bestDistance = long.MaxValue;
        foreach (var candidate in PhotoPixelPalette)
        {
            var dr = r - candidate.R;
            var dg = g - candidate.G;
            var db = b - candidate.B;
            var distance = 30L * dr * dr + 59L * dg * dg + 11L * db * db;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = candidate;
        }

        cache[key] = best;
        return best;
    }
}
