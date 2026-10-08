# Canvas navigation and performance — 2026-10-08

Read this after HANDOFF.md and docs/HANDOFF_UX_COMFORT.md. Preserve their document, plugin, palette, and personal-data boundaries.

## Verified delivery

- Product/build commit: `466026c1a0bdc9584d77c95fd8e83dd15210db4e`.
- Feature branch: `perf/canvas-navigation-20261008`.
- Successful Linux and Windows workflow: https://github.com/LaooooB/MyLovePixel/actions/runs/37779357626
- Both platforms passed all 328 solution tests, 33 existing color/UX checks and 27 navigation/performance checks.
- Windows release build: 0 warnings, 0 errors. Published self-contained win-x64 single-file EXE passed native main-window startup and normal shutdown with exit code 0.
- Executable size: 103846994 bytes. SHA-256: `5ee8669dc6272e80348754ffa5bde1ba200e7e1f8d877947b95ded6a30672a1a`.
- Binary artifact: `11551660979`; Windows evidence: `11551790762`; Linux evidence: `11550793665`.
- These checks use Avalonia Headless with real Skia rendering for interactions, plus a separate native Windows EXE startup/shutdown test. They are not manual tests on the user's machine or a guarantee of frame rate on every display.

## Changes

Middle-button drag pans the canvas in both axes, including fitted small images. Navigation is handled before drawing input, captures the pointer, and clears capture on release, Escape or window deactivation. Pan does not paint or create undo history.

Wheel zoom is cursor-anchored, coalesces consecutive/fractional wheel deltas, and eases to the target in 110 ms. Starting a left-button stroke immediately stops camera animation. Fit centers the canvas; saving does not move it. Zoom limits are 1/128 to 128.

Viewport changes no longer rebuild inspector controls, capture full document snapshots, compose the image again or upload unchanged RGBA. Document snapshots and immutable renderer pixels are retained until document mutation. Cache invalidation happens before mutation notifications; edit, undo and redo preserve old immutable snapshots. Plugin rendering/decorations retain their normal semantics.

The desktop bitmap cache retains its writable image and updates changed preview pixels. Minified display levels are generated lazily, reused across zoom frames and invalidated when pixels change. Full-resolution pixels remain the source for magnification, picking and export. Cache rendering crops to the visible viewport. The hidden inspector preview avoids image updates until it is shown.

Hover background/border/text fades run on the actual button template presenter, with explicit resting/selected/primary states and 160 ms cubic easing. Press feedback is immediate and does not move the click target. Tab backgrounds also receive transitions. The regression probe observes intermediate button colors for both enter and leave. The existing 750 ms tooltip dwell remains unchanged.

Pencil, Eraser, Arc, Shadow, Highlight, Fade and Blur brush-size defaults are all 1. This is the starting size; users can still adjust it. Color libraries, folders, temporary swatches, picker and display settings remain intact, with no personal-data migration or project-schema change in this update.

## Measurements and limits

Probe: 24 alternating synchronous zoom updates at 30% / 31%, including dispatcher/layout/render flushes, with newly created 1024 and 2048 canvases. This is a regression workload, not a dense multilayer/effects benchmark or native GPU FPS measurement. Before/after Linux runs used different hosted VMs, so elapsed-time comparisons are indicative.

| Linux workload | Prior UX build total / median | Updated total / median | Cumulative managed allocations before → after |
| --- | --- | --- | --- |
| 1024 square, 24 updates | 1835.4387 / 77.2657 ms | 512.3085 / 20.1308 ms | 2097781272 → 799312 bytes |
| 2048 square, 24 updates | 2298.3369 / 95.6887 ms | 1076.1756 / 44.9285 ms | 8137591136 → 772200 bytes |

Updated Windows results: 1024 total 399.1746 ms, median 16.3264 ms, p95 20.7679 ms, 795712 bytes allocated; 2048 total 718.3182 ms, median 27.3191 ms, p95 40.9989 ms, 770152 bytes allocated. Allocations are cumulative UI-thread managed allocations, not peak RAM. Do not translate these measurements into guaranteed 60/120 Hz performance.

Before-change regression run: `37773692420`, artifact `11548559813`. It reproduced missing middle pan, snapshot/pixel-storage churn, and four effect-brush defaults of 3. The new tests fail against that old product source.

## Continuing development

Run the solution tests and both UI probes. The existing UX probe requires a clean isolated profile and refuses existing personal data. Never delete real palettes to run it.

```sh
dotnet build MyLovePixel.slnx -c Release
dotnet test MyLovePixel.slnx -c Release --no-build
dotnet run --project tests/MyLovePixel.UxSmoke -c Release -- artifacts/ux-evidence
dotnet run --project tests/MyLovePixel.PerformanceSmoke -c Release -- artifacts/performance
```

New tests: tests/MyLovePixel.Application.Tests/NavigationPerformanceTests.cs and tests/MyLovePixel.PerformanceSmoke. Performance assertions use structural cache/identity and allocation checks; timing metrics are reported rather than enforcing host-specific deadlines.

Avalonia Skia cannot CreateScaledBitmap directly from WriteableBitmap. The resolution cache deliberately draws into a RenderTargetBitmap instead. Preserve premultiplied-alpha filtering and dispose reduced levels on source changes.

No signing was performed. Updates should close the old app and preserve `%LOCALAPPDATA%\MyLovePixel`; no real user files were accessed in development.
