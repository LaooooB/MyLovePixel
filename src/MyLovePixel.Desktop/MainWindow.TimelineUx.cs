using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MyLovePixel.Application;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Desktop;

public sealed partial class MainWindow
{
    private bool _syncTimeline;
    private bool _manualTimelinePage;
    private DocumentSession? _timelineSession;
    private FrameId? _lastTimelineFrame;
    private string? _timelineStamp;
    private readonly Dictionary<FrameId, Button> _frameButtons = [];

    private void RefreshTimeline()
    {
        var session = Current();
        if (session is null)
        {
            _timelineFrames.Children.Clear(); _frameButtons.Clear(); _timelineStamp = null;
            _timelineStatus.Text = string.Empty; _frameDuration.IsEnabled = false; return;
        }
        var snapshot = session.CaptureSnapshot();
        var order = snapshot.FrameOrder.ToArray();
        var currentIndex = Array.IndexOf(order, session.CurrentFrameId);
        var frameChanged = !ReferenceEquals(session, _timelineSession) || _lastTimelineFrame != session.CurrentFrameId;
        if (!ReferenceEquals(session, _timelineSession)) _timelineStart = 0;
        if ((frameChanged && !_manualTimelinePage) && (currentIndex < _timelineStart || currentIndex >= _timelineStart + TimelinePageSize))
            _timelineStart = currentIndex / TimelinePageSize * TimelinePageSize;
        _manualTimelinePage = false;
        _timelineStart = Math.Clamp(_timelineStart, 0, Math.Max(0, (order.Length - 1) / TimelinePageSize * TimelinePageSize));
        var window = session.GetTimelineWindow(_timelineStart, TimelinePageSize);
        var stamp = $"{session.GetHashCode()}:{session.DocumentVersion}:{_timelineStart}:{session.CurrentLayerId}";
        if (stamp != _timelineStamp)
        {
            _timelineStamp = stamp;
            _timelineFrames.Children.Clear();
            _frameButtons.Clear();
            foreach (var frame in window.Items)
            {
                var id = frame.Id;
                var cel = snapshot.Cels.FirstOrDefault(c => c.FrameId == id && c.LayerId == session.CurrentLayerId);
                var linked = cel is not null && snapshot.Cels.Any(c => c.FrameId != id && c.SurfaceId == cel.SurfaceId);
                var state = cel is null ? "Empty layer" : linked ? "Linked" : "Independent";
                var preview = new PixelPreviewView { Width = 58, Height = 40 };
                try { preview.SetPresentation(_plugins.RenderFramePreview(session, id)); }
                catch (Exception ex) { AutomationProperties.SetHelpText(preview, "Preview unavailable: " + ex.Message); }
                var body = new StackPanel { Spacing = 2 };
                body.Children.Add(new TextBlock { Text = (frame.Index + 1).ToString(), HorizontalAlignment = HorizontalAlignment.Center });
                body.Children.Add(preview);
                body.Children.Add(new TextBlock { Text = linked ? "Linked" : $"{frame.DurationTicks / 1000d:0} ms", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Foreground = EditorThemeTokens.TextSecondary });
                var button = new Button { Content = body, Padding = new Thickness(5, 3), MinWidth = 68 };
                Named(button, "frame." + id, $"Frame {frame.Index + 1}, {state}, {frame.DurationTicks / 1000d:0} milliseconds");
                ToolTip.SetTip(button, "Current layer: " + state + (linked ? " · Pixel edits affect linked copies" : string.Empty));
                button.Click += (_, _) => { FinishParameterEdit(); _playback.Stop(session); session.SelectFrame(id); };
                _frameButtons.Add(id, button);
                _timelineFrames.Children.Add(button);
            }
        }
        foreach (var pair in _frameButtons) SetSelected(pair.Value, pair.Key == session.CurrentFrameId);
        if (frameChanged && _frameButtons.TryGetValue(session.CurrentFrameId, out var currentButton))
            Dispatcher.UIThread.Post(() => { if (!_closed) currentButton.BringIntoView(); }, DispatcherPriority.Background);
        _previousPage.IsEnabled = _timelineStart > 0;
        _nextPage.IsEnabled = _timelineStart + TimelinePageSize < order.Length;
        _timelineStatus.Text = $"{window.StartIndex + 1}–{window.StartIndex + window.Items.Count} / {window.TotalCount}";
        _syncTimeline = true;
        try
        {
            _frameDuration.IsEnabled = true;
            if (!ReferenceEquals(_frameDuration, _parameterControl))
                _frameDuration.Value = session.GetTimelineWindow(currentIndex, 1).Items[0].DurationTicks / 1000m;
        }
        finally { _syncTimeline = false; }
        _timelineSession = session;
        _lastTimelineFrame = session.CurrentFrameId;
        RefreshPlaybackState();
    }

    private void ChangeTimelinePage(int direction)
    {
        FinishParameterEdit();
        if (Current() is not { } session) return;
        var count = session.CaptureSnapshot().FrameOrder.Count;
        _timelineStart = Math.Clamp(_timelineStart + direction * TimelinePageSize, 0, Math.Max(0, (count - 1) / TimelinePageSize * TimelinePageSize));
        _manualTimelinePage = true;
        RefreshTimeline();
    }

    private void RefreshPlaybackState()
    {
        var playing = Current() is { } session && _playback.IsPlaying(session);
        _playButton.Content = playing ? "Pause" : "Play";
        AutomationProperties.SetName(_playButton, playing ? "Pause animation" : "Play animation");
        SetSelected(_playButton, playing);
    }
}
