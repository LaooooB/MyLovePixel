using System.Runtime.CompilerServices;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Export;
using MyLovePixel.PluginHost;
using MyLovePixel.PluginSdk;

namespace MyLovePixel.Application;

public sealed record PluginLoadPresentation(bool Succeeded, string? PluginId, string? Error);
public sealed record PluginInfoPresentation(string Id, string Name, string Version, string Capabilities);
public sealed record PluginPanelFieldPresentation(string Id, string Label, string Value, bool ReadOnly);
public sealed record PluginPanelActionPresentation(string Id, string Label, bool Enabled);
public sealed record PluginPanelSectionPresentation(string Title, IReadOnlyList<PluginPanelFieldPresentation> Fields, IReadOnlyList<PluginPanelActionPresentation> Actions);
public sealed record PluginPanelPresentation(string Id, string DisplayName, string Title, IReadOnlyList<PluginPanelSectionPresentation> Sections);
public sealed record PluginPanelActionResult(bool Succeeded, bool Mutated, string? Error);

public sealed class PluginWorkspaceRuntime : IDisposable
{
    private readonly EditorWorkspace _workspace;
    private readonly PluginHost.PluginHost _host = new();
    private readonly PluginAssemblyLoader _loader;
    private readonly Dictionary<DocumentSession, string> _activePluginTools = [];
    private readonly Dictionary<DocumentSession, IReadOnlyList<CanvasPreviewPixel>> _previews = [];
    private int _disposed;

    internal PluginWorkspaceRuntime(EditorWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _loader = new PluginAssemblyLoader(_host);
    }

    public IReadOnlyList<PluginInfoPresentation> Plugins => _host.LoadedPlugins
        .Select(value => new PluginInfoPresentation(
            value.Manifest.Id.Value,
            value.Manifest.Name,
            value.Manifest.Version,
            value.Manifest.Capabilities.ToString()))
        .ToArray();

    public IReadOnlyList<string> Diagnostics => _host.Diagnostics
        .Select(value => $"{value.Code}: {value.Message}")
        .ToArray();

    public PluginLoadPresentation LoadAssembly(string assemblyPath)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        var result = _loader.Load(assemblyPath);
        return new PluginLoadPresentation(
            result.Succeeded,
            result.PluginId?.Value,
            result.Diagnostic?.Message);
    }

    public bool Unload(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        var id = new PluginId(pluginId);
        var removed = _loader.Unload(id);
        if (!removed) return false;
        foreach (var pair in _activePluginTools.Where(pair => !_host.Tools.Ids.Contains(pair.Value, StringComparer.Ordinal)).ToArray())
        {
            _activePluginTools.Remove(pair.Key);
            _previews.Remove(pair.Key);
        }
        return true;
    }

    public IReadOnlyList<ToolPaletteItem> GetTools(DocumentSession session)
    {
        EnsureOwned(session);
        _activePluginTools.TryGetValue(session, out var activePlugin);
        var builtins = session.GetTools()
            .Select(value => activePlugin is null ? value : value with { IsActive = false });
        var plugins = _host.Tools.Values
            .OrderBy(value => value.DisplayName, StringComparer.Ordinal)
            .Select(value => new ToolPaletteItem(
                value.Id,
                value.DisplayName,
                string.Equals(value.Id, activePlugin, StringComparison.Ordinal)));
        return builtins.Concat(plugins).ToArray();
    }

    public void SelectTool(DocumentSession session, string toolId)
    {
        EnsureOwned(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        if (_host.Tools.TryGet(toolId, out _))
        {
            session.CancelToolInteraction();
            _activePluginTools[session] = toolId;
            _previews.Remove(session);
            return;
        }

        _activePluginTools.Remove(session);
        _previews.Remove(session);
        session.SelectTool(toolId);
    }

    public ToolDispatchPresentation DispatchPointer(DocumentSession session, EditorPointerEvent pointerEvent)
    {
        EnsureOwned(session);
        if (!_activePluginTools.TryGetValue(session, out var toolId))
            return session.DispatchPointer(pointerEvent);

        var cel = session.Document.FindCel(session.CurrentLayerId, session.CurrentFrameId);
        if (cel is null) return new ToolDispatchPresentation(false, false, false);
        var gateway = new PluginMutationGateway(session.Document, session.Commands);
        var result = PluginToolExecution.Execute(
            _host,
            toolId,
            gateway,
            cel.SurfaceId.Value,
            ToPluginPointer(pointerEvent));
        var preview = result.PreviewWrites
            .Select(value => new CanvasPreviewPixel(
                new MyLovePixel.Core.Primitives.IntPoint(
                    checked(cel.Position.X + value.X),
                    checked(cel.Position.Y + value.Y)),
                new Rgba32(value.Color.R, value.Color.G, value.Color.B, value.Color.A)))
            .ToArray();
        if (result.Committed || preview.Length == 0) _previews.Remove(session);
        else _previews[session] = preview;
        return new ToolDispatchPresentation(result.Consumed, result.Committed, preview.Length != 0);
    }

    public void CancelTool(DocumentSession session)
    {
        EnsureOwned(session);
        if (_activePluginTools.ContainsKey(session))
        {
            _previews.Remove(session);
            return;
        }
        session.CancelToolInteraction();
    }

    public CanvasPresentation DecorateCanvas(DocumentSession session, CanvasPresentation presentation)
    {
        EnsureOwned(session);
        ArgumentNullException.ThrowIfNull(presentation);
        if (!_previews.TryGetValue(session, out var preview) || preview.Count == 0) return presentation;
        return new CanvasPresentation(
            presentation.FrameId,
            presentation.Size,
            presentation.Rgba,
            presentation.PreviewPixels.Concat(preview),
            presentation.DirtyRegions,
            presentation.Diagnostics);
    }

    public IReadOnlyList<PluginPanelPresentation> GetPanels(DocumentSession? session)
    {
        if (session is not null) EnsureOwned(session);
        var context = new PluginPanelContext(
            session?.CaptureSnapshot().Id.Value,
            session?.CurrentFrameId.Value,
            session?.CurrentLayerId.Value);
        var result = new List<PluginPanelPresentation>();
        foreach (var provider in _host.Panels.Values.OrderBy(value => value.DisplayName, StringComparer.Ordinal))
        {
            try
            {
                var model = provider.Build(context);
                result.Add(new PluginPanelPresentation(
                    provider.Id,
                    provider.DisplayName,
                    model.Title,
                    model.Sections.Select(section => new PluginPanelSectionPresentation(
                        section.Title,
                        section.Fields.Select(field => new PluginPanelFieldPresentation(field.Id, field.Label, field.Value, field.ReadOnly)).ToArray(),
                        section.Actions.Select(action => new PluginPanelActionPresentation(action.Id, action.Label, action.Enabled)).ToArray())).ToArray()));
            }
            catch
            {
                // Failure is isolated to this panel; host-level execution diagnostics cover domain adapters.
            }
        }
        return result;
    }

    public PluginPanelActionResult InvokePanelAction(DocumentSession session, string panelId, string actionId)
    {
        EnsureOwned(session);
        if (!_host.Panels.TryGet(panelId, out var panel))
            return new PluginPanelActionResult(false, false, $"Panel '{panelId}' is not registered.");
        var cel = session.Document.FindCel(session.CurrentLayerId, session.CurrentFrameId);
        var gateway = new PluginMutationGateway(session.Document, session.Commands);
        PluginRasterTarget? target = null;
        if (cel is not null)
        {
            try { target = gateway.CaptureRgbaTarget(cel.SurfaceId.Value); }
            catch (NotSupportedException) { }
        }
        try
        {
            var patch = panel.Invoke(
                actionId,
                new PluginPanelContext(session.CaptureSnapshot().Id.Value, session.CurrentFrameId.Value, session.CurrentLayerId.Value),
                target);
            if (patch is null) return new PluginPanelActionResult(true, false, null);
            gateway.Execute(patch);
            return new PluginPanelActionResult(true, true, null);
        }
        catch (Exception ex)
        {
            return new PluginPanelActionResult(false, false, ex.Message);
        }
    }

    public ExportBundle Export(DocumentSession session, ExportPreset preset, string outputDirectory)
    {
        EnsureOwned(session);
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        var bundle = _host.CreateExportPipeline().Execute(new ExportRequest(session.CaptureSnapshot(), preset));
        ExportBundleWriter.WriteToDirectory(bundle, outputDirectory);
        return bundle;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _loader.Dispose();
        _activePluginTools.Clear();
        _previews.Clear();
    }

    private void EnsureOwned(DocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!_workspace.Sessions.Contains(session)) throw new InvalidOperationException("Document session does not belong to this workspace.");
    }

    private static PluginPointerEvent ToPluginPointer(EditorPointerEvent value) => new(
        value.PointerId,
        value.Kind switch
        {
            EditorPointerKind.Pressed => PluginPointerKind.Pressed,
            EditorPointerKind.Moved => PluginPointerKind.Moved,
            EditorPointerKind.Released => PluginPointerKind.Released,
            EditorPointerKind.Cancelled => PluginPointerKind.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        },
        new PluginIntPoint(value.CanvasPixel.X, value.CanvasPixel.Y),
        Math.Clamp(value.Pressure, 0d, 1d),
        ToPluginButtons(value.Buttons),
        value.TimestampTicks);

    private static PluginPointerButtons ToPluginButtons(EditorPointerButtons buttons)
    {
        var result = PluginPointerButtons.None;
        if ((buttons & EditorPointerButtons.Primary) != 0) result |= PluginPointerButtons.Primary;
        if ((buttons & EditorPointerButtons.Secondary) != 0) result |= PluginPointerButtons.Secondary;
        if ((buttons & EditorPointerButtons.Middle) != 0) result |= PluginPointerButtons.Middle;
        if ((buttons & EditorPointerButtons.Barrel) != 0) result |= PluginPointerButtons.Barrel;
        if ((buttons & EditorPointerButtons.Eraser) != 0) result |= PluginPointerButtons.Eraser;
        return result;
    }
}

public static class EditorWorkspacePluginExtensions
{
    private static readonly ConditionalWeakTable<EditorWorkspace, PluginWorkspaceRuntime> Runtimes = new();

    public static PluginWorkspaceRuntime Plugins(this EditorWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return Runtimes.GetValue(workspace, static value => new PluginWorkspaceRuntime(value));
    }
}
