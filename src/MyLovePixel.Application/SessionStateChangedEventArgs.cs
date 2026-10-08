namespace MyLovePixel.Application;

public enum SessionStateChangeKind { Viewport, Colors, ToolOptions, ToolSelection, Preview }

/// <summary>View-only notifications must not rebuild the document or inspector.</summary>
public sealed class SessionStateChangedEventArgs(SessionStateChangeKind kind) : EventArgs
{
    public SessionStateChangeKind Kind { get; } = kind;
    public static SessionStateChangedEventArgs Viewport { get; } = new(SessionStateChangeKind.Viewport);
    public static SessionStateChangedEventArgs Colors { get; } = new(SessionStateChangeKind.Colors);
    public static SessionStateChangedEventArgs ToolOptions { get; } = new(SessionStateChangeKind.ToolOptions);
    public static SessionStateChangedEventArgs ToolSelection { get; } = new(SessionStateChangeKind.ToolSelection);
    public static SessionStateChangedEventArgs Preview { get; } = new(SessionStateChangeKind.Preview);
}

public sealed partial class DocumentSession
{
    public const double MinimumZoom = 1d / 128d;
    public const double MaximumZoom = 128d;
}
