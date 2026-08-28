namespace MyLovePixel.Application;

public readonly record struct ActionId
{
    public ActionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("ActionId cannot be empty.", nameof(value));
        Value = value.Trim();
    }

    public string Value { get; }
    public override string ToString() => Value;
}

[Flags]
public enum ShortcutModifiers
{
    None = 0,
    Control = 1,
    Shift = 2,
    Alt = 4,
    Meta = 8,
}

public readonly record struct ShortcutGesture
{
    public ShortcutGesture(string key, ShortcutModifiers modifiers = ShortcutModifiers.None)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Shortcut key cannot be empty.", nameof(key));
        Key = key.Trim().ToUpperInvariant();
        Modifiers = modifiers;
    }

    public string Key { get; }
    public ShortcutModifiers Modifiers { get; }
}

public sealed class ShortcutMap
{
    private readonly Dictionary<ShortcutGesture, ActionId> _bindings = [];

    public void Bind(ShortcutGesture gesture, ActionId actionId)
    {
        if (_bindings.TryGetValue(gesture, out var existing) && existing != actionId)
            throw new InvalidOperationException($"Shortcut '{gesture}' is already bound to '{existing}'.");
        _bindings[gesture] = actionId;
    }

    public bool TryResolve(ShortcutGesture gesture, out ActionId actionId) => _bindings.TryGetValue(gesture, out actionId);

    public static ShortcutMap CreateDefault()
    {
        var map = new ShortcutMap();
        map.Bind(new ShortcutGesture("N", ShortcutModifiers.Control), BuiltinActionIds.NewProject);
        map.Bind(new ShortcutGesture("O", ShortcutModifiers.Control), BuiltinActionIds.OpenProject);
        map.Bind(new ShortcutGesture("S", ShortcutModifiers.Control), BuiltinActionIds.SaveProject);
        map.Bind(new ShortcutGesture("S", ShortcutModifiers.Control | ShortcutModifiers.Shift), BuiltinActionIds.SaveProjectAs);
        map.Bind(new ShortcutGesture("E", ShortcutModifiers.Control), BuiltinActionIds.ExportProject);
        map.Bind(new ShortcutGesture("Z", ShortcutModifiers.Control), BuiltinActionIds.Undo);
        map.Bind(new ShortcutGesture("Y", ShortcutModifiers.Control), BuiltinActionIds.Redo);
        return map;
    }
}

public sealed class EditorActionContext(EditorWorkspace workspace, IEditorInteraction interaction)
{
    public EditorWorkspace Workspace { get; } = workspace ?? throw new ArgumentNullException(nameof(workspace));
    public IEditorInteraction Interaction { get; } = interaction ?? throw new ArgumentNullException(nameof(interaction));
}

public sealed class ActionDescriptor
{
    private readonly Func<EditorActionContext, bool> _canExecute;
    private readonly Func<EditorActionContext, CancellationToken, Task> _execute;

    public ActionDescriptor(
        ActionId id,
        string displayName,
        Func<EditorActionContext, CancellationToken, Task> execute,
        Func<EditorActionContext, bool>? canExecute = null,
        string category = "General")
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Action display name cannot be empty.", nameof(displayName));
        Id = id;
        DisplayName = displayName;
        Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute ?? (_ => true);
    }

    public ActionId Id { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public bool CanExecute(EditorActionContext context) => _canExecute(context);
    public Task ExecuteAsync(EditorActionContext context, CancellationToken cancellationToken = default) => _execute(context, cancellationToken);
}

public sealed class ActionRegistry
{
    private readonly Dictionary<ActionId, ActionDescriptor> _actions = [];

    public IReadOnlyCollection<ActionDescriptor> Actions => _actions.Values.OrderBy(action => action.Category, StringComparer.Ordinal).ThenBy(action => action.DisplayName, StringComparer.Ordinal).ToArray();

    public void Register(ActionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!_actions.TryAdd(descriptor.Id, descriptor))
            throw new InvalidOperationException($"Action '{descriptor.Id}' is already registered.");
    }

    public ActionDescriptor Get(ActionId id) => _actions.TryGetValue(id, out var action)
        ? action
        : throw new KeyNotFoundException($"Action '{id}' is not registered.");

    public bool CanExecute(ActionId id, EditorActionContext context) => Get(id).CanExecute(context);

    public async Task ExecuteAsync(ActionId id, EditorActionContext context, CancellationToken cancellationToken = default)
    {
        var action = Get(id);
        if (!action.CanExecute(context)) throw new InvalidOperationException($"Action '{id}' cannot execute in the current context.");
        await action.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
    }

    public static ActionRegistry CreateDefault()
    {
        var registry = new ActionRegistry();
        BuiltinActions.Register(registry);
        return registry;
    }
}

public static class BuiltinActionIds
{
    public static readonly ActionId NewProject = new("project.new");
    public static readonly ActionId OpenProject = new("project.open");
    public static readonly ActionId SaveProject = new("project.save");
    public static readonly ActionId SaveProjectAs = new("project.save-as");
    public static readonly ActionId ExportProject = new("project.export");
    public static readonly ActionId Undo = new("edit.undo");
    public static readonly ActionId Redo = new("edit.redo");
}

public sealed record ExportTarget(MyLovePixel.Export.ExportPreset Preset, string OutputDirectory);

public interface IEditorInteraction
{
    Task<string?> PickOpenProjectAsync(CancellationToken cancellationToken);
    Task<string?> PickSaveProjectAsync(DocumentSession session, CancellationToken cancellationToken);
    Task<ExportTarget?> PickExportTargetAsync(DocumentSession session, CancellationToken cancellationToken);
}

public static class BuiltinActions
{
    public static void Register(ActionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.Register(new ActionDescriptor(BuiltinActionIds.NewProject, "New", (context, _) =>
        {
            context.Workspace.NewDocument(64, 64);
            return Task.CompletedTask;
        }, category: "Project"));

        registry.Register(new ActionDescriptor(BuiltinActionIds.OpenProject, "Open", async (context, token) =>
        {
            var path = await context.Interaction.PickOpenProjectAsync(token).ConfigureAwait(false);
            if (path is not null) context.Workspace.Open(path);
        }, category: "Project"));

        registry.Register(new ActionDescriptor(BuiltinActionIds.SaveProject, "Save", async (context, token) =>
        {
            var session = context.Workspace.CurrentSession!;
            var path = session.FilePath;
            if (path is null) path = await context.Interaction.PickSaveProjectAsync(session, token).ConfigureAwait(false);
            if (path is not null) context.Workspace.Save(session, path);
        }, context => context.Workspace.CurrentSession is not null, "Project"));

        registry.Register(new ActionDescriptor(BuiltinActionIds.SaveProjectAs, "Save As", async (context, token) =>
        {
            var session = context.Workspace.CurrentSession!;
            var path = await context.Interaction.PickSaveProjectAsync(session, token).ConfigureAwait(false);
            if (path is not null) context.Workspace.Save(session, path);
        }, context => context.Workspace.CurrentSession is not null, "Project"));

        registry.Register(new ActionDescriptor(BuiltinActionIds.ExportProject, "Export", async (context, token) =>
        {
            var session = context.Workspace.CurrentSession!;
            var target = await context.Interaction.PickExportTargetAsync(session, token).ConfigureAwait(false);
            if (target is not null) context.Workspace.Export(session, target.Preset, target.OutputDirectory);
        }, context => context.Workspace.CurrentSession is not null, "Project"));

        registry.Register(new ActionDescriptor(BuiltinActionIds.Undo, "Undo", (context, _) =>
        {
            context.Workspace.CurrentSession!.Undo();
            return Task.CompletedTask;
        }, context => context.Workspace.CurrentSession?.CanUndo == true, "Edit"));

        registry.Register(new ActionDescriptor(BuiltinActionIds.Redo, "Redo", (context, _) =>
        {
            context.Workspace.CurrentSession!.Redo();
            return Task.CompletedTask;
        }, context => context.Workspace.CurrentSession?.CanRedo == true, "Edit"));
    }
}
