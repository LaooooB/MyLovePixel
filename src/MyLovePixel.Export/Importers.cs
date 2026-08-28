using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Export;

public sealed class PngImporter : IImporter
{
    public string Id => BuiltinImporterIds.Png;

    public bool CanImport(ImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var data = request.Content.Span;
        return data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    }

    public PixelDocument Import(ImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CanImport(request)) throw new InvalidDataException("Input is not a PNG image.");
        var image = PngCodec.Decode(request.Content.Span);
        var document = PixelDocumentFactory.CreateBlank(image.Size.Width, image.Size.Height);
        var cel = document.Cels.Single();
        var surface = document.Resources.GetSurface(cel.SurfaceId);
        for (var y = 0; y < image.Size.Height; y++)
        for (var x = 0; x < image.Size.Width; x++)
            surface.SetPixel(x, y, image.GetPixel(x, y));
        return document;
    }
}

public static class ExportBundleWriter
{
    public static void WriteToDirectory(ExportBundle bundle, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("Output directory cannot be empty.", nameof(outputDirectory));
        var root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);
        foreach (var artifact in bundle.Artifacts)
        {
            var relative = artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar);
            var path = Path.GetFullPath(Path.Combine(root, relative));
            var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
            if (!path.StartsWith(rootPrefix, StringComparison.Ordinal) && !string.Equals(path, root, StringComparison.Ordinal))
                throw new InvalidOperationException("Export artifact escaped the output directory.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, artifact.Content.ToArray());
        }
    }
}
