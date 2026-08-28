using MyLovePixel.PluginSdk;

namespace MyLovePixel.PluginHost;

public sealed class PluginHost
{
    private readonly Dictionary<PluginId, LoadedState> _plugins = [];
    private readonly List<PluginDiagnostic> _diagnostics = [];

    public PluginExtensionRegistry<IPluginTool> Tools { get; } = new();
    public PluginExtensionRegistry<IPluginCommand> Commands { get; } = new();
    public PluginExtensionRegistry<IPluginEffectEvaluator> Effects { get; } = new();
    public PluginExtensionRegistry<IPluginExporter> Exporters { get; } = new();
    public PluginExtensionRegistry<IPluginImporter> Importers { get; } = new();
    public PluginExtensionRegistry<IPluginPanelProvider> Panels { get; } = new();
    public PluginExtensionRegistry<IPluginPaletteAlgorithm> PaletteAlgorithms { get; } = new();
    public PluginExtensionRegistry<IPluginDitherAlgorithm> DitherAlgorithms { get; } = new();
    public PluginExtensionRegistry<IPluginAutoTileRule> AutoTileRules { get; } = new();

    public IReadOnlyList<PluginDiagnostic> Diagnostics => _diagnostics.AsReadOnly();
    public IReadOnlyList<LoadedPluginInfo> LoadedPlugins => _plugins.Values
        .OrderBy(value => value.Plugin.Manifest.Id.Value, StringComparer.Ordinal)
        .Select(value => new LoadedPluginInfo(
            value.Plugin.Manifest,
            value.Scope.Registrations.Select(registration => (registration.Kind, registration.ExtensionId)).ToArray()))
        .ToArray();

    public PluginLoadResult Load(IPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        var manifest = plugin.Manifest ?? throw new ArgumentException("Plugin manifest cannot be null.", nameof(plugin));
        if (!PluginApi.IsCompatible(manifest))
            return Fail(new PluginDiagnostic(
                PluginDiagnosticCode.IncompatibleApi,
                manifest.Id,
                $"Plugin API range {manifest.MinimumApiVersion}-{manifest.MaximumApiVersion} is incompatible with host API {PluginApi.Current}."));
        if (_plugins.ContainsKey(manifest.Id))
            return Fail(new PluginDiagnostic(
                PluginDiagnosticCode.DuplicatePlugin,
                manifest.Id,
                $"Plugin '{manifest.Id}' is already loaded."));

        var scope = new PluginRegistrationScope();
        var context = new RegistrationContext(this, manifest, scope);
        try
        {
            plugin.Register(context);
        }
        catch (Exception ex)
        {
            scope.DisposeAll();
            return Fail(new PluginDiagnostic(
                PluginDiagnosticCode.RegistrationFailed,
                manifest.Id,
                $"Plugin '{manifest.Id}' failed during registration.",
                Exception: ex));
        }

        _plugins.Add(manifest.Id, new LoadedState(plugin, scope));
        return PluginLoadResult.Success;
    }

    public bool Unload(PluginId id)
    {
        if (!_plugins.Remove(id, out var state)) return false;
        state.Scope.DisposeAll();
        if (state.Plugin is IPluginLifecycle lifecycle)
        {
            try
            {
                lifecycle.OnUnload();
            }
            catch (Exception ex)
            {
                Record(new PluginDiagnostic(
                    PluginDiagnosticCode.ExecutionFailed,
                    id,
                    $"Plugin '{id}' threw during unload.",
                    Exception: ex));
            }
        }
        return true;
    }

    public bool IsLoaded(PluginId id) => _plugins.ContainsKey(id);

    public void ClearDiagnostics() => _diagnostics.Clear();

    internal void Record(PluginDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        _diagnostics.Add(diagnostic);
    }

    private PluginLoadResult Fail(PluginDiagnostic diagnostic)
    {
        Record(diagnostic);
        return new PluginLoadResult(false, diagnostic);
    }

    private sealed record LoadedState(IPlugin Plugin, PluginRegistrationScope Scope);

    private sealed class RegistrationContext(
        PluginHost host,
        PluginManifest manifest,
        PluginRegistrationScope scope) : IPluginRegistrationContext
    {
        public PluginManifest Manifest { get; } = manifest;

        public IPluginRegistration RegisterTool(IPluginTool tool) =>
            Register(PluginCapability.Tool, () => host.Tools.Register(manifest.Id, PluginExtensionKind.Tool, tool));

        public IPluginRegistration RegisterCommand(IPluginCommand command) =>
            Register(PluginCapability.Command, () => host.Commands.Register(manifest.Id, PluginExtensionKind.Command, command));

        public IPluginRegistration RegisterEffect(IPluginEffectEvaluator effect) =>
            Register(PluginCapability.Effect, () => host.Effects.Register(manifest.Id, PluginExtensionKind.Effect, effect));

        public IPluginRegistration RegisterExporter(IPluginExporter exporter) =>
            Register(PluginCapability.Exporter, () => host.Exporters.Register(manifest.Id, PluginExtensionKind.Exporter, exporter));

        public IPluginRegistration RegisterImporter(IPluginImporter importer) =>
            Register(PluginCapability.Importer, () => host.Importers.Register(manifest.Id, PluginExtensionKind.Importer, importer));

        public IPluginRegistration RegisterPanel(IPluginPanelProvider panel) =>
            Register(PluginCapability.Panel, () => host.Panels.Register(manifest.Id, PluginExtensionKind.Panel, panel));

        public IPluginRegistration RegisterPaletteAlgorithm(IPluginPaletteAlgorithm algorithm) =>
            Register(PluginCapability.Palette, () => host.PaletteAlgorithms.Register(manifest.Id, PluginExtensionKind.Palette, algorithm));

        public IPluginRegistration RegisterDitherAlgorithm(IPluginDitherAlgorithm algorithm) =>
            Register(PluginCapability.Dither, () => host.DitherAlgorithms.Register(manifest.Id, PluginExtensionKind.Dither, algorithm));

        public IPluginRegistration RegisterAutoTileRule(IPluginAutoTileRule rule) =>
            Register(PluginCapability.AutoTile, () => host.AutoTileRules.Register(manifest.Id, PluginExtensionKind.AutoTile, rule));

        private IPluginRegistration Register(PluginCapability required, Func<IPluginRegistration> factory)
        {
            if ((manifest.Capabilities & required) != required)
            {
                var diagnostic = new PluginDiagnostic(
                    PluginDiagnosticCode.MissingCapability,
                    manifest.Id,
                    $"Plugin '{manifest.Id}' attempted to register capability '{required}' that is not declared in its manifest.");
                host.Record(diagnostic);
                throw new InvalidOperationException(diagnostic.Message);
            }

            try
            {
                return scope.Track(factory());
            }
            catch (InvalidOperationException ex)
            {
                host.Record(new PluginDiagnostic(
                    PluginDiagnosticCode.DuplicateExtension,
                    manifest.Id,
                    ex.Message,
                    Exception: ex));
                throw;
            }
        }
    }
}
