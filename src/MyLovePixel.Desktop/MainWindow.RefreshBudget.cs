using Avalonia.Controls;
using Avalonia.VisualTree;
using MyLovePixel.Application;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private DocumentSession? _stableToolsSession;
    private string? _stableToolIds;
    private string? _optionsUiSignature;
    private string? _layersUiSignature;
    private string? _timelineUiSignature;
    private bool _recoveryUiLoaded;
    private readonly Dictionary<Control, string> _inspectorStamps = [];

    // Mirrors the reference release's lazy inspector refresh. Hidden inspectors
    // cannot steal an input frame to rebuild controls or discover files on disk.
    private void RefreshVisibleInspector(Control panel, string stamp, Action refresh)
    {
        if (TopLevel.GetTopLevel(panel) is null || !panel.IsEffectivelyVisible || panel.IsKeyboardFocusWithin) return;
        if (_inspectorStamps.TryGetValue(panel, out var previous) && previous == stamp) return;
        refresh();
        _inspectorStamps[panel] = stamp;
    }

    private string InspectorStamp()
    {
        var session = Current();
        return $"{session?.GetHashCode()}:{session?.CaptureSnapshot().GetHashCode()}:{session?.CurrentFrameId}:{session?.CurrentLayerId}:{_plugins.Plugins.Count}:{_plugins.Diagnostics.Count}";
    }

    private void SyncStableToolButtons(DocumentSession session, IReadOnlyList<ToolPaletteItem> tools)
    {
        var editable = session.HasEditableCel || session.CaptureSnapshot().Layers.ContainsKey(session.CurrentLayerId);
        foreach (var button in _toolsPanel.Children.OfType<Button>())
        {
            if (button.Tag is not string id) continue;
            var selected = id switch
            {
                "selection" => _selectionMode,
                "eyedropper" => _eyedropperMode,
                _ => !_selectionMode && !_eyedropperMode && tools.Any(t => t.Id == id && t.IsActive),
            };
            SetSelectedClass(button, selected);
            button.IsEnabled = id is "selection" or "eyedropper" || editable;
        }
    }
}
