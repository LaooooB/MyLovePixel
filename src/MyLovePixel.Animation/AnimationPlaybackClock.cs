using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Animation;

public sealed class AnimationPlaybackClock
{
    private FrameId[] _sequence = [];
    private Dictionary<FrameId, long> _durations = [];
    private AnimationLoopMode _loopMode = AnimationLoopMode.Loop;
    private int _index;
    private int _direction = 1;
    private long _elapsedInFrameTicks;

    public bool IsPlaying { get; private set; }
    public bool IsConfigured => _sequence.Length > 0;
    public FrameId CurrentFrameId => IsConfigured
        ? _sequence[_index]
        : throw new InvalidOperationException("Playback clock has not been configured.");
    public int CurrentSequenceIndex => IsConfigured
        ? _index
        : throw new InvalidOperationException("Playback clock has not been configured.");
    public long ElapsedInFrameTicks => _elapsedInFrameTicks;
    public IReadOnlyList<FrameId> Sequence => Array.AsReadOnly(_sequence);

    public void Configure(
        DocumentSnapshot snapshot,
        AnimationClipId? clipId = null,
        FrameId? startFrameId = null,
        bool autoplay = true)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.FrameOrder.Count == 0)
            throw new InvalidOperationException("Cannot configure playback for a document with no frames.");

        AnimationClip? clip = null;
        if (clipId is { } requestedClipId)
        {
            clip = snapshot.Animation.Clips.FirstOrDefault(value => value.Id == requestedClipId)
                ?? throw new KeyNotFoundException($"Animation clip '{requestedClipId}' does not exist in the snapshot.");
        }

        _sequence = clip is null
            ? snapshot.FrameOrder.ToArray()
            : BuildClipSequence(snapshot.FrameOrder, clip);
        _durations = _sequence.ToDictionary(
            frameId => frameId,
            frameId => snapshot.GetFrame(frameId).DurationTicks);
        _loopMode = clip?.LoopMode ?? AnimationLoopMode.Loop;
        _direction = 1;
        _elapsedInFrameTicks = 0;

        if (startFrameId is { } requestedStart)
        {
            var index = Array.IndexOf(_sequence, requestedStart);
            if (index < 0)
                throw new ArgumentException($"Start frame '{requestedStart}' is outside the configured playback range.", nameof(startFrameId));
            _index = index;
        }
        else
        {
            _index = 0;
        }

        IsPlaying = autoplay;
    }

    public void Play()
    {
        EnsureConfigured();
        IsPlaying = true;
    }

    public void Pause()
    {
        EnsureConfigured();
        IsPlaying = false;
    }

    public void Restart(bool autoplay = true)
    {
        EnsureConfigured();
        _index = 0;
        _direction = 1;
        _elapsedInFrameTicks = 0;
        IsPlaying = autoplay;
    }

    public void Seek(FrameId frameId, long elapsedInFrameTicks = 0)
    {
        EnsureConfigured();
        var index = Array.IndexOf(_sequence, frameId);
        if (index < 0) throw new ArgumentException($"Frame '{frameId}' is outside the configured playback range.", nameof(frameId));
        var duration = _durations[frameId];
        if (elapsedInFrameTicks < 0 || elapsedInFrameTicks >= duration)
            throw new ArgumentOutOfRangeException(nameof(elapsedInFrameTicks));
        _index = index;
        _elapsedInFrameTicks = elapsedInFrameTicks;
        _direction = 1;
    }

    public FrameId Advance(long elapsedTicks)
    {
        EnsureConfigured();
        if (elapsedTicks < 0) throw new ArgumentOutOfRangeException(nameof(elapsedTicks));
        if (!IsPlaying || elapsedTicks == 0) return CurrentFrameId;

        var remaining = elapsedTicks;
        while (remaining > 0 && IsPlaying)
        {
            var currentDuration = _durations[CurrentFrameId];
            var untilNextFrame = checked(currentDuration - _elapsedInFrameTicks);
            if (remaining < untilNextFrame)
            {
                _elapsedInFrameTicks = checked(_elapsedInFrameTicks + remaining);
                remaining = 0;
                break;
            }

            remaining -= untilNextFrame;
            _elapsedInFrameTicks = 0;
            MoveNext();
        }

        return CurrentFrameId;
    }

    private void MoveNext()
    {
        if (_sequence.Length == 1)
        {
            if (_loopMode == AnimationLoopMode.Once) IsPlaying = false;
            return;
        }

        switch (_loopMode)
        {
            case AnimationLoopMode.Once:
                if (_index >= _sequence.Length - 1)
                {
                    _index = _sequence.Length - 1;
                    IsPlaying = false;
                }
                else
                {
                    _index++;
                }
                break;

            case AnimationLoopMode.Loop:
                _index = (_index + 1) % _sequence.Length;
                break;

            case AnimationLoopMode.PingPong:
                var candidate = _index + _direction;
                if (candidate >= _sequence.Length)
                {
                    _direction = -1;
                    candidate = _sequence.Length - 2;
                }
                else if (candidate < 0)
                {
                    _direction = 1;
                    candidate = 1;
                }
                _index = candidate;
                break;

            default:
                throw new InvalidOperationException($"Unsupported loop mode '{_loopMode}'.");
        }
    }

    private static FrameId[] BuildClipSequence(IReadOnlyList<FrameId> frameOrder, AnimationClip clip)
    {
        var startIndex = IndexOf(frameOrder, clip.StartFrameId);
        var endIndex = IndexOf(frameOrder, clip.EndFrameId);
        if (startIndex > endIndex)
            throw new InvalidOperationException($"Animation clip '{clip.Id}' has an invalid frame range in the snapshot.");

        var result = new FrameId[endIndex - startIndex + 1];
        for (var index = 0; index < result.Length; index++)
            result[index] = frameOrder[startIndex + index];
        return result;
    }

    private static int IndexOf(IReadOnlyList<FrameId> values, FrameId value)
    {
        for (var index = 0; index < values.Count; index++)
            if (values[index] == value) return index;
        throw new InvalidOperationException($"Frame '{value}' is not present in the snapshot frame order.");
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured) throw new InvalidOperationException("Playback clock has not been configured.");
    }
}
