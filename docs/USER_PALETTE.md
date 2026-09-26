# HEX input and My palette

The Edit inspector keeps HEX input above the existing 512-color grid. Type `#654321` (or `654321`) and press Enter or Apply. Eight-digit colors use **RRGGBBAA**, with alpha last. Invalid input leaves the drawing color unchanged; Escape restores the last applied color.

**My palette** sits directly below the existing palette. **Save color** saves the entered color immediately. Clicking a saved swatch applies it to the active Primary/Secondary target. **Remove selected** removes only that saved swatch; it does not change artwork or the active drawing color. Duplicate RGBA colors are not added. Up to 512 custom swatches are supported, and their HEX labels are always visible.

## Persistence and failure behavior

Windows preferences path: `%LOCALAPPDATA%\MyLovePixel\user-palette.json`.

The palette is shared across projects and survives application restarts and EXE replacement. It is not inside `.pixelproj`, does not dirty documents and does not enter undo history. JSON is an independent `schemaVersion: 1` preference format; the project schema is unchanged.

Writes use a flushed, same-directory temporary file followed by replacement. A short-lived exclusive `.lock` file handle protects a read/modify/write operation; the empty `.lock` file may remain on disk. Each mutation re-reads the latest palette, preserving changes from another app window. Failed writes are reported and never published as successful in-memory changes. A corrupt, unsupported or inaccessible existing palette is reported and is not overwritten. Close the app and repair/restore that file before retrying.

## Regression coverage

Application tests cover RGB/RGBA parsing, invalid text, canonical formatting, initial empty state, save/reopen order and alpha, duplicate handling, removal, separate-window updates, invalid/newer preference formats, busy-file failure, capacity and read-only snapshots. Windows packaging retains the existing full build/test, self-contained publication, checksum and startup smoke-test gates.
