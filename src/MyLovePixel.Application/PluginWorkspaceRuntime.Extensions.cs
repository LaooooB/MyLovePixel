using MyLovePixel.Commands.Color;
using MyLovePixel.Commands.Effects;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Commands.Tiles;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;
using MyLovePixel.Effects;
using MyLovePixel.Export;
using MyLovePixel.PluginHost;
using MyLovePixel.PluginSdk;
using MyLovePixel.Render;
using MyLovePixel.Tilemap;

namespace MyLovePixel.Application;

public sealed record PluginExtensionPresentation(string Id, string DisplayName);
public sealed record PluginCommandRunPresentation(bool Succeeded, bool Mutated, string? Message, string? Error);

public sealed partial class PluginWorkspaceRuntime
{
    private FrameRenderer _pluginRenderer = new();

    private void RefreshSessionRenderers() => _pluginRenderer = _host.CreateFrameRenderer();

    public CanvasPresentation RenderCanvas(DocumentSession session, OnionSkinPresentationSettings? onion = null)
    {
        EnsureOwned(session);
        var baseline = session.RenderCanvas();
        ReadOnlyMemory<byte> rgba;
        if (onion is null)
        {
            rgba = _pluginRenderer.Render(session.CaptureSnapshot(), new FrameRenderRequest(session.CurrentFrameId)).Surface.Bytes;
        }
        else
        {
            rgba = new OnionSkinRenderer(_pluginRenderer).Render(
                session.CaptureSnapshot(),
                new FrameRenderRequest(session.CurrentFrameId),
                new OnionSkinSettings(onion.PreviousFrames, onion.NextFrames, onion.Opacity, onion.DepthFalloff)).Surface.Bytes;
        }
        return DecorateCanvas(session, new CanvasPresentation(
            baseline.FrameId,
            baseline.Size,
            rgba,
            baseline.PreviewPixels,
            baseline.DirtyRegions,
            baseline.Diagnostics));
    }

    public IReadOnlyList<PluginExtensionPresentation> Commands => Describe(_host.Commands.Values);
    public IReadOnlyList<PluginExtensionPresentation> Importers => Describe(_host.Importers.Values);
    public IReadOnlyList<PluginExtensionPresentation> Exporters =>
        new[] { new PluginExtensionPresentation(BuiltinExporterIds.GameAssets, "Game Assets") }
            .Concat(Describe(_host.Exporters.Values)).ToArray();
    public IReadOnlyList<PluginExtensionPresentation> PaletteAlgorithms => Describe(_host.PaletteAlgorithms.Values);
    public IReadOnlyList<PluginExtensionPresentation> DitherAlgorithms => Describe(_host.DitherAlgorithms.Values);
    public IReadOnlyList<PluginExtensionPresentation> AutoTileRules => Describe(_host.AutoTileRules.Values);

    public PluginCommandRunPresentation ExecuteCommand(DocumentSession session, string commandId)
    {
        EnsureOwned(session);
        var cel = session.Document.FindCel(session.CurrentLayerId, session.CurrentFrameId);
        var result = PluginCommandExecution.Execute(
            _host,
            commandId,
            new PluginMutationGateway(session.Document, session.Commands),
            cel?.SurfaceId.Value);
        return new PluginCommandRunPresentation(
            result.Succeeded,
            result.Mutated,
            result.Message,
            result.Diagnostic?.Message);
    }

    public DocumentSession ImportFile(string importerId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(importerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var request = new ImportRequest(Path.GetFileName(fullPath), File.ReadAllBytes(fullPath));
        var document = _host.CreateImportPipeline().Execute(importerId, request);
        return _workspace.OpenImported(new Persistence.PixelProject(document));
    }

    public IReadOnlyList<string> GetEffectTypes() =>
        AdvancedEditingExtensions.GetBuiltinEffectTypes()
            .Concat(_host.Effects.Values.Select(value => value.Descriptor.TypeId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<EffectItemPresentation> GetEffects(DocumentSession session)
    {
        EnsureOwned(session);
        return session.GetCurrentEffects().Select(value =>
        {
            if (_host.Effects.TryGet(value.TypeId, out var plugin))
                return value with { DisplayName = plugin.Descriptor.DisplayName };
            return value;
        }).ToArray();
    }

    public IReadOnlyList<EffectParameterPresentation> GetEffectParameters(DocumentSession session, EffectInstanceId effectId)
    {
        EnsureOwned(session);
        var (cel, effect) = ResolveEffect(session, effectId);
        if (!_host.Effects.TryGet(effect.TypeId, out var plugin)) return session.GetEffectParameters(effectId);
        var descriptor = PluginEffectIntegration.ToCoreDescriptor(plugin.Descriptor);
        return descriptor.Parameters.Values.Select(parameter =>
        {
            effect.TryResolveParameter(parameter.Key, session.CurrentFrameId, descriptor, out var value);
            var hasKeyframe = effect.ParameterTracks.TryGetValue(parameter.Key, out var track) && track.Values.ContainsKey(session.CurrentFrameId);
            return new EffectParameterPresentation(parameter.Key, parameter.DisplayName, parameter.Kind, value, parameter.Minimum, parameter.Maximum, parameter.Animatable, hasKeyframe);
        }).ToArray();
    }

    public EffectInstanceId AddEffect(DocumentSession session, string typeId)
    {
        EnsureOwned(session);
        if (!_host.Effects.TryGet(typeId, out _)) return session.AddEffect(typeId);
        var cel = ResolveCurrentCel(session);
        var command = new AddEffectCommand(cel.Id, typeId);
        session.Execute(command);
        return command.EffectId;
    }

    public void SetEffectParameter(DocumentSession session, EffectInstanceId id, string key, EffectValue value)
    {
        EnsureOwned(session);
        var (cel, effect) = ResolveEffect(session, id);
        if (!_host.Effects.TryGet(effect.TypeId, out var plugin)) { session.SetEffectParameter(id, key, value); return; }
        session.Execute(new SetEffectParameterCommand(cel.Id, id, key, value, PluginEffectIntegration.ToCoreDescriptor(plugin.Descriptor)));
    }

    public void SetEffectParameterKeyframe(DocumentSession session, EffectInstanceId id, string key, EffectValue value)
    {
        EnsureOwned(session);
        var (cel, effect) = ResolveEffect(session, id);
        if (!_host.Effects.TryGet(effect.TypeId, out var plugin)) { session.SetEffectParameterKeyframe(id, key, value); return; }
        session.Execute(new SetEffectParameterKeyframeCommand(cel.Id, id, session.CurrentFrameId, key, value, PluginEffectIntegration.ToCoreDescriptor(plugin.Descriptor)));
    }

    public void ClearEffectParameterKeyframe(DocumentSession session, EffectInstanceId id, string key)
    {
        EnsureOwned(session);
        session.ClearEffectParameterKeyframe(id, key);
    }

    public void BakeEffects(DocumentSession session)
    {
        EnsureOwned(session);
        var snapshot = session.CaptureSnapshot();
        var cel = snapshot.Cels.First(value => value.LayerId == session.CurrentLayerId && value.FrameId == session.CurrentFrameId);
        if (cel.Effects.EffectOrder.Count == 0) return;
        var plan = new EffectBakePlanner(_host.CreateEffectEngine()).Prepare(snapshot, session.CurrentFrameId, cel);
        session.Execute(new BakeEffectsCommand(plan));
    }

    public void ApplyPaletteAlgorithm(DocumentSession session, PaletteId paletteId, string algorithmId)
    {
        EnsureOwned(session);
        if (!_host.PaletteAlgorithms.TryGet(algorithmId, out var algorithm)) throw new KeyNotFoundException($"Palette algorithm '{algorithmId}' is not registered.");
        var palette = session.CaptureSnapshot().GetPalette(paletteId);
        var input = palette.Colors.Select(ToPlugin).ToArray();
        var result = algorithm.Process(input) ?? throw new InvalidOperationException("Plugin palette algorithm returned null.");
        if (result.Count is < 1 or > 256) throw new InvalidOperationException("Plugin palette algorithm must return 1..256 colors.");
        session.Execute(new ReplacePaletteColorsCommand(paletteId, result.Select(ToCore), algorithm.DisplayName));
    }

    public void ApplyDitherAlgorithm(DocumentSession session, PaletteId paletteId, string algorithmId)
    {
        EnsureOwned(session);
        if (!_host.DitherAlgorithms.TryGet(algorithmId, out var algorithm)) throw new KeyNotFoundException($"Dither algorithm '{algorithmId}' is not registered.");
        var snapshot = session.CaptureSnapshot();
        var cel = ResolveCurrentCel(session);
        var surface = snapshot.GetSurface(cel.SurfaceId);
        if (surface.Format != PixelFormat.Rgba32) throw new InvalidOperationException("Plugin dither requires an RGBA32 Cel.");
        var palette = snapshot.GetPalette(paletteId);
        var image = new PluginImage(new PluginIntSize(surface.Size.Width, surface.Size.Height), surface.Bytes);
        var result = algorithm.Process(image, palette.Colors.Select(ToPlugin).ToArray()) ?? throw new InvalidOperationException("Plugin dither algorithm returned null.");
        if (result.Origin != default || result.Size.Width != surface.Size.Width || result.Size.Height != surface.Size.Height)
            throw new InvalidOperationException("Plugin dither output must keep the current surface size and zero origin.");
        session.Execute(new ReplacePixelSurfaceCommand(cel.SurfaceId, PixelFormat.Rgba32, null, result.Rgba, algorithm.DisplayName));
    }

    public void ApplyAutoTileRule(DocumentSession session, TilemapId tilemapId, IntRect area, string ruleId)
    {
        EnsureOwned(session);
        if (!_host.AutoTileRules.TryGet(ruleId, out var rule)) throw new KeyNotFoundException($"AutoTile rule '{ruleId}' is not registered.");
        var snapshot = session.CaptureSnapshot();
        var tilemap = snapshot.GetTilemap(tilemapId);
        var writes = new List<TileCellWrite>();
        for (var y = area.Y; y < area.Bottom; y++)
        for (var x = area.X; x < area.Right; x++)
        {
            var point = new IntPoint(x, y);
            if (tilemap.GetCell(point) is not { } cell) continue;
            var mask = TileNeighborMaskCalculator.Calculate(tilemap, point, TileNeighborMode.Eight);
            var variant = rule.ResolveVariant(unchecked((long)snapshot.Seed), new PluginIntPoint(x, y), (int)mask);
            if (variant is < 0 or > ushort.MaxValue) throw new InvalidOperationException($"Plugin AutoTile rule returned invalid variant {variant}.");
            writes.Add(new TileCellWrite(point, new TileCell(cell.TileId, cell.Flags, checked((ushort)variant))));
        }
        if (writes.Count == 0) return;
        session.Execute(new ApplyTilemapCellPatchCommand(tilemapId, new TilemapCellPatch(writes), rule.DisplayName));
    }

    private static IReadOnlyList<PluginExtensionPresentation> Describe<T>(IReadOnlyCollection<T> extensions) where T : IPluginExtension =>
        extensions.OrderBy(value => value.DisplayName, StringComparer.Ordinal)
            .Select(value => new PluginExtensionPresentation(value.Id, value.DisplayName)).ToArray();

    private static CelSnapshot ResolveCurrentCel(DocumentSession session) =>
        session.CaptureSnapshot().Cels.FirstOrDefault(value => value.LayerId == session.CurrentLayerId && value.FrameId == session.CurrentFrameId)
        ?? throw new InvalidOperationException("Current Layer/Frame has no Cel.");

    private static (CelSnapshot Cel, EffectInstanceSnapshot Effect) ResolveEffect(DocumentSession session, EffectInstanceId effectId)
    {
        var cel = ResolveCurrentCel(session);
        return (cel, cel.Effects.GetEffect(effectId));
    }

    private static PluginRgba32 ToPlugin(Rgba32 value) => new(value.R, value.G, value.B, value.A);
    private static Rgba32 ToCore(PluginRgba32 value) => new(value.R, value.G, value.B, value.A);
}
