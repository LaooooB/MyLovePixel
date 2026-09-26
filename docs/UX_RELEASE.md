# UX release — 2026-09-26

Scope: complete the approved P0/P1 usability pass. No new large modules, project schema changes, or plugin API changes. Main baseline: 26b8a0b. Work continues on ux-release-20260926.

| Area | Implementation / regression coverage |
| --- | --- |
| Eyedropper | I and held Alt; visible composite / raw current layer; exact RGBA and indexed identity; excludes overlays; read-only; right button cannot erase while sampling. |
| Controls | Persistent sidebar names, real shortcut catalog, accessible names, visible toggle state, focus outlines, readable primary-action contrast. |
| Workspace | Responsive labeled rail and inspector; compact View menu; timeline/preview collapse at short heights; scrollable sections; 125/150/200% rendering tests. |
| Canvas | Mouse-anchored zoom, fit/100%, space/middle-button panning, brush footprint, cancel-safe capture, visible and hittable edge-transform handles. |
| Colors | Foreground/background targets, RGBA/HEX, inline validation, checkerboard transparency, bounded color library, exact indexed palette matching. |
| Layers / timeline | Stable rows, rename on double click/F2, visible lock/opacity %, frame thumbnails and linked/empty state, paging beyond 24 frames, explicit playback state. |
| Editing | Local refresh preserves controls, focus and scroll state; context-aware shortcuts; parameter gestures form one undo unit and Esc rolls back. Clear Frame preserves locked layers and other linked frames; an empty clear is a no-op. |
| Documents | Separate durable errors and coordinate status; disabled document-only actions in an empty workspace; save/discard/cancel on close; exact dirty savepoints; guarded asynchronous file tasks. |
| Recovery | Detached snapshot before background I/O; recovery provenance remains distinct from a formally saved project. |
| Export / dialogs | Retry retains settings; invalid Windows filenames remain in the dialog; owned compact dialogs with explicit confirm/cancel and keyboard behavior. |
| Performance | Bitmap presentation cache, no sidebar rebuild or recovery scan on pointer move; visible-frame previews; measured 256x256 / 96-pointer-stroke scenario with 96 undos. |

## Verification and delivery

The release workflow builds the entire solution, runs all 258 unit tests and 33 rendered desktop interaction checks, records the exact source, publishes a self-contained Windows x64 EXE, then launches that EXE on Windows for native UI Automation smoke checks. Publishing stops on a failed gate. Actual results, source SHA, metrics and checksums are attached to that workflow; this document alone is not a passing result.

The desktop checks use real Avalonia controls, input routing and Skia rendering. High-DPI checks render at changed scale; they do not emulate every Windows monitor/driver combination. Performance measurements explicitly include headless queue/render drains and are not physical display latency or a universal FPS promise. Native smoke coverage includes startup, named eyedropper, painting, canceled unsaved close and a compact window.

`MyLovePixel.exe` embeds .NET and native libraries; no separate .NET installation is needed. It is unsigned. Source and third-party notices are shipped separately. No main-branch history rewrite is performed.

Author review was performed against implementation, tests and screenshots. No independent reviewer or separate human usability study is claimed. Transport patch scripts are consumed on CI and removed from the recorded application source; they are not runtime code.
