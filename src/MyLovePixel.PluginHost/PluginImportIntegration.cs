using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Export;
using MyLovePixel.PluginSdk;

namespace MyLovePixel.PluginHost;

public static class PluginImportIntegration
{
    private const int HeaderProbeLength = 64;

    public static ImportPipeline CreateImportPipeline(this PluginHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var importers = new List<IImporter> { new PngImporter() };
        importers.AddRange(host.Importers.Values.Select(importer => new Adapter(host, importer)));
        return new ImportPipeline(importers);
    }

    private sealed class Adapter(PluginHost host, IPluginImporter plugin) : IImporter
    {
        public string Id => plugin.Id;

        public bool CanImport(ImportRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var owner = host.Importers.GetOwner(plugin.Id);
            try
            {
                var headerLength = Math.Min(HeaderProbeLength, request.Content.Length);
                return plugin.CanImport(request.Name, request.Content.Span[..headerLength]);
            }
            catch (Exception ex)
            {
                host.Record(new PluginDiagnostic(
                    PluginDiagnosticCode.ExecutionFailed,
                    owner,
                    $"Plugin importer '{plugin.Id}' failed while probing input.",
                    plugin.Id,
                    ex));
                throw new AssetPipelineException(
                    AssetPipelineErrorCode.ImportFailed,
                    $"Plugin importer '{plugin.Id}' failed while probing input.",
                    ex);
            }
        }

        public PixelDocument Import(ImportRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var owner = host.Importers.GetOwner(plugin.Id);
            try
            {
                var result = plugin.Import(new PluginImportRequest(request.Name, request.Content))
                    ?? throw new InvalidOperationException("Plugin importer returned null.");
                var image = result.Image
                    ?? throw new InvalidOperationException("Plugin importer returned no image.");
                if (image.Origin != default)
                    throw new InvalidOperationException("Plugin importer image origin must be zero for document import.");
                if (result.Metadata is { Count: > 0 })
                    throw new InvalidOperationException("Plugin importer metadata has no document mapping in Plugin API 1.0.");

                return RgbaDocumentFactory.Create(
                    new IntSize(image.Size.Width, image.Size.Height),
                    image.Rgba.Span);
            }
            catch (AssetPipelineException)
            {
                throw;
            }
            catch (Exception ex)
            {
                host.Record(new PluginDiagnostic(
                    PluginDiagnosticCode.ExecutionFailed,
                    owner,
                    $"Plugin importer '{plugin.Id}' failed.",
                    plugin.Id,
                    ex));
                throw new AssetPipelineException(
                    AssetPipelineErrorCode.ImportFailed,
                    $"Plugin importer '{plugin.Id}' failed.",
                    ex);
            }
        }
    }
}
