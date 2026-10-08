using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace MyLovePixel.Desktop;

/// <summary>Keeps hover content attached until fade-out has finished.</summary>
internal static class HoverToolTips
{
    private static readonly ConditionalWeakTable<Control, TipState> States = new();
    private static TipState? _active;

    public static void Install()
    {
        ToolTip.TipProperty.Changed.AddClassHandler<Control>((owner, change) =>
        {
            if (ToolTip.GetTip(owner) is not null) _ = States.GetValue(owner, c => new TipState(c));
            else if (States.TryGetValue(owner, out var state)) state.Hide(true);
        });
        ToolTip.ToolTipOpeningEvent.AddClassHandler<Control>((owner, e) =>
        {
            // The native popup closes synchronously and cannot fade out. The same
            // attached content/placement/delay is presented in our window overlay.
            e.Cancel = true;
            if (ToolTip.GetTip(owner) is not null) States.GetValue(owner, c => new TipState(c)).Show();
        });
    }

    private sealed class FadingTip : ToolTip
    {
        public FadingTip() => PseudoClasses.Set(":open", true);
        protected override Type StyleKeyOverride => typeof(ToolTip);
    }

    private sealed class TipState
    {
        private readonly Control _owner;
        private Popup? _popup;
        private ToolTip? _tip;
        private IDisposable? _timer;
        private TopLevel? _root;
        private bool _suppressed;
        private bool _closing;
        private int _generation;

        internal TipState(Control owner)
        {
            _owner = owner;
            owner.PointerEntered += Enter;
            owner.PointerExited += Exit;
            owner.PointerPressed += Press;
            owner.DetachedFromVisualTree += (_, _) => Hide(true);
        }

        private void Enter(object? sender, PointerEventArgs e)
        {
            _suppressed = false;
            _timer?.Dispose(); _timer = null;
            if (_popup?.IsOpen == true) { Show(); return; }
            var delay = Math.Max(0, ToolTip.GetShowDelay(_owner));
            _timer = DispatcherTimer.RunOnce(Show, TimeSpan.FromMilliseconds(delay));
        }
        private void Exit(object? sender, PointerEventArgs e) => Hide(false);
        private void Press(object? sender, PointerPressedEventArgs e) { _suppressed = true; Hide(true); }
        private void Key(object? sender, KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Escape) { _suppressed = true; Hide(false); }
        }
        private void Deactivated(object? sender, EventArgs e) { _suppressed = true; Hide(true); }

        internal void Show()
        {
            if (_suppressed || !_owner.IsEffectivelyVisible || !_owner.IsPointerOver || ToolTip.GetTip(_owner) is not { } content) return;
            _timer?.Dispose(); _timer = null;
            var generation = ++_generation;
            _closing = false;
            if (_popup?.IsOpen == true)
            {
                _tip!.Opacity = 1;
                return;
            }
            _active?.Hide(true);
            _active = this;
            _root = TopLevel.GetTopLevel(_owner);
            if (_root is null) return;
            _root.AddHandler(InputElement.KeyDownEvent, Key, RoutingStrategies.Tunnel, handledEventsToo: true);
            if (_root is Window window) window.Deactivated += Deactivated;
            _tip = new FadingTip
            {
                Content = content is ToolTip supplied ? supplied.Content : content,
                Opacity = 0,
                IsHitTestVisible = false,
                MaxWidth = Math.Min(360, Math.Max(100, _root.Bounds.Width - 24)),
                Transitions = new Transitions { EditorMotion.OpacityFade(Visual.OpacityProperty) },
            };
            _popup = new Popup
            {
                PlacementTarget = _owner,
                Placement = ToolTip.GetPlacement(_owner),
                HorizontalOffset = ToolTip.GetHorizontalOffset(_owner),
                VerticalOffset = Math.Max(6, ToolTip.GetVerticalOffset(_owner)),
                ShouldUseOverlayLayer = true,
                IsLightDismissEnabled = false,
                TakesFocusFromNativeControl = false,
                IsHitTestVisible = false,
                Child = _tip,
            };
            ((Avalonia.Controls.ISetLogicalParent)_popup).SetParent(_owner);
            _popup.IsOpen = true;
            // Let the zero-opacity frame enter the visual tree before targeting 1.
            _timer = DispatcherTimer.RunOnce(() =>
            {
                if (_generation == generation && !_closing && _tip is not null) _tip.Opacity = 1;
            }, TimeSpan.FromMilliseconds(16));
        }

        internal void Hide(bool immediate)
        {
            _timer?.Dispose(); _timer = null;
            var generation = ++_generation;
            if (_popup is null) return;
            if (immediate) { Close(); return; }
            _closing = true;
            _tip!.Opacity = 0;
            _timer = DispatcherTimer.RunOnce(() =>
            {
                if (_generation == generation) Close();
            }, EditorMotion.FadeDuration + TimeSpan.FromMilliseconds(20));
        }

        private void Close()
        {
            _timer?.Dispose(); _timer = null;
            if (_root is not null)
            {
                _root.RemoveHandler(InputElement.KeyDownEvent, Key);
                if (_root is Window window) window.Deactivated -= Deactivated;
            }
            if (_popup is not null) { _popup.IsOpen = false; _popup.Child = null; ((Avalonia.Controls.ISetLogicalParent)_popup).SetParent(null); }
            if (_tip is not null) _tip.Content = null;
            _popup = null; _tip = null; _root = null; _closing = false;
            if (ReferenceEquals(_active, this)) _active = null;
        }
    }
}
