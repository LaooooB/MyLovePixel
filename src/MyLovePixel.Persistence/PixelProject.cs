using MyLovePixel.Core.Document;

namespace MyLovePixel.Persistence;

public sealed class PixelProject
{
    public PixelProject(PixelDocument document)
        : this(document, ProjectPersistenceState.Empty)
    {
    }

    internal PixelProject(PixelDocument document, ProjectPersistenceState persistenceState)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        PersistenceState = persistenceState ?? throw new ArgumentNullException(nameof(persistenceState));
    }

    public PixelDocument Document { get; }

    internal ProjectPersistenceState PersistenceState { get; set; }
}

internal sealed class ProjectPersistenceState
{
    public static ProjectPersistenceState Empty { get; } = new(null, null, new Dictionary<string, byte[]>(StringComparer.Ordinal));

    public ProjectPersistenceState(
        ManifestDto? manifestTemplate,
        DocumentDto? documentTemplate,
        Dictionary<string, byte[]> opaqueEntries)
    {
        ManifestTemplate = manifestTemplate;
        DocumentTemplate = documentTemplate;
        OpaqueEntries = opaqueEntries;
    }

    public ManifestDto? ManifestTemplate { get; }
    public DocumentDto? DocumentTemplate { get; }
    public Dictionary<string, byte[]> OpaqueEntries { get; }
}
