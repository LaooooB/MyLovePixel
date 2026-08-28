using MyLovePixel.Commands;
using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Export;
using MyLovePixel.Persistence;
using MyLovePixel.Render;

namespace MyLovePixel.Application;

public sealed class DocumentSession
{
    private readonly FrameRenderer _renderer = new();

    public DocumentSession(PixelProject project, string? filePath = null)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        FilePath = string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFullPath(filePath);
        Commands = new CommandBus(project.Document);
        CurrentFrameId = project.Document.FrameOrder.First();
        CurrentLayerId = project.Document.LayerOrder.First();
        Commands.Changed += OnDocumentChanged;
    }

    public event EventHandler? StateChanged;

    public PixelProject Project { get; }
    public PixelDocument Document => Project.Document;
    public CommandBus Commands { get; }
    public string? FilePath { get; private set; }
    public bool IsDirty { get; private set; }
    public bool CanUndo => Commands.CanUndo;
    public bool CanRedo => Commands.CanRedo;
    public FrameId CurrentFrameId { get; private set; }
    public LayerId CurrentLayerId { get; private set; }
    public string ActiveToolId { get; private set; } = "pencil";
    public double Zoom { get; private set; } = 16d;

    public DocumentSnapshot CaptureSnapshot() => DocumentSnapshot.Capture(Document);

    public DocumentChange Execute(ICommand command) => Commands.Execute(command);

    public void Undo()
    {
        Commands.Undo();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        Commands.Redo();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectFrame(FrameId frameId)
    {
        Document.GetFrame(frameId);
        if (CurrentFrameId == frameId) return;
        CurrentFrameId = frameId;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectLayer(LayerId layerId)
    {
        Document.GetLayer(layerId);
        if (CurrentLayerId == layerId) return;
        CurrentLayerId = layerId;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectTool(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId)) throw new ArgumentException("Tool id cannot be empty.", nameof(toolId));
        var normalized = toolId.Trim();
        if (string.Equals(ActiveToolId, normalized, StringComparison.Ordinal)) return;
        ActiveToolId = normalized;
        StateChanged?.Invoke(this, EventArgs.Empty);
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
        return new CanvasPresentation(CurrentFrameId, result.Surface.Size, result.Surface.Bytes);
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

    private void OnDocumentChanged(object? sender, DocumentChange change)
    {
        IsDirty = true;
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

    public CanvasPresentation(FrameId frameId, IntSize size, ReadOnlyMemory<byte> rgba)
    {
        var expected = checked(size.Width * size.Height * 4);
        if (rgba.Length != expected) throw new ArgumentException("Canvas RGBA length does not match size.", nameof(rgba));
        FrameId = frameId;
        Size = size;
        _rgba = rgba.ToArray();
    }

    public FrameId FrameId { get; }
    public IntSize Size { get; }
    public ReadOnlyMemory<byte> Rgba => _rgba;
}

public sealed record LayerListItem(int Index, LayerId Id, string Name, bool Visible, bool Locked, byte Opacity, bool IsCurrent);
public sealed record TimelineFrameItem(int Index, FrameId Id, long DurationTicks, bool IsCurrent);
public sealed record TimelineWindow(int StartIndex, int TotalCount, IReadOnlyList<TimelineFrameItem> Items);
public sealed record PaletteListItem(PaletteId Id, int ColorCount, byte? TransparentIndex, long Revision);
