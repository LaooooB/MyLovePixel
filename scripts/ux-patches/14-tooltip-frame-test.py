from pathlib import Path
import zipfile
p=Path('tests/MyLovePixel.Desktop.UxTests/InteractionPolishTests.cs')
s=p.read_text(encoding='utf-8')
old='''            PumpFor(70);
            var tip = w.GetVisualDescendants().OfType<ToolTip>().FirstOrDefault(t => t.IsEffectivelyVisible);
            Check(tip is not null, "The tooltip did not open in the window overlay.");
            Check(tip!.Opacity > 0 && tip!.Opacity < 1, "Tooltip fade-in did not produce an intermediate opacity.");
            PumpFor(210); Check(tip!.Opacity > .99, "Tooltip never reached full opacity.");
            MoveWithoutSettling(w, new Point(w.Bounds.Width / 2, 15)); PumpFor(55);
            Check(tip!.IsAttachedToVisualTree() && tip!.Opacity > 0 && tip!.Opacity < 1, "Tooltip vanished before fade-out completed.");
            PumpFor(200); Check(!tip!.IsAttachedToVisualTree(), "Faded tooltip remained attached.");'''
new='''            ToolTip? tip = null;
            // Observe actual rendered frames: a loaded Windows worker may spend
            // the old fixed 70 ms creating the popup, before its animation starts.
            Check(ObserveAnimation(() =>
            {
                tip = w.GetVisualDescendants().OfType<ToolTip>().FirstOrDefault(t => t.IsEffectivelyVisible);
                return tip is { Opacity: > 0 and < 1 };
            }), "Tooltip fade-in did not produce an intermediate opacity.");
            PumpFor(210); Check(tip!.Opacity > .99, "Tooltip never reached full opacity.");
            MoveWithoutSettling(w, new Point(w.Bounds.Width / 2, 15));
            Check(ObserveAnimation(() => tip!.IsAttachedToVisualTree() && tip!.Opacity > 0 && tip!.Opacity < 1),
                "Tooltip vanished before fade-out completed.");
            PumpFor(200); Check(!tip!.IsAttachedToVisualTree(), "Faded tooltip remained attached.");'''
assert s.count(old)==1
s=s.replace(old,new)
needle='    private static void PumpFor(int milliseconds)\n'
helper='''    private static bool ObserveAnimation(Func<bool> condition)
    {
        var observed = false;
        var frame = new Avalonia.Threading.DispatcherFrame();
        var render = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) };
        render.Tick += (_, _) =>
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            if (condition()) { observed = true; frame.Continue = false; }
        };
        using var end = Avalonia.Threading.DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromSeconds(2));
        render.Start();
        try { Avalonia.Threading.Dispatcher.UIThread.PushFrame(frame); }
        finally { render.Stop(); }
        return observed;
    }

'''
assert s.count(needle)==1
s=s.replace(needle,helper+needle)
p.write_text(s,encoding='utf-8',newline='\r\n' if b'\r\n' in p.read_bytes() else '\n')
with zipfile.ZipFile('release/review-source.zip','w',zipfile.ZIP_DEFLATED) as z:
    for directory in ['src','tests','docs','.github','scripts']:
        for p in Path(directory).rglob('*'):
            if p.is_file() and not any(v in p.parts for v in ['bin','obj','__pycache__','ux-patches']): z.write(p,p.as_posix())
    for name in ['Directory.Build.props','Directory.Packages.props','MyLovePixel.slnx','global.json','HANDOFF.md','README.md','THIRD_PARTY_NOTICES.md']:
        if Path(name).is_file(): z.write(name)
