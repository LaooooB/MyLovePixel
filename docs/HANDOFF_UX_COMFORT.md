# MyLovePixel · 2026-10-08 UX / Color Library Handoff

This supplements the architectural rules in the root HANDOFF.md. Read the live branch and Actions state before continuing development.

## Verified source and executable

- Feature branch: `ux/color-library-comfort`.
- Source preservation commit: `69f5edd770f8617241e5c98da3718d23af25033b`.
- Build inputs commit: `6da44bb7089b7f12d7755b8e4f16e9d3b55cb8c0`; the verified generated source was subsequently committed as ordinary C# files. The one-time integration scripts were removed by the preservation commit. Normal builds do not need them.
- Successful Linux + Windows workflow: https://github.com/LaooooB/MyLovePixel/actions/runs/37744990021
- Linux Release build: succeeded; solution tests: 318 passed, 0 failed; headless UX checks: 33 passed.
- Windows Server 2025 Release build: 0 compiler warnings, 0 errors; solution tests: 318 passed, 0 failed, 0 skipped; headless UX checks: 33 passed.
- Published Windows x64 self-contained single-file executable was started as a native process. Main window title was `MyLovePixel — Untitled`; normal close and exit code 0 were verified.
- Executable: `MyLovePixel.Desktop.exe`, 103834706 bytes.
- EXE SHA-256: `bfec0937bfeee2e3e1ffb1161a5c11a861c368a66ccc343580cba6d2a42a1d02`.
- Windows binary artifact: `11535299431`; Windows screenshot/checks artifact: `11535378548`; Linux source/screenshots/checks artifact: `11534699084`.
- SDK used by CI: .NET 10.0.401. Bundled runtime config specifies Microsoft.NETCore.App 10.0.12.

## What changed

### Hover and focus

Tooltips require a 750 ms dwell on every control, including when moving from a previous tooltip. Button background/border feedback transitions take 120 ms. Existing selected states remain distinguishable on hover. Tool names are permanently visible in the left rail. New controls have text labels and automation names/IDs.

Selecting a color changes its selection styling in place; it does not rebuild the selected button and discard keyboard focus. Search typing does not invoke drawing shortcuts. New document / color target changes synchronize the active HEX. Escape exits eyedropper; picker Escape and Cancel restore its original color. Clicking outside the picker keeps the preview without unconditionally stealing focus back from the clicked control.

### Light canvas backdrop

The always-visible Backdrop slider above the canvas ranges 0–100, default 60, with Reset. Both endpoints remain neutral gray rather than switching to a dark theme. It updates the canvas transparency backdrop and preview surroundings; saves are debounced and flushed on close.

The checker is a fixed 12-DIP cell pattern with a 12/255 luminance-channel difference. It is independent of source-pixel zoom. Rendering uses a cached WriteableBitmap, high-quality minification and nearest-neighbor magnification instead of issuing one draw call per source pixel. Display buffers are copies; original RGBA, picking and exports retain their original semantics. Opaque white artwork is intentionally not dimmed by the backdrop slider.

### Color workspace

Inspector tabs are Edit / Colors / Layers / Advanced. Colors has Saved colors and Temporary sub-tabs. The preview is hidden while Colors is open, and the timeline occupies only the canvas/tool side. Search, folder choice and active-color controls stay outside the swatch scroll area. Low-frequency RGB/folder/independent-HEX controls use explicit click menus; the fixed 512-color grid and redundant Photo tab are removed.

Search matches HEX, color name and folder name. Folder IDs and color IDs are stable. Saving an existing color preserves its name and folder. New saves stay in the selected folder; a save reveals an existing color's actual folder when necessary. Removing a folder unfiles its colors without deleting them. Destructive library actions require confirmation with Cancel as the initial focus.

Picker supports captured SV dragging, keyboard arrows with Shift for larger steps, hue and opacity sliders, preview and Cancel / Done. Transparent swatches display over a checker. I selects eyedropper, B returns to pencil, Alt-click temporarily picks original canvas RGBA. Add temp can be used repeatedly without forcing a tab switch. Temporary colors persist until explicitly removed and are not silently evicted or reordered.

Photo conversion remains in the Import click menu; sprite-sheet slicing is under Advanced → Animation.

## Persistence boundaries

Files live in the same stable per-user MyLovePixel application-data folder, not beside the EXE:

- Windows root: `%LOCALAPPDATA%\MyLovePixel`.
- `user-palette.json`: original palette; migration reads it without changing its bytes.
- `color-library.json`: schema 1, names/folders/saved/temporary colors. Legacy colors are imported once when the library is first created.
- `display-settings.json`: schema 1, backdrop brightness.

The new stores validate before publishing, use an exclusive lease and reread before editing, write-through temporary files and same-directory replacement. The previous good version is backed up. Corrupt/future formats block overwrites. Missing primary plus existing backup blocks silent initialization. Explicit color-library recovery retains damaged bytes and refuses to overwrite a primary another window has already repaired.

Limits: 16384 saved colors, 256 folders, 512 temporary slots. Library edits and display preferences never change `.pixelproj`, document undo history or Core mutation rules. No project-schema change was made.

Users should close the old EXE and preserve the complete application-data folder during upgrades. Do not alternate old/new versions for palette editing: edits made to the old palette after its one-time import are not automatically merged into the new library. The real user's data was not accessed during development; tests use isolated fixtures.

## Verification and maintenance

The UI probe is `tests/MyLovePixel.UxSmoke`. It runs separately from the solution tests and requires a clean isolated user profile. It refuses to run over existing personal palette/library/display-settings files. Do not delete real user data to make this test run.

```sh
dotnet build MyLovePixel.slnx -c Release
dotnet test MyLovePixel.slnx -c Release --no-build
dotnet run --project tests/MyLovePixel.UxSmoke -c Release -- /tmp/ux-evidence
```

The 33 UI checks cover 1080×700 and 1480×920 layouts, minimum swatch viewport, tooltip properties, search/keyboard behavior, folder save context, focus preservation, repeated temporary storage, picker preview/cancel, active-color synchronization, 1024×1024 view invariance, raw RGBA picking under display inversion, and restart persistence. Both platform screenshot sets were inspected. This is not a complete manual accessibility, monitor-calibration, mixed-DPI or long-duration performance audit.

The Skia native Linux dependency is explicitly aligned with managed SkiaSharp 4.151.1. Without the explicit Linux asset version, Avalonia's transitive native 3.119 runtime failed during window initialization despite unit tests passing. Preserve this alignment when upgrading packages.

The executable is unsigned. No code-signing or installer integration was performed. Native Windows smoke verifies startup/shutdown, while interaction checks use Avalonia Headless with actual Skia rendering. Do not describe this as manual testing on the user's computer.
