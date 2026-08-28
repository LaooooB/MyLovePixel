using MyLovePixel.Commands;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Raster;

namespace MyLovePixel.Tools;

public sealed class ToolHost
{
    private readonly IToolDocumentReader _document;
    private readonly CommandBus _commands;
    private RasterWorkBudget _workBudget;
    private KeyModifiers _keyboardModifiers;

    public ToolHost(
        IToolDocumentReader document,
        CommandBus commands,
        ToolTarget target,
        ITool initialTool,
        Rgba32? primaryColor = null,
        Rgba32? secondaryColor = null,
        RasterWorkBudget? workBudget = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        Target = target;
        ActiveTool = initialTool ?? throw new ArgumentNullException(nameof(initialTool));
        PrimaryColor = primaryColor ?? new Rgba32(0, 0, 0, 255);
        SecondaryColor = secondaryColor ?? new Rgba32(255, 255, 255, 255);
        _workBudget = workBudget ?? RasterWorkBudget.Default;
        Options = ActiveTool.Descriptor.Options.CreateDefaults();
    }

    public ITool ActiveTool { get; private set; }
    public ToolTarget Target { get; private set; }
    public ToolOptions Options { get; private set; }
    public Rgba32 PrimaryColor { get; private set; }
    public Rgba32 SecondaryColor { get; private set; }
    public ToolPreview? Preview { get; private set; }
    public KeyModifiers KeyboardModifiers => _keyboardModifiers;

    public void SetActiveTool(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (ReferenceEquals(ActiveTool, tool)) return;
        CancelInteraction();
        ActiveTool = tool;
        Options = tool.Descriptor.Options.CreateDefaults();
        Preview = null;
    }

    public void SetTarget(ToolTarget target)
    {
        if (Target == target) return;
        CancelInteraction();
        Target = target;
        Preview = null;
    }

    public void SetColors(Rgba32 primaryColor, Rgba32 secondaryColor)
    {
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
    }

    public void SetWorkBudget(RasterWorkBudget workBudget)
    {
        _workBudget = workBudget ?? throw new ArgumentNullException(nameof(workBudget));
    }

    public void SetOption(string id, object value)
    {
        if (ActiveTool.IsInteracting)
            throw new InvalidOperationException("Tool options cannot change during an active interaction.");
        Options = Options.With(id, value);
    }

    public void SetKeyboardModifiers(KeyModifiers modifiers) =>
        _keyboardModifiers = modifiers;

    public ToolDispatchResult Dispatch(PointerEvent pointerEvent)
    {
        if (pointerEvent.Kind == PointerEventKind.Cancelled)
            return CancelInteraction();

        var effectiveModifiers = pointerEvent.Modifiers | _keyboardModifiers;
        var effectiveEvent = pointerEvent.WithModifiers(effectiveModifiers);

        try
        {
            var result = ActiveTool.HandlePointer(CreateContext(), Options, effectiveEvent);
            Preview = result.Committed ? null : result.Preview;
            return result;
        }
        catch
        {
            if (!ActiveTool.IsInteracting)
                Preview = null;
            throw;
        }
    }

    public ToolDispatchResult CancelInteraction()
    {
        if (!ActiveTool.IsInteracting)
        {
            Preview = null;
            return ToolDispatchResult.Cleared;
        }

        var result = ActiveTool.Cancel(CreateContext());
        Preview = null;
        return result;
    }

    private ToolContext CreateContext() =>
        new(
            _document,
            _commands,
            Target,
            PrimaryColor,
            SecondaryColor,
            _workBudget);
}
