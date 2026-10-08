using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MyLovePixel.Application;
using MyLovePixel.Desktop;

namespace MyLovePixel.PerformanceSmoke;

internal static class InspectorChecks
{
    public static void Run(MainWindow window, Action<bool, string> check)
    {
        var tabs = window.GetVisualDescendants().OfType<TabControl>().First(t => AutomationProperties.GetAutomationId(t) == "inspector-tabs");
        tabs.SelectedIndex = 2; Flush();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var panel = (StackPanel)typeof(MainWindow).GetField("_layersPanel", flags)!.GetValue(window)!;
        var workspace = (EditorWorkspace)typeof(MainWindow).GetField("_workspace", flags)!.GetValue(window)!;
        var session = workspace.CurrentSession!;
        var initialCount = session.GetLayers().Count;
        var add = panel.GetVisualDescendants().OfType<Button>().First(b => ToolTip.GetTip(b)?.ToString() == "Add layer");
        var p = add.TranslatePoint(new Point(add.Bounds.Width / 2, add.Bounds.Height / 2), window)!.Value;
        window.MouseDown(p, MouseButton.Left); window.MouseUp(p, MouseButton.Left); Flush();
        check(session.GetLayers().Count == initialCount + 1 && panel.Children.OfType<Grid>().Count() == initialCount + 1, "Focused Add Layer button updates visible rows immediately");
        var hide = panel.GetVisualDescendants().OfType<Button>().First(b => ToolTip.GetTip(b)?.ToString() == "Hide");
        p = hide.TranslatePoint(new Point(hide.Bounds.Width / 2, hide.Bounds.Height / 2), window)!.Value;
        window.MouseDown(p, MouseButton.Left); window.MouseUp(p, MouseButton.Left); Flush();
        check(panel.GetVisualDescendants().OfType<Button>().Any(b => ToolTip.GetTip(b)?.ToString() == "Show"), "Layer visibility button updates without requiring a click elsewhere");
    }
    private static void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
