using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MyLovePixel.Application;
using MyLovePixel.Export;

namespace MyLovePixel.Desktop;

public sealed class AvaloniaEditorInteraction(Window window) : IEditorInteraction
{
    private static readonly FilePickerFileType ProjectType = new("MyLovePixel Project")
    {
        Patterns = ["*.pixelproj"],
    };

    private readonly Window _window = window ?? throw new ArgumentNullException(nameof(window));

    public async Task<string?> PickOpenProjectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var files = await _window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open MyLovePixel Project",
            AllowMultiple = false,
            FileTypeFilter = [ProjectType],
        });
        cancellationToken.ThrowIfCancellationRequested();
        return files.Count == 0 ? null : files[0].Path.LocalPath;
    }

    public async Task<string?> PickSaveProjectAsync(DocumentSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        var suggestedName = session.FilePath is null
            ? "sprite.pixelproj"
            : Path.GetFileName(session.FilePath);
        var file = await _window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save MyLovePixel Project",
            SuggestedFileName = suggestedName,
            DefaultExtension = "pixelproj",
            FileTypeChoices = [ProjectType],
        });
        cancellationToken.ThrowIfCancellationRequested();
        return file?.Path.LocalPath;
    }

    public async Task<ExportTarget?> PickExportTargetAsync(DocumentSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        var folders = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Export Game Assets",
            AllowMultiple = false,
        });
        cancellationToken.ThrowIfCancellationRequested();
        if (folders.Count == 0) return null;

        return new ExportTarget(
            new ExportPreset
            {
                Name = "Desktop Default",
                Layout = ExportLayout.SpriteSheet,
                Trim = true,
                ImageBaseName = "sprite",
                MetadataFileName = "sprite.json",
            },
            folders[0].Path.LocalPath);
    }
}
