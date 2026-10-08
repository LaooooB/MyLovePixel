using MyLovePixel.Persistence;

namespace MyLovePixel.Application;

public sealed partial class DocumentSession
{
    private string? _savedSemanticHash;

    internal void RefreshSavedState()
    {
        var dirty = _savedSemanticHash is null || !string.Equals(_savedSemanticHash, ProjectSemanticHash.Compute(Document), StringComparison.Ordinal);
        if (IsDirty == dirty) return;
        IsDirty = dirty;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
