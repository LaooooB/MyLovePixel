# Right drag and hover — 2026-09-26

- Right mouse down starts a captured one-pixel erase gesture; move segments use the existing integer line rasterizer. Mouse-up commits one Undo entry. Escape, lost capture and document switching roll back unfinished gestures.
- Fast movement interpolates the crossed pixels in one batch per segment. Outside-canvas movement breaks the path. Blank pixels produce no history. Locked/hidden/indexed drawing restrictions and eyedropper priority are preserved.
- Chrome surfaces use reversible 140 ms fades on their actual presenters/template borders. Labels and artwork are never globally faded. Each control has its own transition collection.
- Existing tooltip content, placement and delay feed a shared overlay lifecycle. Fade-out completes before removal; reentry reverses it. Escape, click, window deactivation and detach dismiss appropriately.
- The pixel cursor fades on entering/leaving only; pixel tracking within the canvas remains immediate.

Local checks: Release build: 0 warnings/0 errors; 258 unit tests; 42 desktop interaction checks. Native packaged Windows checks are recorded in the release workflow artifact, including real right-drag erasure and one-step Undo.
