using Avalonia.Automation;
using Avalonia.Automation.Peers;

namespace MyLovePixel.Desktop;

public sealed partial class PixelCanvasView
{
    protected override AutomationPeer OnCreateAutomationPeer() => new PixelCanvasAutomationPeer(this);

    private sealed class PixelCanvasAutomationPeer(PixelCanvasView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
        protected override string GetClassNameCore() => nameof(PixelCanvasView);
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
    }
}
