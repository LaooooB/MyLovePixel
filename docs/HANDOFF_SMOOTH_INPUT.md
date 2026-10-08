# Smooth input and 5572f97 UX reference — 2026-10-08

Read after HANDOFF.md, HANDOFF_UX_COMFORT.md and HANDOFF_CANVAS_PERFORMANCE.md. Preserve all Core/Commands, plugin, project and personal-data boundaries.

## Verified delivery

- Build/source commit: `04f19fa4d71792896740c3cfa70381990c32bbeb`.
- Feature branch: `perf/input-latency-20261008`.
- UX reference inspected: `5572f9735a71dd663d292a914be040aa8638d864`.
- Successful Windows workflow: https://github.com/LaooooB/MyLovePixel/actions/runs/37787801755
- Desktop Release build succeeded. Existing color/workspace probe: 33 passed. Navigation, hover, stable-control and inspector-button probe: 32 passed, zero failures.
- Published win-x64 self-contained single-file EXE opened a native main window titled `MyLovePixel — Untitled`, entered its message loop and closed normally with exit code 0.
- EXE size: 103859282 bytes. SHA-256: `666b28bcf41309a464f57fd8e5b2e4cfeaf0ff7f1610c7afb97b6d22864f8985`.
- Binary artifact: `11555565391`; exact source and checks artifact: `11555047489`.
- Binary ZIP and executable hashes were verified after download; compiled source was compared to the reviewed local source.

## UX comparison and changes

The reference separates stable/lazy inspector refresh, cached checker rendering, template-level motion and delayed tooltip removal. These parts were adapted; the whole older product was not restored. Its separate enlarged preview window was inspected but was not ported in this update.

Middle-button pan now updates a retained transform instead of ScrollViewer layout at each pointer move. Scroll offsets are committed on gesture end; capture clears on release, Escape and deactivation. The visible drawing keeps a guard band and is refreshed when newly exposed content exceeds that band. Navigation remains separate from painting and undo.

Wheel zoom updates a scale/translation around the cursor. The animation path no longer forces layout on each frame; one layout commits final source-pixel coordinates. Beginning a stroke stops the camera animation. The 75 ms easing is interruptible. Space+left navigation is scoped to canvas/window keyboard focus so form controls retain Space activation.

The transparency checker uses a reused tiny raster tile instead of rerasterizing a vector DrawingBrush. Existing neutral backdrop brightness is retained. Native Skia resource-cache budget is 256 MiB; this is a cache limit, not a promise of fixed RAM usage or GPU frame rate.

Tool and unchanged timeline controls retain identity. Options/layers rebuild on their relevant signature changes. Hidden effects, tilemap, animation, plugin and recovery panels refresh when visible and changed. Active text/numeric/combo editing defers inspector rebuilding, then refreshes after focus leaves; a focused action button does not block its visible result. Add Layer and visibility feedback were exercised by mouse input.

EditorMotion and HoverToolTips are adapted from the reference: 140 ms cubic fades on actual template controls; the tooltip popup remains attached through its fade-out. Existing explicit button pressed/selected styling and 750 ms tooltip dwell are retained. Swatch color itself remains unchanged.

Synchronous recovery checkpoints are postponed while panning, zooming, drawing or making a selection gesture. They continue through the original autosave timer afterward. This does not move recovery or the document model to another thread.

## Preserved behavior and limits

Saved colors, folders, temporary swatches, picker, backdrop preferences and all previous one-pixel brush defaults remain. No user-data or project schema changes. Windows data remains under `%LOCALAPPDATA%\MyLovePixel`; no real user files were accessed. Close the old EXE before updating and preserve the full data folder.

Interaction checks use Avalonia Headless with Skia rendering, plus a separate native EXE startup/shutdown check. They are not manual testing on the user's GPU or a long-duration dense-multilayer/effects benchmark. The synchronous SetZoom timing workload does not measure native composited pan/zoom FPS. Do not claim guaranteed 60/120 Hz or complete release certification. No code signing was performed.

The startup probe polls actual main-window readiness with a bounded deadline and records states; a fixed 1.5 second sleep previously yielded an inconclusive startup failure. The final successful native readiness record is in `checks/native-startup.json`; there was no crash log. Product code was unchanged for this probe correction.

## Build and maintenance

`.github/workflows/smooth-package.yml` checks out its exact commit and builds/packages on Windows. The one-time source-transfer patch has been removed; normal builds use ordinary committed C# files. Keep UxSmoke before PerformanceSmoke on an isolated CI profile. Never delete personal palette files to run probes.

```sh
dotnet build src/MyLovePixel.Desktop/MyLovePixel.Desktop.csproj -c Release
dotnet run --project tests/MyLovePixel.UxSmoke -c Release -- artifacts/ux
dotnet run --project tests/MyLovePixel.PerformanceSmoke -c Release -- artifacts/checks
```
