# Color Studio

Base: the delivered `5572f9735a71dd663d292a914be040aa8638d864` build. No newer/default-branch product code was merged into this update.

## Use

The right inspector has Colors, Layers, Tools, Effects, Tiles, Preview and More tabs. The redundant Photo/drop-image tab and the 512-swatch wall are no longer displayed; the existing Import action remains. Preview retains the white background and independent enlarged live window. Open large preview is always available at the bottom of the inspector.

Colors has the current foreground/background and upper HEX input. Pick opens a draggable HSV field with a hue strip, opacity and exact HEX. Use color confirms; Cancel preserves the starting drawing color. RGBA opens the compact channel controls. Keep adds the current drawing/eyedropper color to Quick colors; clicking a slot reuses it. Quick Save names it in My palette, without removing the slot.

Search matches custom names, HEX and folder names, ignoring case. Space-separated words are combined. All colors and Unfiled are built-in filters. Folders creates or renames folders. Move assigns the selected saved color. Removing a folder moves its colors to Unfiled and never deletes them. New / Save opens the naming form; successful Save returns to the list. Rows keep names and HEX visible, wrapping long names. The results list is virtualized and a changed search/folder resets the result scroll position.

## Existing user data

The preferences path is unchanged: `%LOCALAPPDATA%\MyLovePixel\user-palette.json`. Replacing the EXE on the same Windows account reads this existing file. No palette data is bundled in the executable or copied over the user's file. Artwork files, recovery settings and exported transparency are unchanged.

Versions 1 (HEX strings) and 2 (named colors) are readable without rewriting on launch. The first successful mutation writes version 3 with named colors, stable folder IDs and quick slots. Before that write, the original bytes are preserved as `.v1.bak` or `.v2.bak`; that migration backup is never replaced. A rolling `.bak` also preserves the last pre-write file. A write is staged in a same-directory flushed temporary file, then atomically replaced under an exclusive read/modify/write lease. Another instance's changes are read before each mutation. Invalid or unsupported data is reported and not overwritten. The last good in-memory snapshot stays available on failed reload.

The preferences schema does not change `.pixelproj`. Saving, renaming, organizing and retaining colors do not mark artwork dirty or enter artwork undo. The palette supports 8192 saved RGBA colors, 256 folders and 128 quick slots; duplicates are not added and full quick slots are never silently evicted. Old EXEs cannot read the new schema; use the updated EXE. The migration backup can restore an old-format palette when deliberately returning to an old application version.

## Verification

`dotnet build MyLovePixel.slnx -c Release`

`dotnet test MyLovePixel.slnx -c Release --no-build`

`dotnet run --project tests/MyLovePixel.Desktop.UxTests -c Release`

`scripts/Test-UxRelease.ps1 -Executable <packaged-exe> -OutputDirectory <evidence-directory>`

Coverage includes byte-exact legacy backup, 512-name migration, 1200-color legacy loading, folder lifecycle, Unicode/HEX search, concurrent instances, quick-slot persistence, HSV round trips, actual picker dragging/cancel/confirm, 3000-color virtualization and post-scroll search, high-DPI control reachability, plus previous drawing, erase, hover, named-palette, brush and preview regressions. The native Windows test seeds a 64-color version-2 file and verifies migration, existing values/order, folder creation, Unicode names, restart, search, retained quick colors and the packaged picker and preview.
