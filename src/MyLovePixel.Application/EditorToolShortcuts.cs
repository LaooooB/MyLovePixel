namespace MyLovePixel.Application;

/// <summary>The same bindings drive tool labels, accessibility hints and keyboard routing.</summary>
public static class EditorToolShortcuts
{
    private static readonly (string Tool, string Key)[] Bindings =
    [
        ("core.pencil", "B"), ("core.eraser", "E"), ("core.line", "L"),
        ("core.shape", "U"), ("core.fill", "G"), ("core.eyedropper", "I"),
        ("workspace.selection", "M"),
    ];

    public static string? ForTool(string toolId) => Bindings.FirstOrDefault(b => b.Tool == toolId).Key;
    public static string? ForKey(string key) => Bindings.FirstOrDefault(b => string.Equals(b.Key, key, StringComparison.OrdinalIgnoreCase)).Tool;
}
