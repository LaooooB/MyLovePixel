# Color library upgrade

User asked to implement and deliver EXE directly, continuing existing work.

Keep existing `%LOCALAPPDATA%/MyLovePixel/user-palette.json`, project format, app identity,
recovery location, drawing commands and tool shortcuts. Read legacy v1 without changing
bytes. Before first v2 write, preserve `.pre-v2.bak`; each replacement keeps previous
valid state in `.bak`. Corrupt or unsupported files must not be overwritten. Continue
exclusive read/modify/write lease and commit in-memory state only after durable writes.

Replace 512-color stock grid with an on-demand HSV picker next to current HEX input.
Drag a small marker in the saturation/value square; hue and alpha sliders, HEX, Apply,
Cancel. Primary/Secondary targets preserved. Remove Photo tab, retain existing Import
and access to photo-to-pixel through Edit as a dialog (avoid losing its existing function).

Colors is its own right-hand tab. Saved colors support name/HEX/folder search, flat
folders, creation/rename/deletion, moves and renaming. Deleting a folder moves colors
to Unfiled. All/Unfiled folders cannot be renamed/deleted. Paginate 48 saved swatches
per page so thousands of colors do not create thousands of controls.

Temporary colors support save-current, select, remove, clear with confirmation and
promotion to the library. Preserve temporary colors on restart until explicitly cleared.
Limit permanent entries to 16,384 and temporary to 128 with visible errors, no eviction.

Test migration byte preservation, backup rotation, alpha, folder/search semantics,
interleaved windows, invalid metadata, failed writes, and HSV round trips. Build and
run full suite, publish self-contained Windows x64 EXE, run packaged startup and UI
smoke tests. Report accurately what is and is not tested on the user's own computer.
