# One-pixel brushes and larger white Preview

Base: named-palette UX release `e84cc2c3a85e10eb04468ee07fbd117ada0ee9d7`, branch `ux-release-20260926`.

All nine built-in tools with Brush Size now initialize at 1 pixel. Brush size remains adjustable from 1 to 64; no user artwork is resized.

The Preview header has a persistent Enlarge button. It opens one resizable, non-modal live window, so the editor remains usable. Double-clicking the sidebar image opens the same window. Out/In zoom, the percentage resets to 100%, and Fit shows the whole image. The wheel zooms at the pointer; dragging pans. F fits, 1 resets, +/- zoom, and Escape closes the larger window. Zoom is independent of the main canvas.

The preview and frame thumbnails use pure white behind the unchanged RGBA composite. Preview excludes onion skin, canvas grid, selection, transient overlays and inverted-view decoration. The editor's transparency checker and exported alpha are unchanged. Switching documents fits the preview again; edits and frame changes update an already-open preview.

At very short window heights, the sidebar keeps Enlarge and a small white preview visible; all zoom controls remain available in the larger window. No project/palette format or storage changes were made.

Regression commands: `dotnet build MyLovePixel.slnx -c Release`; `dotnet test MyLovePixel.slnx -c Release --no-build`; `dotnet run --project tests/MyLovePixel.Desktop.UxTests -c Release`. The Windows packaging smoke test also opens/reopens the real EXE's Preview, checks zoom controls and samples the white background from screen pixels.
