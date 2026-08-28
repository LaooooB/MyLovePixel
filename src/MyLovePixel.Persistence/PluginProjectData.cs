namespace MyLovePixel.Persistence;

public static class PluginProjectData
{
    public static IReadOnlyList<string> ListKeys(PixelProject project, string pluginId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var prefix = Prefix(pluginId);
        return project.PersistenceState.OpaqueEntries.Keys
            .Where(path => path.StartsWith(prefix, StringComparison.Ordinal))
            .Select(path => path[prefix.Length..])
            .Where(key => key.Length != 0)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool TryGet(PixelProject project, string pluginId, string key, out ReadOnlyMemory<byte> data)
    {
        ArgumentNullException.ThrowIfNull(project);
        var path = PathFor(pluginId, key);
        if (project.PersistenceState.OpaqueEntries.TryGetValue(path, out var bytes))
        {
            data = bytes.ToArray();
            return true;
        }
        data = default;
        return false;
    }

    public static void Set(PixelProject project, string pluginId, string key, ReadOnlyMemory<byte> data)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (data.IsEmpty) throw new ArgumentException("Plugin project data cannot be empty.", nameof(data));
        project.PersistenceState.OpaqueEntries[PathFor(pluginId, key)] = data.ToArray();
    }

    public static bool Remove(PixelProject project, string pluginId, string key)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.PersistenceState.OpaqueEntries.Remove(PathFor(pluginId, key));
    }

    private static string Prefix(string pluginId)
    {
        ValidatePluginId(pluginId);
        return $"plugins/{pluginId}/";
    }

    private static string PathFor(string pluginId, string key)
    {
        var prefix = Prefix(pluginId);
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Plugin project data key cannot be empty.", nameof(key));
        var normalized = key.Replace('\\', '/').Trim('/');
        if (normalized.Length == 0 || normalized.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new ArgumentException("Plugin project data key must be a safe relative path.", nameof(key));
        return prefix + normalized;
    }

    private static void ValidatePluginId(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId)) throw new ArgumentException("Plugin id cannot be empty.", nameof(pluginId));
        if (pluginId.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_')))
            throw new ArgumentException("Plugin id contains unsupported path characters.", nameof(pluginId));
    }
}
