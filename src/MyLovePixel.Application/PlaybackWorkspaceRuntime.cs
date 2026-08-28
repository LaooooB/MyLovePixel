using System.Runtime.CompilerServices;
using MyLovePixel.Animation;
using MyLovePixel.Commands.Color;
using MyLovePixel.Commands.Document;
using MyLovePixel.Commands.Effects;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Commands.Resources;
using MyLovePixel.Commands.Tiles;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;
using MyLovePixel.Effects;
using MyLovePixel.Export;
using MyLovePixel.Persistence;
using MyLovePixel.Render;
using MyLovePixel.Selection;

namespace MyLovePixel.Application;

public sealed class PlaybackWorkspaceRuntime
{
    private readonly ConditionalWeakTable<DocumentSession, ClockHolder> _clocks = new();
    private sealed class ClockHolder { public AnimationPlaybackClock Clock { get; } = new(); }

    public bool IsPlaying(DocumentSession session) => _clocks.TryGetValue(session, out var holder) && holder.Clock.IsConfigured && holder.Clock.IsPlaying;

    public void Toggle(DocumentSession session)
    {
        var clock = _clocks.GetOrCreateValue(session).Clock;
        if (!clock.IsConfigured)
        {
            clock.Configure(session.CaptureSnapshot(), startFrameId: session.CurrentFrameId, autoplay: true);
            return;
        }
        if (clock.IsPlaying) clock.Pause();
        else
        {
            clock.Configure(session.CaptureSnapshot(), startFrameId: session.CurrentFrameId, autoplay: true);
        }
    }

    public bool Advance(DocumentSession session, long microseconds)
    {
        if (!_clocks.TryGetValue(session, out var holder) || !holder.Clock.IsConfigured || !holder.Clock.IsPlaying) return false;
        var next = holder.Clock.Advance(microseconds);
        if (next == session.CurrentFrameId) return false;
        session.SelectFrame(next);
        return true;
    }

    public void Stop(DocumentSession session)
    {
        if (_clocks.TryGetValue(session, out var holder) && holder.Clock.IsConfigured) holder.Clock.Pause();
    }
}
