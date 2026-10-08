namespace MyLovePixel.Persistence;

public static partial class PixelProjectFile
{
    /// <summary>Detaches editable resources and persistence metadata before background I/O.</summary>
    public static PixelProject CaptureDetached(PixelProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var package = BuildPackage(project);
        var document = ProjectMapper.FromDto(package.PersistenceState.DocumentTemplate!, package.LogicalEntries);
        return new PixelProject(document, package.PersistenceState);
    }
}
