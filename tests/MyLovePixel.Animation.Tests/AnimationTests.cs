using MyLovePixel.Animation;
using MyLovePixel.Commands;
using MyLovePixel.Commands.Timeline;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;
using Xunit;

namespace MyLovePixel.Animation.Tests;

public sealed class AnimationPlaybackClockTests
{
    [Fact]
    public void LoopPlayback_UsesExactFrameDurations()
    {
        var fixture = CreateThreeFrameDocument(10, 20, 30);
        var snapshot = DocumentSnapshot.Capture(fixture.Document);
        var clock = new AnimationPlaybackClock();
        clock.Configure(snapshot);

        Assert.Equal(fixture.Frame0, clock.CurrentFrameId);
        clock.Advance(9);
        Assert.Equal(fixture.Frame0, clock.CurrentFrameId);
        Assert.Equal(9, clock.ElapsedInFrameTicks);

        clock.Advance(1);
        Assert.Equal(fixture.Frame1, clock.CurrentFrameId);
        Assert.Equal(0, clock.ElapsedInFrameTicks);

        clock.Advance(20);
        Assert.Equal(fixture.Frame2, clock.CurrentFrameId);

        clock.Advance(30);
        Assert.Equal(fixture.Frame0, clock.CurrentFrameId);
        Assert.True(clock.IsPlaying);
    }

    [Fact]
    public void OncePlayback_StopsOnLastFrame()
    {
        var fixture = CreateThreeFrameDocument(5, 5, 5);
        fixture.Bus.Execute(new UpsertAnimationClipCommand(
            new AnimationClip(
                AnimationClipId.New(),
                "Once",
                fixture.Frame0,
                fixture.Frame2,
                AnimationLoopMode.Once)));

        var snapshot = DocumentSnapshot.Capture(fixture.Document);
        var clipId = snapshot.Animation.Clips.Single().Id;
        var clock = new AnimationPlaybackClock();
        clock.Configure(snapshot, clipId);

        clock.Advance(15);

        Assert.Equal(fixture.Frame2, clock.CurrentFrameId);
        Assert.False(clock.IsPlaying);
        Assert.Equal(0, clock.ElapsedInFrameTicks);
    }

    [Fact]
    public void PingPongPlayback_ReversesWithoutRepeatingEndpointFrames()
    {
        var fixture = CreateThreeFrameDocument(10, 10, 10);
        var clipId = AnimationClipId.New();
        fixture.Bus.Execute(new UpsertAnimationClipCommand(
            new AnimationClip(
                clipId,
                "Ping Pong",
                fixture.Frame0,
                fixture.Frame2,
                AnimationLoopMode.PingPong)));

        var clock = new AnimationPlaybackClock();
        clock.Configure(DocumentSnapshot.Capture(fixture.Document), clipId);

        clock.Advance(10);
        Assert.Equal(fixture.Frame1, clock.CurrentFrameId);
        clock.Advance(10);
        Assert.Equal(fixture.Frame2, clock.CurrentFrameId);
        clock.Advance(10);
        Assert.Equal(fixture.Frame1, clock.CurrentFrameId);
        clock.Advance(10);
        Assert.Equal(fixture.Frame0, clock.CurrentFrameId);
        clock.Advance(10);
        Assert.Equal(fixture.Frame1, clock.CurrentFrameId);
    }

    private static ThreeFrameFixture CreateThreeFrameDocument(long first, long second, long third)
    {
        var document = PixelDocumentFactory.CreateBlank(2, 2);
        var bus = new CommandBus(document);
        var frame0 = document.FrameOrder[0];

        var copy1 = new CopyFrameCommand(frame0);
        bus.Execute(copy1);
        var frame1 = copy1.NewFrameId;

        var copy2 = new CopyFrameCommand(frame1);
        bus.Execute(copy2);
        var frame2 = copy2.NewFrameId;

        bus.Execute(new SetFrameDurationCommand(frame0, first));
        bus.Execute(new SetFrameDurationCommand(frame1, second));
        bus.Execute(new SetFrameDurationCommand(frame2, third));
        return new ThreeFrameFixture(document, bus, frame0, frame1, frame2);
    }

    private sealed record ThreeFrameFixture(
        PixelDocument Document,
        CommandBus Bus,
        FrameId Frame0,
        FrameId Frame1,
        FrameId Frame2);
}

public sealed class TimelineCommandTests
{
    [Fact]
    public void CopyFrame_CopiesAllPerFrameAnimationTracks_AndUndoRemovesCopiedValues()
    {
        var document = PixelDocumentFactory.CreateBlank(8, 8);
        var bus = new CommandBus(document);
        var source = document.FrameOrder[0];
        var pivot = new IntPoint(3, 4);
        var hitboxes = new BoxFrameValue([
            new NamedBox("attack", new IntRect(1, 2, 3, 2)),
        ]);
        var hurtboxes = new BoxFrameValue([
            new NamedBox("body", new IntRect(0, 0, 4, 6)),
        ]);
        var sockets = new SocketFrameValue([
            new SocketPose("weapon", new IntPoint(7, 3)),
        ]);
        var events = new EventFrameValue([
            new AnimationEventMarker("footstep", "stone"),
        ]);

        bus.Execute(new SetPivotKeyframeCommand(source, pivot));
        bus.Execute(new SetHitboxesKeyframeCommand(source, hitboxes));
        bus.Execute(new SetHurtboxesKeyframeCommand(source, hurtboxes));
        bus.Execute(new SetSocketsKeyframeCommand(source, sockets));
        bus.Execute(new SetAnimationEventsKeyframeCommand(source, events));

        var copy = new CopyFrameCommand(source, FrameCopyMode.Linked);
        bus.Execute(copy);
        var target = copy.NewFrameId;

        Assert.True(document.Animation.PivotTrack.TryGetValue(target, out var copiedPivot));
        Assert.Equal(pivot, copiedPivot);
        Assert.True(document.Animation.HitboxTrack.TryGetValue(target, out var copiedHitboxes));
        Assert.Equal(hitboxes, copiedHitboxes);
        Assert.True(document.Animation.HurtboxTrack.TryGetValue(target, out var copiedHurtboxes));
        Assert.Equal(hurtboxes, copiedHurtboxes);
        Assert.True(document.Animation.SocketTrack.TryGetValue(target, out var copiedSockets));
        Assert.Equal(sockets, copiedSockets);
        Assert.True(document.Animation.EventTrack.TryGetValue(target, out var copiedEvents));
        Assert.Equal(events, copiedEvents);

        bus.Undo();

        Assert.DoesNotContain(target, document.FrameOrder);
        Assert.False(document.Animation.PivotTrack.TryGetValue(target, out _));
        Assert.False(document.Animation.HitboxTrack.TryGetValue(target, out _));
        Assert.False(document.Animation.HurtboxTrack.TryGetValue(target, out _));
        Assert.False(document.Animation.SocketTrack.TryGetValue(target, out _));
        Assert.False(document.Animation.EventTrack.TryGetValue(target, out _));
    }

    [Fact]
    public void MoveFrame_RejectsRangeInversionWithoutChangingOrder()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 2);
        var bus = new CommandBus(document);
        var frame0 = document.FrameOrder[0];
        var copy1 = new CopyFrameCommand(frame0);
        bus.Execute(copy1);
        var frame1 = copy1.NewFrameId;
        var copy2 = new CopyFrameCommand(frame1);
        bus.Execute(copy2);
        var frame2 = copy2.NewFrameId;

        bus.Execute(new UpsertAnimationClipCommand(
            new AnimationClip(AnimationClipId.New(), "Attack", frame0, frame1)));
        bus.Execute(new UpsertAnimationTagCommand(
            new AnimationTag(AnimationTagId.New(), "Contact", frame0, frame1)));
        var before = document.FrameOrder.ToArray();
        var undoCount = bus.UndoCount;

        Assert.Throws<InvalidOperationException>(() =>
            bus.Execute(new MoveFrameCommand(frame0, 2)));

        Assert.Equal(before, document.FrameOrder);
        Assert.Equal(undoCount, bus.UndoCount);
        Assert.Equal(frame2, document.FrameOrder[2]);
    }

    [Fact]
    public void RemoveFrame_ShrinksClipAndTagEndpoints_AndUndoRestoresThem()
    {
        var document = PixelDocumentFactory.CreateBlank(2, 2);
        var bus = new CommandBus(document);
        var frame0 = document.FrameOrder[0];
        var copy1 = new CopyFrameCommand(frame0);
        bus.Execute(copy1);
        var frame1 = copy1.NewFrameId;
        var copy2 = new CopyFrameCommand(frame1);
        bus.Execute(copy2);
        var frame2 = copy2.NewFrameId;
        var clipId = AnimationClipId.New();
        var tagId = AnimationTagId.New();

        bus.Execute(new UpsertAnimationClipCommand(
            new AnimationClip(clipId, "Run", frame0, frame2)));
        bus.Execute(new UpsertAnimationTagCommand(
            new AnimationTag(tagId, "Stride", frame0, frame2)));

        bus.Execute(new RemoveFrameCommand(frame0));

        Assert.Equal(frame1, document.Animation.GetClip(clipId).StartFrameId);
        Assert.Equal(frame2, document.Animation.GetClip(clipId).EndFrameId);
        Assert.Equal(frame1, document.Animation.GetTag(tagId).StartFrameId);

        bus.Undo();

        Assert.Equal(frame0, document.FrameOrder[0]);
        Assert.Equal(frame0, document.Animation.GetClip(clipId).StartFrameId);
        Assert.Equal(frame2, document.Animation.GetClip(clipId).EndFrameId);
        Assert.Equal(frame0, document.Animation.GetTag(tagId).StartFrameId);
    }
}
