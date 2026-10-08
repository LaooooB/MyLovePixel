# Named personal palette

Release base: `f4985b163edabf9db8a19814e75297bf2efb09f2` on `ux-release-20260926`. This release extends the complete UX build; it does not replace it with the older `main` UI.

In the fixed color editor, enter a HEX value and choose **Save…** to open the name field. **My palette** is directly below **Color library** on the Edit tab. It also accepts HEX entry directly. Name and HEX stay visible beside every saved swatch. Click a swatch to use it, **Rename** to focus its name, and **Save name** to persist the edit. **Remove** deletes only that personal swatch.

Names support Unicode, up to 64 characters. A blank name uses the HEX value. Duplicate RGBA entries are not added; saving an existing color edits its name. Transparent colors keep their alpha. Edits do not change document data, dirty status or artwork undo history. Existing indexed-mode color restrictions are retained.

Preferences: `%LOCALAPPDATA%\MyLovePixel\user-palette.json`. Schema 2 stores `colors: [{ hex, name }]`. The previous schema 1 string palette is read without alteration and is migrated on its next edit, keeping a `.v1.bak` copy. Corrupt or unsupported data is reported and never silently overwritten. Each mutation reloads under a local file lease and writes through a same-directory replacement.

Preview is expanded by default and stays expanded when the window becomes compact; it is height-adaptive and may still be manually collapsed.

Keep the existing continuous right erase, eyedropper, hover motion, accessibility, undo, timeline and save/recovery regression suites enabled. The native Windows smoke test also enters a Unicode name, renames it, restarts the packaged EXE and checks that the named color still applies.
