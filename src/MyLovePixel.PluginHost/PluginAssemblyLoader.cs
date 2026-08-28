using System.Reflection;
using System.Runtime.Loader;
using MyLovePixel.PluginSdk;

namespace MyLovePixel.PluginHost;

public sealed record PluginAssemblyLoadResult(
    bool Succeeded,
    PluginId? PluginId,
    PluginDiagnostic? Diagnostic);

public sealed class PluginAssemblyLoader : IDisposable
{
    private readonly PluginHost _host;
    private readonly Dictionary<PluginId, LoadState> _loads = [];
    private int _disposed;

    public PluginAssemblyLoader(PluginHost host) => _host = host ?? throw new ArgumentNullException(nameof(host));

    public IReadOnlyList<PluginId> LoadedAssemblyPlugins => _loads.Keys.OrderBy(value => value.Value, StringComparer.Ordinal).ToArray();

    public PluginAssemblyLoadResult Load(string assemblyPath)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        var fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Plugin assembly does not exist.", fullPath);

        var context = new CollectiblePluginLoadContext(fullPath);
        try
        {
            var assembly = context.LoadFromAssemblyPath(fullPath);
            var pluginTypes = assembly.GetTypes()
                .Where(type =>
                    type is { IsAbstract: false, IsInterface: false } &&
                    typeof(IPlugin).IsAssignableFrom(type) &&
                    type.GetConstructor(Type.EmptyTypes) is not null)
                .ToArray();
            if (pluginTypes.Length != 1)
            {
                context.Unload();
                var diagnostic = new PluginDiagnostic(
                    PluginDiagnosticCode.RegistrationFailed,
                    null,
                    $"Plugin assembly must contain exactly one public parameterless IPlugin implementation; found {pluginTypes.Length}.");
                return new PluginAssemblyLoadResult(false, null, diagnostic);
            }

            var plugin = (IPlugin?)Activator.CreateInstance(pluginTypes[0])
                ?? throw new InvalidOperationException("Plugin activation returned null.");
            var result = _host.Load(plugin);
            if (!result.Succeeded)
            {
                context.Unload();
                return new PluginAssemblyLoadResult(false, plugin.Manifest.Id, result.Diagnostic);
            }

            _loads.Add(plugin.Manifest.Id, new LoadState(context, new WeakReference(context, trackResurrection: false)));
            return new PluginAssemblyLoadResult(true, plugin.Manifest.Id, null);
        }
        catch (Exception ex)
        {
            context.Unload();
            var diagnostic = new PluginDiagnostic(
                PluginDiagnosticCode.RegistrationFailed,
                null,
                $"Plugin assembly '{Path.GetFileName(fullPath)}' could not be loaded.",
                Exception: ex);
            return new PluginAssemblyLoadResult(false, null, diagnostic);
        }
    }

    public bool Unload(PluginId pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        if (!_loads.Remove(pluginId, out var state)) return _host.Unload(pluginId);
        var unloaded = _host.Unload(pluginId);
        state.Context.Unload();
        return unloaded;
    }

    public WeakReference? GetLoadContextWeakReference(PluginId pluginId) =>
        _loads.TryGetValue(pluginId, out var state) ? state.WeakReference : null;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        foreach (var id in _loads.Keys.ToArray())
        {
            _host.Unload(id);
            _loads[id].Context.Unload();
        }
        _loads.Clear();
    }

    private sealed record LoadState(CollectiblePluginLoadContext Context, WeakReference WeakReference);

    private sealed class CollectiblePluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public CollectiblePluginLoadContext(string pluginPath)
            : base($"MyLovePixel.Plugin:{Path.GetFileNameWithoutExtension(pluginPath)}:{Guid.NewGuid():N}", isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (string.Equals(assemblyName.Name, typeof(IPlugin).Assembly.GetName().Name, StringComparison.Ordinal))
                return null;
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
        }
    }
}
