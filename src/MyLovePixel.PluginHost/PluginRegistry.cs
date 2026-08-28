using System.Collections.ObjectModel;
using MyLovePixel.PluginSdk;

namespace MyLovePixel.PluginHost;

public enum PluginDiagnosticCode
{
    IncompatibleApi = 1,
    DuplicatePlugin = 2,
    MissingCapability = 3,
    DuplicateExtension = 4,
    RegistrationFailed = 5,
    ExecutionFailed = 6,
    InvalidMutation = 7,
    ScriptBudgetExceeded = 8,
}

public sealed record PluginDiagnostic(
    PluginDiagnosticCode Code,
    PluginId? PluginId,
    string Message,
    string? ExtensionId = null,
    Exception? Exception = null);

public sealed record PluginLoadResult(bool Succeeded, PluginDiagnostic? Diagnostic)
{
    public static PluginLoadResult Success { get; } = new(true, null);
}

public sealed record LoadedPluginInfo(
    PluginManifest Manifest,
    IReadOnlyList<(PluginExtensionKind Kind, string ExtensionId)> Registrations);

public sealed class PluginExtensionRegistry<T> where T : class, IPluginExtension
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public long Revision { get; private set; }
    public IReadOnlyList<string> Ids => _entries.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<T> Values => _entries.Values.Select(value => value.Extension).ToArray();

    public bool TryGet(string id, out T extension) =>
        _entries.TryGetValue(id, out var entry) ? Return(entry.Extension, out extension) : Return(null, out extension);

    public T Get(string id) => TryGet(id, out var extension)
        ? extension
        : throw new KeyNotFoundException($"Plugin extension '{id}' is not registered.");

    public PluginId GetOwner(string id) => _entries.TryGetValue(id, out var entry)
        ? entry.Owner
        : throw new KeyNotFoundException($"Plugin extension '{id}' is not registered.");

    internal IPluginRegistration Register(PluginId owner, PluginExtensionKind kind, T extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        ValidateExtension(extension);
        if (_entries.ContainsKey(extension.Id))
            throw new InvalidOperationException($"Plugin extension '{extension.Id}' is already registered.");
        _entries.Add(extension.Id, new Entry(owner, extension));
        Revision = checked(Revision + 1);
        return new Registration(this, owner, kind, extension.Id, extension);
    }

    private void Remove(string id, T expected)
    {
        if (!_entries.TryGetValue(id, out var entry) || !ReferenceEquals(entry.Extension, expected)) return;
        _entries.Remove(id);
        Revision = checked(Revision + 1);
    }

    private static void ValidateExtension(T extension)
    {
        if (string.IsNullOrWhiteSpace(extension.Id)) throw new ArgumentException("Plugin extension id cannot be empty.", nameof(extension));
        if (!extension.Id.Contains('.', StringComparison.Ordinal)) throw new ArgumentException("Plugin extension id must be namespaced.", nameof(extension));
        if (string.IsNullOrWhiteSpace(extension.DisplayName)) throw new ArgumentException("Plugin extension display name cannot be empty.", nameof(extension));
    }

    private static bool Return(T? value, out T result)
    {
        result = value!;
        return value is not null;
    }

    private sealed record Entry(PluginId Owner, T Extension);

    private sealed class Registration(
        PluginExtensionRegistry<T> registry,
        PluginId owner,
        PluginExtensionKind kind,
        string extensionId,
        T extension) : IPluginRegistration
    {
        private int _disposed;
        public PluginId Owner { get; } = owner;
        public PluginExtensionKind Kind { get; } = kind;
        public string ExtensionId { get; } = extensionId;
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            registry.Remove(extensionId, extension);
        }
    }
}

internal sealed class PluginRegistrationScope
{
    private readonly List<IPluginRegistration> _registrations = [];

    public IReadOnlyList<IPluginRegistration> Registrations => new ReadOnlyCollection<IPluginRegistration>(_registrations);

    public T Track<T>(T registration) where T : IPluginRegistration
    {
        _registrations.Add(registration);
        return registration;
    }

    public void DisposeAll()
    {
        for (var index = _registrations.Count - 1; index >= 0; index--)
            _registrations[index].Dispose();
        _registrations.Clear();
    }
}
