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

public sealed record SelectionOverlayPresentation(IntRect Bounds, IReadOnlyList<IntPoint> Pixels);
public sealed record EffectItemPresentation(EffectInstanceId Id, string TypeId, string DisplayName, bool Enabled, int Index);
public sealed record EffectParameterPresentation(string Key, string DisplayName, EffectParameterKind Kind, EffectValue Value, double? Minimum, double? Maximum, bool Animatable, bool HasKeyframe);
public sealed record TilesetPresentation(TilesetId Id, string Name, IntSize TileSize, int TileCount);
public sealed record TilePresentation(TileId Id, string Name, ResourceId SurfaceId, bool IsCurrent);
public sealed record TilemapPresentation(TilemapId Id, string Name, TilesetId TilesetId, int OccupiedCellCount);
public sealed record AnimationClipPresentation(AnimationClipId Id, string Name, int Start, int End, AnimationLoopMode LoopMode);
public sealed record AnimationTagPresentation(AnimationTagId Id, string Name, int Start, int End);
public sealed record AnimationTracksPresentation(bool HasPivot, int HitboxCount, int HurtboxCount, int SocketCount, int EventCount, int ColorCycleCount);
public sealed record AnimationBoxPresentation(string Name, int X, int Y, int Width, int Height);
public sealed record AnimationSocketPresentation(string Name, int X, int Y);
public sealed record AnimationEventPresentation(string Name, string Payload);
public sealed record AnimationColorCyclePresentation(PaletteId PaletteId, byte StartIndex, byte EndIndex, int Offset);
public sealed record OnionSkinPresentationSettings(int PreviousFrames = 1, int NextFrames = 1, byte Opacity = 96, double DepthFalloff = 0.65);
public sealed record TileSurfacePresentation(IntSize Size, PixelFormat Format, PaletteId? PaletteId, ReadOnlyMemory<byte> Bytes);

public static partial class AdvancedEditingExtensions
{
    private static readonly EffectRegistry Effects = EffectRegistry.CreateDefault();

    public static LayerId AddLayer(this DocumentSession session, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        var command = new AddPixelLayerCommand(name ?? $"Layer {session.GetLayers().Count + 1}", session.CurrentFrameId);
        session.Execute(command);
        session.SelectLayer(command.LayerId);
        return command.LayerId;
    }

    public static void RemoveCurrentLayer(this DocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var order = session.CaptureSnapshot().LayerOrder;
        if (order.Count <= 1) return;
        var oldIndex = order.ToList().IndexOf(session.CurrentLayerId);
        var next = order[oldIndex == 0 ? 1 : oldIndex - 1];
        session.Execute(new RemoveLayerCommand(session.CurrentLayerId));
        session.SelectLayer(next);
    }

    public static void MoveCurrentLayer(this DocumentSession session, int delta)
    {
        ArgumentNullException.ThrowIfNull(session);
        var snapshot = session.CaptureSnapshot();
        var oldIndex = snapshot.LayerOrder.ToList().IndexOf(session.CurrentLayerId);
        var next = Math.Clamp(oldIndex + delta, 0, snapshot.LayerOrder.Count - 1);
        if (next != oldIndex) session.Execute(new MoveLayerCommand(session.CurrentLayerId, next));
    }

    public static void EnsureEditableCel(this DocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.HasEditableCel) return;
        session.Execute(new EnsureCelCommand(session.CurrentLayerId, session.CurrentFrameId));
    }

    public static FrameId DuplicateCurrentFrame(this DocumentSession session, bool linked)
    {
        ArgumentNullException.ThrowIfNull(session);
        var command = new CopyFrameCommand(session.CurrentFrameId, linked ? FrameCopyMode.Linked : FrameCopyMode.Independent);
        session.Execute(command);
        session.SelectFrame(command.NewFrameId);
        return command.NewFrameId;
    }

    public static void RemoveCurrentFrame(this DocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var snapshot = session.CaptureSnapshot();
        if (snapshot.FrameOrder.Count <= 1) return;
        var oldIndex = snapshot.FrameOrder.ToList().IndexOf(session.CurrentFrameId);
        var next = snapshot.FrameOrder[oldIndex == 0 ? 1 : oldIndex - 1];
        session.Execute(new RemoveFrameCommand(session.CurrentFrameId));
        session.SelectFrame(next);
    }

    public static void MoveCurrentFrame(this DocumentSession session, int delta)
    {
        ArgumentNullException.ThrowIfNull(session);
        var snapshot = session.CaptureSnapshot();
        var oldIndex = snapshot.FrameOrder.ToList().IndexOf(session.CurrentFrameId);
        var next = Math.Clamp(oldIndex + delta, 0, snapshot.FrameOrder.Count - 1);
        if (next != oldIndex) session.Execute(new MoveFrameCommand(session.CurrentFrameId, next));
    }

    public static void SetCurrentFrameDuration(this DocumentSession session, long durationTicks)
    {
        ArgumentNullException.ThrowIfNull(session);
        var current = session.CaptureSnapshot().GetFrame(session.CurrentFrameId).DurationTicks;
        if (current == durationTicks) return;
        session.Execute(new SetFrameDurationCommand(session.CurrentFrameId, durationTicks));
    }

    public static Rgba32 GetCanvasPixel(this DocumentSession session, int canvasX, int canvasY)
    {
        ArgumentNullException.ThrowIfNull(session);
        var snapshot = session.CaptureSnapshot();
        var cel = snapshot.Cels.FirstOrDefault(value => value.LayerId == session.CurrentLayerId && value.FrameId == session.CurrentFrameId)
            ?? throw new InvalidOperationException("Current Layer/Frame has no Cel.");
        var local = new IntPoint(canvasX - cel.Position.X, canvasY - cel.Position.Y);
        var surface = snapshot.GetSurface(cel.SurfaceId);
        if ((uint)local.X >= (uint)surface.Size.Width || (uint)local.Y >= (uint)surface.Size.Height) return Rgba32.Transparent;
        return surface.GetPixel(local.X, local.Y);
    }

    public static IReadOnlyList<EffectItemPresentation> GetCurrentEffects(this DocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var snapshot = session.CaptureSnapshot();
        var cel = snapshot.Cels.FirstOrDefault(value => value.LayerId == session.CurrentLayerId && value.FrameId == session.CurrentFrameId);
        if (cel is null) return [];
        return cel.Effects.EffectOrder.Select((id, index) =>
        {
            var value = cel.Effects.GetEffect(id);
            var display = Effects.TryGetDescriptor(value.TypeId, out var descriptor) ? descriptor.DisplayName : value.TypeId;
            return new EffectItemPresentation(id, value.TypeId, display, value.Enabled, index);
        }).ToArray();
    }

    public static IReadOnlyList<EffectParameterPresentation> GetEffectParameters(this DocumentSession session, EffectInstanceId effectId)
    {
        ArgumentNullException.ThrowIfNull(session);
        var snapshot = session.CaptureSnapshot();
        var cel = snapshot.Cels.First(value => value.LayerId == session.CurrentLayerId && value.FrameId == session.CurrentFrameId);
        var effect = cel.Effects.GetEffect(effectId);
        if (!Effects.TryGetDescriptor(effect.TypeId, out var descriptor)) return [];
        return descriptor.Parameters.Values.Select(parameter =>
        {
            effect.TryResolveParameter(parameter.Key, session.CurrentFrameId, descriptor, out var value);
            var hasKeyframe = effect.ParameterTracks.TryGetValue(parameter.Key, out var track) && track.Values.ContainsKey(session.CurrentFrameId);
            return new EffectParameterPresentation(parameter.Key, parameter.DisplayName, parameter.Kind, value, parameter.Minimum, parameter.Maximum, parameter.Animatable, hasKeyframe);
        }).ToArray();
    }

    public static IReadOnlyList<string> GetBuiltinEffectTypes() => Effects.TypeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray();

    public static EffectInstanceId AddEffect(this DocumentSession session, string typeId)
    {
        ArgumentNullException.ThrowIfNull(session);
        Effects.GetDescriptor(typeId);
        var cel = ResolveCurrentCel(session);
        var command = new AddEffectCommand(cel.Id, typeId);
        session.Execute(command);
        return command.EffectId;
    }

    public static void RemoveEffect(this DocumentSession session, EffectInstanceId effectId)
    {
        var cel = ResolveCurrentCel(session);
        session.Execute(new RemoveEffectCommand(cel.Id, effectId));
    }

    public static void MoveEffect(this DocumentSession session, EffectInstanceId effectId, int delta)
    {
        var cel = ResolveCurrentCel(session);
        var order = cel.Effects.EffectOrder;
        var index = order.ToList().IndexOf(effectId);
        var next = Math.Clamp(index + delta, 0, order.Count - 1);
        if (next != index) session.Execute(new MoveEffectCommand(cel.Id, effectId, next));
    }

    public static void SetEffectEnabled(this DocumentSession session, EffectInstanceId effectId, bool enabled)
    {
        var cel = ResolveCurrentCel(session);
        session.Execute(new SetEffectEnabledCommand(cel.Id, effectId, enabled));
    }

    public static void SetEffectParameter(this DocumentSession session, EffectInstanceId effectId, string key, EffectValue value)
    {
        var cel = ResolveCurrentCel(session);
        var effect = cel.Effects.GetEffect(effectId);
        var descriptor = Effects.GetDescriptor(effect.TypeId);
        session.Execute(new SetEffectParameterCommand(cel.Id, effectId, key, value, descriptor));
    }


    public static void SetEffectParameterKeyframe(this DocumentSession session, EffectInstanceId effectId, string key, EffectValue value)
    {
        var cel = ResolveCurrentCel(session);
        var effect = cel.Effects.GetEffect(effectId);
        var descriptor = Effects.GetDescriptor(effect.TypeId);
        session.Execute(new SetEffectParameterKeyframeCommand(cel.Id, effectId, session.CurrentFrameId, key, value, descriptor));
    }

    public static void ClearEffectParameterKeyframe(this DocumentSession session, EffectInstanceId effectId, string key)
    {
        var cel = ResolveCurrentCel(session);
        var effect = cel.Effects.GetEffect(effectId);
        if (!effect.ParameterTracks.TryGetValue(key, out var track) || !track.Values.ContainsKey(session.CurrentFrameId)) return;
        session.Execute(new ClearEffectParameterKeyframeCommand(cel.Id, effectId, session.CurrentFrameId, key));
    }

    public static void BakeCurrentEffects(this DocumentSession session)
    {
        var snapshot = session.CaptureSnapshot();
        var cel = snapshot.Cels.First(value => value.LayerId == session.CurrentLayerId && value.FrameId == session.CurrentFrameId);
        if (cel.Effects.EffectOrder.Count == 0) return;
        var plan = new EffectBakePlanner(EffectEngine.CreateDefault()).Prepare(snapshot, session.CurrentFrameId, cel);
        session.Execute(new BakeEffectsCommand(plan));
    }

    public static PaletteId AddDefaultPalette(this DocumentSession session)
    {
        var colors = Enumerable.Range(0, 16).Select(i =>
        {
            var v = checked((byte)(255 * i / 15));
            return new Rgba32(v, v, v, 255);
        }).ToArray();
        var command = new AddPaletteCommand(colors);
        session.Execute(command);
        return command.PaletteId;
    }


    public static void MovePaletteColor(this DocumentSession session, PaletteId paletteId, byte index, int delta)
    {
        var palette = session.CaptureSnapshot().GetPalette(paletteId);
        var target = Math.Clamp(index + delta, 0, palette.Count - 1);
        if (target == index) return;
        var order = Enumerable.Range(0, palette.Count).Select(value => checked((byte)value)).ToList();
        var moved = order[index];
        order.RemoveAt(index);
        order.Insert(target, moved);
        session.Execute(new ReorderPaletteCommand(paletteId, order));
    }
}
