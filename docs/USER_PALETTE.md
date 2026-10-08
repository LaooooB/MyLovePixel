# Colors workspace — October 2026

The right inspector has Edit, Colors, Layers and Advanced tabs. Colors owns the full-height right panel; the timeline sits below the canvas only. The old 512-color grid and Photo/drop-image tab are removed. Photo conversion remains under Edit, and Import remains in the top toolbar.

## Pick and reuse
The upper current-color HEX field accepts RGB/RGBA in **RRGGBBAA** order. **Palette** beside it opens a draggable saturation/brightness pad, hue, alpha and RGB controls. Apply/Enter accepts, Cancel/Escape leaves the previous drawing color unchanged. Primary/Secondary determines the target.

**Eyedropper** or **I** enters sticky sampling. Click artwork to sample the active color target; right-click samples Secondary. Sampling reads artwork data, never a checkerboard, grid or hover decoration, and adds no undo entry. Use B or select a drawing tool to leave sampling. Edit shows a current-layer-only sampling option.

**+ Temp** keeps each sampled color immediately. The Temporary subtab lets you reselect, remove, clear with confirmation, or copy a color to the saved library. The rack persists across restarts until manually cleared. It holds up to 128 unique RGBA colors and never evicts an old sample to make room.

## Find and organize
Saved colors supports case-insensitive search by name, HEX and folder, All colors/Unfiled/folder filtering, 48 colors per page, and a separately scrollable swatch area. New/Rename/Delete manages folders. Edit / Move changes a saved color's optional name and folder atomically. Deleting a folder moves its colors to Unfiled; it never deletes them. Save current immediately reveals the color in its actual folder, including an existing duplicate. Maximum library size is 16,384 RGBA colors and 512 folders.

## Upgrade safety
The existing Windows path is unchanged:
`%LOCALAPPDATA%\MyLovePixel\user-palette.json`

The file is independent of the EXE location and project files. Replacing/downloading the EXE on the same Windows account reuses this location. Close old versions before opening the new EXE. Do not delete this AppData folder. A different PC/account requires copying this folder separately; no cloud sync is implied.

Version-1 colors load losslessly, retaining order and all RGBA bytes. Opening the app alone does not rewrite the file. The first successful change creates a byte-exact `user-palette.json.pre-v2.bak` and writes version 2. Each successful later replacement retains the previous valid generation in `user-palette.json.bak`. New-version preferences cannot be edited by the old version.

A same-directory temporary file is flushed and validated before atomic replacement. The read/modify/write sequence holds an exclusive `.lock` handle and re-reads the latest disk state, preserving changes from another window. Failed writes never publish a successful in-memory change. Corrupt, unsupported, invalidly typed or inaccessible files produce a visible error and are never replaced with empty data. Restore a known-good backup while the app is closed; no destructive automatic repair is attempted.

## Verification
Application regression tests cover migration, backup generations, capacity, alpha, invalid schema types, interleaved windows, folder edits/deletion, query behavior, temporary racks and sampling. The desktop smoke harness opens a real window with 600 legacy colors, operates actual dialogs and buttons, sends native pointer drag events to the spectrum, and checks 1480×920 and 1080×700 layouts. All fixtures use isolated temporary paths; users' real local palettes are neither read nor included in builds.
