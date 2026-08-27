using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Document;

public sealed class Frame
{
    public const long DefaultDurationTicks = 100_000; // 100 ms when one tick = 1 microsecond.

    public Frame(FrameId id, long durationTicks = DefaultDurationTicks)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("FrameId cannot be empty.", nameof(id));
        if (durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationTicks));
        Id = id;
        DurationTicks = durationTicks;
    }

    public FrameId Id { get; }
    public long DurationTicks { get; internal set; }
}
