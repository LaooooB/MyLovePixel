using MyLovePixel.Persistence;
using MyLovePixel.PluginSdk;

namespace MyLovePixel.PluginHost;

public sealed class PluginProjectDataSession
{
    private readonly PixelProject _project;

    internal PluginProjectDataSession(PixelProject project, PluginId pluginId)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        PluginId = pluginId;
    }

    public PluginId PluginId { get; }
    public IReadOnlyList<string> Keys => PluginProjectData.ListKeys(_project, PluginId.Value);

    public bool TryGet(string key, out ReadOnlyMemory<byte> data) =>
        PluginProjectData.TryGet(_project, PluginId.Value, key, out data);

    public void Set(string key, ReadOnlyMemory<byte> data) =>
        PluginProjectData.Set(_project, PluginId.Value, key, data);

    public bool Remove(string key) => PluginProjectData.Remove(_project, PluginId.Value, key);
}

public static class PluginProjectDataIntegration
{
    public static PluginProjectDataSession OpenProjectData(this PluginHost host, PixelProject project, PluginId pluginId)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(project);
        var loaded = host.LoadedPlugins.FirstOrDefault(value => value.Manifest.Id == pluginId)
            ?? throw new InvalidOperationException($"Plugin '{pluginId}' is not loaded.");
        if ((loaded.Manifest.Capabilities & PluginCapability.ProjectData) == 0)
            throw new InvalidOperationException($"Plugin '{pluginId}' did not declare ProjectData capability.");
        return new PluginProjectDataSession(project, pluginId);
    }
}
