using MyLovePixel.Commands;
using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Export;
using MyLovePixel.Persistence;
using MyLovePixel.Render;
using MyLovePixel.Tools;

namespace MyLovePixel.Application;

public sealed class DocumentSession
{
    private readonly FrameRenderer _renderer = new();
    private ToolHost? _toolHost;
    private string _activeToolId = BuiltinToolCatalog.DefaultToolId;
    private Rgba32 _primaryColor = new(0, 0, 0, 255);
    private Rgba32 _secondaryColor = new(255, 255, 255, 255);

    public DocumentSession(PixelProject project, string? filePath = null)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        FilePath = string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFullPath(filePath);
        Commands = new CommandBus(project.Document);
        CurrentFrameId = project.Document.FrameOrder.First();
        CurrentLayerId = project.Document.LayerOrder.First();
        Commands.Changed += OnDocumentChanged;
        RefreshToolTarget();
    }

    public event EventHandler? StateChanged;

    internal PixelProject Project { get; }
    internal PixelDocument Document => Project.Document;
    public CommandBus Commands { get; }
    public string? FilePath { get; private set; }
    public bool IsDirty { get; private set; }
    public bool CanUndo => Commands.CanUndo;
    public bool CanRedo => Commands.CanRedo;
    public FrameId CurrentFrameId { get; private set; }
    public LayerId CurrentLayerId { get; private set; }
    public string ActiveToolId => _activeToolId;
    public double Zoom { get; private set; } = 16d;
    public bool HasEditableCel => _toolHost is not null;

    public DocumentSnapshot CaptureSnapshot() => DocumentSnapshot.Capture(Document);

    public DocumentChange Execute(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Commands.Execute(command);
    }

    public void Undo()
    {
        Commands.Undo();
        RefreshToolTarget();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        Commands.Redo();
        RefreshToolTarget();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectFrame(FrameId frameId)
    {
        Document.GetFrame(frameId);
        if (CurrentFrameId == frameId) return;
        _toolHost?.CancelInteraction();
        CurrentFrameId = frameId;
        RefreshToolTarget();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectLayer(LayerId layerId)
    {
        Document.GetLayer(layerId);
        if (CurrentLayerId == layerId) return;
        _toolHost?.CancelInteraction();
        CurrentLayerId = layerId;
        RefreshToolTarget();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectTool(string toolId)
    {
        var tool = BuiltinToolCatalog.Create(toolId);
        if (string.Equals(_activeToolId, tool.Descriptor.Id, StringComparison.Ordinal)) return;
        _activeToolId = tool.Descriptor.Id;
        _toolHost?.SetActiveTool(tool);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<ToolPaletteItem> GetTools() => BuiltinToolCatalog.Describe(_activeToolId);

    public IReadOnlyList<ToolOptionPresentation> GetToolOptions() =>
        _toolHost is null
            ? Array.Empty<ToolOptionPresentation>()
            : ToolPresentationMapper.DescribeOptions(_toolHost);

    public void SetToolOption(string id, object value)
    {
        if (_toolHost is null) throw new InvalidOperationException("The current Layer/Frame has no editable Cel.");
        _toolHost.SetOption(id, value);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public ToolColorState GetToolColors() => new(_primaryColor, _secondaryColor);

    public void SetToolColors(Rgba32 primary, Rgba32 secondary)
    {
        _primaryColor = primary;
        _secondaryColor = secondary;
        _toolHost?.SetColors(primary, secondary);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public ToolDispatchPresentation DispatchPointer(EditorPointerEvent pointerEvent)
    {
        if (_toolHost is null)
            return new ToolDispatchPresentation(false, false, false);

        var surface = Document.Resources.GetSurface(_toolHost.Target.SurfaceId);
        if (surface.Format != PixelFormat.Rgba32)
            throw new InvalidOperationException("Built-in raster tools currently require an RGBA32 Cel surface.");

        var result = _toolHost.Dispatch(ToolPresentationMapper.ToToolEvent(pointerEvent));
        if (!result.Committed)
            StateChanged?.Invoke(this, EventArgs.Empty);
        return new ToolDispatchPresentation(result.Consumed, result.Committed, result.Preview is not null);
    }

    public void CancelToolInteraction()
    {
        if (_toolHost is null) return;
        var hadPreview = _toolHost.Preview is not null || _toolHost.ActiveTool.IsInteracting;
        _toolHost.CancelInteraction();
        if (hadPreview) StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetZoom(double zoom)
    {
        if (!double.IsFinite(zoom) || zoom <= 0d) throw new ArgumentOutOfRangeException(nameof(zoom));
        var clamped = Math.Clamp(zoom, 0.125d, 128d);
        if (Math.Abs(Zoom - clamped) < double.Epsilon) return;
        Zoom = clamped;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public CanvasPresentation RenderCanvas()
    {
        var snapshot = CaptureSnapshot();
        var result = _renderer.Render(snapshot, new FrameRenderRequest(CurrentFrameId));
        return new CanvasPresentation(
            CurrentFrameId,
            result.Surface.Size,
            result.Surface.Bytes,
            BuildPreviewPixels(result.Surface.Size));
    }

    public IReadOnlyList<LayerListItem> GetLayers()
    {
        var snapshot = CaptureSnapshot();
        return snapshot.LayerOrder
            .Select((id, index) =>
            {
                var layer = snapshot.GetLayer(id);
                return new LayerListItem(index, id, layer.Name, layer.Visible, layer.Locked, layer.Opacity, id == CurrentLayerId);
            })
            .ToArray();
    }

    public TimelineWindow GetTimelineWindow(int startIndex, int count)
    {
        if (startIndex < 0) throw new ArgumentOutOfRangeException(nameof(startIndex));
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        var snapshot = CaptureSnapshot();
        var total = snapshot.FrameOrder.Count;
        if (startIndex > total) throw new ArgumentOutOfRangeException(nameof(startIndex));
        var items = snapshot.FrameOrder
            .Skip(startIndex)
            .Take(count)
            .Select((id, offset) => new TimelineFrameItem(
                startIndex + offset,
                id,
                snapshot.GetFrame(id).DurationTicks,
                id == CurrentFrameId))
            .ToArray();
        return new TimelineWindow(startIndex, total, items);
    }

    public IReadOnlyList<PaletteListItem> GetPalettes()
    {
        var snapshot = CaptureSnapshot();
        return snapshot.Palettes
            .OrderBy(pair => pair.Key.Value)
            .Select(pair => new PaletteListItem(pair.Key, pair.Value.Count, pair.Value.TransparentIndex, pair.Value.Revision))
            .ToArray();
    }

    internal void MarkSaved(string path)
    {
        FilePath = Path.GetFullPath(path);
        IsDirty = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<CanvasPreviewPixel> BuildPreviewPixels(IntSize canvasSize)
    {
        if (_toolHost?.Preview is not { } preview) return Array.Empty<CanvasPreviewPixel>();
        var origin = _toolHost.Target.SurfaceOriginInCanvas;
        var values = new List<CanvasPreviewPixel>(preview.Patch.Writes.Count);
        foreach (var write in preview.Patch.Writes)
        {
            var x = checked(origin.X + write.X);
            var y = checked(origin.Y + write.Y);
            if ((uint)x >= (uint)canvasSize.Width || (uint)y >= (uint)canvasSize.Height) continue;
            values.Add(new CanvasPreviewPixel(new IntPoint(x, y), write.Color));
        }
        return values.AsReadOnly();
    }

    private void RefreshToolTarget()
    {
        var cel = Document.FindCel(CurrentLayerId, CurrentFrameId);
        if (cel is null)
        {
            _toolHost?.CancelInteraction();
            _toolHost = null;
            return;
        }

        var target = ToolTarget.FromCel(cel);
        if (_toolHost is null)
        {
            _toolHost = new ToolHost(
                new PixelDocumentToolReader(Document),
                Commands,
                target,
                BuiltinToolCatalog.Create(_activeToolId),
                _primaryColor,
                _secondaryColor);
        }
        else
        {
            _toolHost.SetTarget(target);
        }
    }

    private void OnDocumentChanged(object? sender, DocumentChange change)
    {
        IsDirty = true;
        RefreshToolTarget();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class EditorWorkspace
{
    private readonly List<DocumentSession> _sessions = [];

    public event EventHandler? Changed;

    public IReadOnlyList<DocumentSession> Sessions => _sessions.AsReadOnly();
    public DocumentSession? CurrentSession { get; private set; }

    public DocumentSession NewDocument(int width, int height)
    {
        var session = new DocumentSession(new PixelProject(PixelDocumentFactory.CreateBlank(width, height)));
        AddSession(session);
        return session;
    }

    public DocumentSession Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var session = new DocumentSession(PixelProjectFile.Load(fullPath), fullPath);
        AddSession(session);
        return session;
    }

    public void Save(DocumentSession session, string path)
    {
        EnsureOwned(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        PixelProjectFile.Save(fullPath, session.Project);
        session.MarkSaved(fullPath);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public ExportBundle Export(DocumentSession session, ExportPreset preset, string outputDirectory)
    {
        EnsureOwned(session);
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        var snapshot = session.CaptureSnapshot();
        var bundle = ExportPipeline.CreateDefault().Execute(new ExportRequest(snapshot, preset));
        ExportBundleWriter.WriteToDirectory(bundle, outputDirectory);
        return bundle;
    }

    public void Activate(DocumentSession session)
    {
        EnsureOwned(session);
        if (ReferenceEquals(CurrentSession, session)) return;
        CurrentSession = session;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Close(DocumentSession session)
    {
        EnsureOwned(session);
        var index = _sessions.IndexOf(session);
        _sessions.RemoveAt(index);
        if (ReferenceEquals(CurrentSession, session))
            CurrentSession = _sessions.Count == 0 ? null : _sessions[Math.Min(index, _sessions.Count - 1)];
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void AddSession(DocumentSession session)
    {
        _sessions.Add(session);
        CurrentSession = session;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureOwned(DocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!_sessions.Contains(session)) throw new InvalidOperationException("Document session does not belong to this workspace.");
    }
}

public sealed class CanvasPresentation
{
    private readonly byte[] _rgba;
    private readonly CanvasPreviewPixel[] _previewPixels;

    public CanvasPresentation(
        FrameId frameId,
        IntSize size,
        ReadOnlyMemory<byte> rgba,
        IEnumerable<CanvasPreviewPixel>? previewPixels = null)
    {
        var expected = checked(size.Width * size.Height * 4);
        if (rgba.Length != expected) throw new ArgumentException("Canvas RGBA length does not match size.", nameof(rgba));
        FrameId = frameId;
        Size = size;
        _rgba = rgba.ToArray();
        _previewPixels = (previewPixels ?? Array.Empty<CanvasPreviewPixel>()).ToArray();
    }

    public FrameId FrameId { get; }
    public IntSize Size { get; }
    public ReadOnlyMemory<byte> Rgba => _rgba;
    public IReadOnlyList<CanvasPreviewPixel> PreviewPixels => Array.AsReadOnly(_previewPixels);
}

public sealed record LayerListItem(int Index, LayerId Id, string Name, bool Visible, bool Locked, byte Opacity, bool IsCurrent);
public sealed record TimelineFrameItem(int Index, FrameId Id, long DurationTicks, bool IsCurrent);
public sealed record TimelineWindow(int StartIndex, int TotalCount, IReadOnlyList<TimelineFrameItem> Items);
public sealed record PaletteListItem(PaletteId Id, int ColorCount, byte? TransparentIndex, long Revision);
