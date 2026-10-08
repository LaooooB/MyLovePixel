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

public static partial class AdvancedEditingExtensions
{
    public static TilesetId AddTileset(this DocumentSession session, string name, IntSize tileSize)
    {
        var command = new AddTilesetCommand(name, tileSize);
        session.Execute(command);
        return command.TilesetId;
    }

    public static TileId AddTile(this DocumentSession session, TilesetId tilesetId)
    {
        var command = new AddTileCommand(tilesetId, "Tile");
        session.Execute(command);
        return command.TileId;
    }

    public static TilemapId AddTilemap(this DocumentSession session, string name, TilesetId tilesetId)
    {
        var command = new AddTilemapCommand(name, tilesetId);
        session.Execute(command);
        return command.TilemapId;
    }

    public static IReadOnlyList<TilesetPresentation> GetTilesets(this DocumentSession session)
    {
        var snapshot = session.CaptureSnapshot();
        return snapshot.Tilesets.Values.OrderBy(v => v.Name, StringComparer.Ordinal)
            .Select(v => new TilesetPresentation(v.Id, v.Name, v.TileSize, v.TileOrder.Count)).ToArray();
    }

    public static IReadOnlyList<TilePresentation> GetTiles(this DocumentSession session, TilesetId tilesetId, TileId? current = null)
    {
        var tileset = session.CaptureSnapshot().GetTileset(tilesetId);
        return tileset.TileOrder.Select(id =>
        {
            var tile = tileset.GetTile(id);
            return new TilePresentation(id, tile.Name, tile.SurfaceId, current == id);
        }).ToArray();
    }

    public static IReadOnlyList<TilemapPresentation> GetTilemaps(this DocumentSession session)
    {
        var snapshot = session.CaptureSnapshot();
        return snapshot.Tilemaps.Values.OrderBy(v => v.Name, StringComparer.Ordinal)
            .Select(v => new TilemapPresentation(v.Id, v.Name, v.TilesetId, v.OccupiedCellCount)).ToArray();
    }

    public static void SetTileCell(this DocumentSession session, TilemapId tilemapId, int x, int y, TileId? tileId, TileCellFlags flags = TileCellFlags.None)
    {
        TileCell? cell = tileId is { } id ? new TileCell(id, flags) : null;
        session.Execute(new SetTileCellCommand(tilemapId, new IntPoint(x, y), cell));
    }


    public static void MakeUniqueTile(this DocumentSession session, TilemapId tilemapId, int x, int y) =>
        session.Execute(new MakeUniqueTileCommand(tilemapId, new IntPoint(x, y)));

    public static void CollectUnusedTiles(this DocumentSession session, TilesetId tilesetId) =>
        session.Execute(new CollectUnusedTilesCommand(tilesetId));

    public static TileSurfacePresentation GetTileSurface(this DocumentSession session, TilesetId tilesetId, TileId tileId)
    {
        var snapshot = session.CaptureSnapshot();
        var tile = snapshot.GetTileset(tilesetId).GetTile(tileId);
        var surface = snapshot.GetSurface(tile.SurfaceId);
        return new TileSurfacePresentation(surface.Size, surface.Format, surface.PaletteId, surface.Bytes);
    }

    public static void SetTilePixel(this DocumentSession session, TilesetId tilesetId, TileId tileId, int x, int y, Rgba32 color) =>
        session.Execute(new EditTilePixelsCommand(tilesetId, tileId, [new PixelWrite(x, y, color)]));

    public static void SetIndexedTilePixel(this DocumentSession session, TilesetId tilesetId, TileId tileId, int x, int y, byte index) =>
        session.Execute(new EditTilePixelsCommand(tilesetId, tileId, [new IndexedPixelWrite(x, y, index)]));

    public static IReadOnlyList<AnimationClipPresentation> GetAnimationClips(this DocumentSession session)
    {
        var s = session.CaptureSnapshot();
        return s.Animation.Clips.Select(v => new AnimationClipPresentation(v.Id, v.Name, s.FrameOrder.ToList().IndexOf(v.StartFrameId), s.FrameOrder.ToList().IndexOf(v.EndFrameId), v.LoopMode)).ToArray();
    }

    public static IReadOnlyList<AnimationTagPresentation> GetAnimationTags(this DocumentSession session)
    {
        var s = session.CaptureSnapshot();
        return s.Animation.Tags.Select(v => new AnimationTagPresentation(v.Id, v.Name, s.FrameOrder.ToList().IndexOf(v.StartFrameId), s.FrameOrder.ToList().IndexOf(v.EndFrameId))).ToArray();
    }

    public static AnimationTracksPresentation GetCurrentAnimationTracks(this DocumentSession session)
    {
        var a = session.CaptureSnapshot().Animation;
        var f = session.CurrentFrameId;
        return new AnimationTracksPresentation(
            a.PivotTrack.Values.ContainsKey(f),
            a.HitboxTrack.Values.TryGetValue(f, out var hit) ? hit.Boxes.Count : 0,
            a.HurtboxTrack.Values.TryGetValue(f, out var hurt) ? hurt.Boxes.Count : 0,
            a.SocketTrack.Values.TryGetValue(f, out var sockets) ? sockets.Sockets.Count : 0,
            a.EventTrack.Values.TryGetValue(f, out var events) ? events.Events.Count : 0,
            a.ColorCycleTrack.Values.TryGetValue(f, out var cycles) ? cycles.Cycles.Count : 0);
    }

    public static AnimationClipId AddAnimationClip(this DocumentSession session, string name, int start, int end, AnimationLoopMode loopMode)
    {
        var s = session.CaptureSnapshot();
        start = Math.Clamp(start, 0, s.FrameOrder.Count - 1);
        end = Math.Clamp(end, start, s.FrameOrder.Count - 1);
        var clip = new AnimationClip(AnimationClipId.New(), name, s.FrameOrder[start], s.FrameOrder[end], loopMode);
        session.Execute(new UpsertAnimationClipCommand(clip));
        return clip.Id;
    }

    public static void RemoveAnimationClip(this DocumentSession session, AnimationClipId id) => session.Execute(new RemoveAnimationClipCommand(id));

    public static void UpdateAnimationClip(this DocumentSession session, AnimationClipId id, string name, int start, int end, AnimationLoopMode loopMode)
    {
        var s = session.CaptureSnapshot();
        start = Math.Clamp(start, 0, s.FrameOrder.Count - 1);
        end = Math.Clamp(end, start, s.FrameOrder.Count - 1);
        session.Execute(new UpsertAnimationClipCommand(new AnimationClip(id, name, s.FrameOrder[start], s.FrameOrder[end], loopMode)));
    }

    public static void UpdateAnimationTag(this DocumentSession session, AnimationTagId id, string name, int start, int end)
    {
        var s = session.CaptureSnapshot();
        start = Math.Clamp(start, 0, s.FrameOrder.Count - 1);
        end = Math.Clamp(end, start, s.FrameOrder.Count - 1);
        session.Execute(new UpsertAnimationTagCommand(new AnimationTag(id, name, s.FrameOrder[start], s.FrameOrder[end])));
    }

    public static AnimationTagId AddAnimationTag(this DocumentSession session, string name, int start, int end)
    {
        var s = session.CaptureSnapshot();
        start = Math.Clamp(start, 0, s.FrameOrder.Count - 1);
        end = Math.Clamp(end, start, s.FrameOrder.Count - 1);
        var tag = new AnimationTag(AnimationTagId.New(), name, s.FrameOrder[start], s.FrameOrder[end]);
        session.Execute(new UpsertAnimationTagCommand(tag));
        return tag.Id;
    }

    public static void RemoveAnimationTag(this DocumentSession session, AnimationTagId id) => session.Execute(new RemoveAnimationTagCommand(id));

    public static IReadOnlyList<AnimationBoxPresentation> GetCurrentHitboxes(this DocumentSession session)
    {
        var animation = session.CaptureSnapshot().Animation;
        return animation.HitboxTrack.Values.TryGetValue(session.CurrentFrameId, out var value)
            ? value.Boxes.Select(box => new AnimationBoxPresentation(box.Name, box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height)).ToArray()
            : [];
    }

    public static IReadOnlyList<AnimationBoxPresentation> GetCurrentHurtboxes(this DocumentSession session)
    {
        var animation = session.CaptureSnapshot().Animation;
        return animation.HurtboxTrack.Values.TryGetValue(session.CurrentFrameId, out var value)
            ? value.Boxes.Select(box => new AnimationBoxPresentation(box.Name, box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height)).ToArray()
            : [];
    }

    public static IReadOnlyList<AnimationSocketPresentation> GetCurrentSockets(this DocumentSession session)
    {
        var animation = session.CaptureSnapshot().Animation;
        return animation.SocketTrack.Values.TryGetValue(session.CurrentFrameId, out var value)
            ? value.Sockets.Select(socket => new AnimationSocketPresentation(socket.Name, socket.Position.X, socket.Position.Y)).ToArray()
            : [];
    }

    public static IReadOnlyList<AnimationEventPresentation> GetCurrentAnimationEvents(this DocumentSession session)
    {
        var animation = session.CaptureSnapshot().Animation;
        return animation.EventTrack.Values.TryGetValue(session.CurrentFrameId, out var value)
            ? value.Events.Select(marker => new AnimationEventPresentation(marker.Name, marker.Payload)).ToArray()
            : [];
    }

    public static IReadOnlyList<AnimationColorCyclePresentation> GetCurrentColorCycles(this DocumentSession session)
    {
        var animation = session.CaptureSnapshot().Animation;
        return animation.ColorCycleTrack.Values.TryGetValue(session.CurrentFrameId, out var value)
            ? value.Cycles.Select(cycle => new AnimationColorCyclePresentation(cycle.PaletteId, cycle.StartIndex, cycle.EndIndex, cycle.Offset)).ToArray()
            : [];
    }

    public static void SetHitboxes(this DocumentSession session, IEnumerable<AnimationBoxPresentation> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        var values = boxes.Select(box => new NamedBox(box.Name, new IntRect(box.X, box.Y, box.Width, box.Height))).ToArray();
        if (values.Length == 0) { session.ClearHitboxes(); return; }
        session.Execute(new SetHitboxesKeyframeCommand(session.CurrentFrameId, new BoxFrameValue(values)));
    }

    public static void SetHurtboxes(this DocumentSession session, IEnumerable<AnimationBoxPresentation> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        var values = boxes.Select(box => new NamedBox(box.Name, new IntRect(box.X, box.Y, box.Width, box.Height))).ToArray();
        if (values.Length == 0) { session.ClearHurtboxes(); return; }
        session.Execute(new SetHurtboxesKeyframeCommand(session.CurrentFrameId, new BoxFrameValue(values)));
    }

    public static void SetSockets(this DocumentSession session, IEnumerable<AnimationSocketPresentation> sockets)
    {
        ArgumentNullException.ThrowIfNull(sockets);
        var values = sockets.Select(socket => new SocketPose(socket.Name, new IntPoint(socket.X, socket.Y))).ToArray();
        if (values.Length == 0) { session.ClearSockets(); return; }
        session.Execute(new SetSocketsKeyframeCommand(session.CurrentFrameId, new SocketFrameValue(values)));
    }

    public static void SetAnimationEvents(this DocumentSession session, IEnumerable<AnimationEventPresentation> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var values = events.Select(value => new AnimationEventMarker(value.Name, value.Payload)).ToArray();
        if (values.Length == 0) { session.ClearAnimationEvents(); return; }
        session.Execute(new SetAnimationEventsKeyframeCommand(session.CurrentFrameId, new EventFrameValue(values)));
    }

    public static void SetColorCycles(this DocumentSession session, IEnumerable<AnimationColorCyclePresentation> cycles)
    {
        ArgumentNullException.ThrowIfNull(cycles);
        var values = cycles.Select(cycle => new PaletteCycle(cycle.PaletteId, cycle.StartIndex, cycle.EndIndex, cycle.Offset)).ToArray();
        if (values.Length == 0) { session.ClearColorCycles(); return; }
        session.Execute(new SetColorCyclesKeyframeCommand(session.CurrentFrameId, new ColorCycleFrameValue(values)));
    }

    public static void SetPivot(this DocumentSession session, int x, int y) => session.Execute(new SetPivotKeyframeCommand(session.CurrentFrameId, new IntPoint(x, y)));
    public static void ClearPivot(this DocumentSession session)
    {
        if (session.CaptureSnapshot().Animation.PivotTrack.Values.ContainsKey(session.CurrentFrameId))
            session.Execute(new ClearPivotKeyframeCommand(session.CurrentFrameId));
    }

    public static void SetHitbox(this DocumentSession session, string name, int x, int y, int width, int height) =>
        session.Execute(new SetHitboxesKeyframeCommand(session.CurrentFrameId, new BoxFrameValue([new NamedBox(name, new IntRect(x, y, width, height))])));

    public static void ClearHitboxes(this DocumentSession session)
    {
        if (session.CaptureSnapshot().Animation.HitboxTrack.Values.ContainsKey(session.CurrentFrameId))
            session.Execute(new ClearHitboxesKeyframeCommand(session.CurrentFrameId));
    }

    public static void SetHurtbox(this DocumentSession session, string name, int x, int y, int width, int height) =>
        session.Execute(new SetHurtboxesKeyframeCommand(session.CurrentFrameId, new BoxFrameValue([new NamedBox(name, new IntRect(x, y, width, height))])));

    public static void ClearHurtboxes(this DocumentSession session)
    {
        if (session.CaptureSnapshot().Animation.HurtboxTrack.Values.ContainsKey(session.CurrentFrameId))
            session.Execute(new ClearHurtboxesKeyframeCommand(session.CurrentFrameId));
    }

    public static void SetSocket(this DocumentSession session, string name, int x, int y) =>
        session.Execute(new SetSocketsKeyframeCommand(session.CurrentFrameId, new SocketFrameValue([new SocketPose(name, new IntPoint(x, y))])));

    public static void ClearSockets(this DocumentSession session)
    {
        if (session.CaptureSnapshot().Animation.SocketTrack.Values.ContainsKey(session.CurrentFrameId))
            session.Execute(new ClearSocketsKeyframeCommand(session.CurrentFrameId));
    }

    public static void SetAnimationEvent(this DocumentSession session, string name, string payload) =>
        session.Execute(new SetAnimationEventsKeyframeCommand(session.CurrentFrameId, new EventFrameValue([new AnimationEventMarker(name, payload)])));

    public static void ClearAnimationEvents(this DocumentSession session)
    {
        if (session.CaptureSnapshot().Animation.EventTrack.Values.ContainsKey(session.CurrentFrameId))
            session.Execute(new ClearAnimationEventsKeyframeCommand(session.CurrentFrameId));
    }

    public static void SetColorCycle(this DocumentSession session, PaletteId paletteId, byte start, byte end, int offset) =>
        session.Execute(new SetColorCyclesKeyframeCommand(session.CurrentFrameId, new ColorCycleFrameValue([new PaletteCycle(paletteId, start, end, offset)])));

    public static void ClearColorCycles(this DocumentSession session)
    {
        if (session.CaptureSnapshot().Animation.ColorCycleTrack.Values.ContainsKey(session.CurrentFrameId))
            session.Execute(new ClearColorCyclesKeyframeCommand(session.CurrentFrameId));
    }

    public static IReadOnlyList<SpriteSlice> GetSpriteSlices(this DocumentSession session) => session.CaptureSnapshot().Animation.Slices;

    public static SliceId AddSpriteSlice(this DocumentSession session, string name, int x, int y, int width, int height, int pivotX, int pivotY)
    {
        var slice = new SpriteSlice(SliceId.New(), name, new IntRect(x, y, width, height), new IntPoint(pivotX, pivotY));
        session.Execute(new UpsertSpriteSliceCommand(slice));
        return slice.Id;
    }

    public static void RemoveSpriteSlice(this DocumentSession session, SliceId id) => session.Execute(new RemoveSpriteSliceCommand(id));

    public static void UpdateSpriteSlice(this DocumentSession session, SliceId id, string name, int x, int y, int width, int height, int pivotX, int pivotY, NineSliceInsets? nineSlice)
    {
        var slice = new SpriteSlice(id, name, new IntRect(x, y, width, height), new IntPoint(pivotX, pivotY), nineSlice);
        session.Execute(new UpsertSpriteSliceCommand(slice));
    }

    public static CanvasPresentation RenderCanvasWithOnionSkin(this DocumentSession session, OnionSkinPresentationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settings);
        var current = session.RenderCanvas();
        var snapshot = session.CaptureSnapshot();
        var onion = new OnionSkinRenderer(new FrameRenderer()).Render(
            snapshot,
            new FrameRenderRequest(session.CurrentFrameId),
            new OnionSkinSettings(settings.PreviousFrames, settings.NextFrames, settings.Opacity, settings.DepthFalloff));
        return new CanvasPresentation(
            current.FrameId,
            current.Size,
            onion.Surface.Bytes,
            current.PreviewPixels,
            current.DirtyRegions,
            current.Diagnostics);
    }

    public static DocumentSession ImportPng(this EditorWorkspace workspace, string path)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var document = ImportPipeline.CreateDefault().Execute(
            BuiltinImporterIds.Png,
            new ImportRequest(Path.GetFileName(fullPath), File.ReadAllBytes(fullPath)));
        return workspace.OpenRecovered(new PixelProject(document), fullPath, $"import-{Guid.NewGuid():N}");
    }

    private static CelSnapshot ResolveCurrentCel(DocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.CaptureSnapshot().Cels.FirstOrDefault(v => v.LayerId == session.CurrentLayerId && v.FrameId == session.CurrentFrameId)
            ?? throw new InvalidOperationException("Current Layer/Frame has no Cel.");
    }
}
