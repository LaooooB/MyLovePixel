using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Export;
using MyLovePixel.PluginSdk;
using MyLovePixel.Render;

namespace MyLovePixel.PluginHost;

public static class PluginExportIntegration
{
    public static FrameRenderer CreateFrameRenderer(this PluginHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var graph = new RenderGraph();
        graph.Add(new FrameCompositeRenderNode(host.CreateEffectEngine()));
        return new FrameRenderer(graph);
    }

    public static ExportPipeline CreateExportPipeline(this PluginHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var renderer = host.CreateFrameRenderer();
        var exporters = new List<IExporter> { new GameAssetExporter(renderer) };
        exporters.AddRange(host.Exporters.Values.Select(exporter => new Adapter(host, exporter, renderer)));
        return new ExportPipeline(exporters);
    }

    private sealed class Adapter(PluginHost host, IPluginExporter plugin, FrameRenderer renderer) : IExporter
    {
        public string Id => plugin.Id;

        public ExportBundle Export(ExportRequest request)
        {
            var owner = host.Exporters.GetOwner(plugin.Id);
            try
            {
                var frames = ResolveFrames(request.Snapshot, request.Preset.Selection)
                    .Select(frameId =>
                    {
                        var rendered = renderer.Render(request.Snapshot, new FrameRenderRequest(frameId)).Surface;
                        var image = new PluginImage(
                            new PluginIntSize(rendered.Size.Width, rendered.Size.Height),
                            rendered.Bytes);
                        return new PluginExportFrame(frameId.Value, request.Snapshot.GetFrame(frameId).DurationTicks, image);
                    })
                    .ToArray();
                var options = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["layout"] = request.Preset.Layout.ToString(),
                    ["scale"] = request.Preset.Scale.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["trim"] = request.Preset.Trim.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["imageBaseName"] = request.Preset.ImageBaseName,
                    ["metadataFileName"] = request.Preset.MetadataFileName,
                };
                var result = plugin.Export(new PluginExportRequest(
                    request.Snapshot.Id.Value,
                    request.Preset.Name,
                    frames,
                    options));
                return new ExportBundle(result.Artifacts.Select(artifact =>
                    new ExportArtifact(artifact.RelativePath, artifact.MediaType, artifact.Content)));
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
                    $"Plugin exporter '{plugin.Id}' failed.",
                    plugin.Id,
                    ex));
                throw new AssetPipelineException(
                    AssetPipelineErrorCode.ExportFailed,
                    $"Plugin exporter '{plugin.Id}' failed.",
                    ex);
            }
        }
    }

    private static FrameId[] ResolveFrames(DocumentSnapshot snapshot, ExportFrameSelection selection) => selection.Mode switch
    {
        ExportFrameSelectionMode.All => snapshot.FrameOrder.ToArray(),
        ExportFrameSelectionMode.Explicit => snapshot.FrameOrder.Where(selection.FrameIds.Contains).ToArray(),
        ExportFrameSelectionMode.Clip when selection.ClipId is { } clipId => Range(
            snapshot.FrameOrder,
            snapshot.Animation.Clips.First(value => value.Id == clipId).StartFrameId,
            snapshot.Animation.Clips.First(value => value.Id == clipId).EndFrameId),
        ExportFrameSelectionMode.Tag when selection.TagId is { } tagId => Range(
            snapshot.FrameOrder,
            snapshot.Animation.Tags.First(value => value.Id == tagId).StartFrameId,
            snapshot.Animation.Tags.First(value => value.Id == tagId).EndFrameId),
        _ => throw new InvalidOperationException("Export frame selection is incomplete."),
    };

    private static FrameId[] Range(IReadOnlyList<FrameId> order, FrameId start, FrameId end)
    {
        var startIndex = IndexOf(order, start);
        var endIndex = IndexOf(order, end);
        if (startIndex > endIndex) throw new InvalidOperationException("Animation range start appears after end.");
        return order.Skip(startIndex).Take(endIndex - startIndex + 1).ToArray();
    }

    private static int IndexOf(IReadOnlyList<FrameId> order, FrameId id)
    {
        for (var index = 0; index < order.Count; index++) if (order[index] == id) return index;
        throw new InvalidOperationException($"Frame '{id}' is not present in the snapshot frame order.");
    }
}
