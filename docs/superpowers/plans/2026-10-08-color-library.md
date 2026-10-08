# Color Library Implementation Plan

> **For agentic workers:** Execute inline with superpowers:executing-plans. User requested direct delivery.

**Goal:** Preserve local colors across upgrades while adding an organized color workspace.
**Architecture:** Keep UserPaletteStore as application boundary; versioned JSON migration and atomic replacement. Separate picker, library and temporary-color UI partials in existing Avalonia desktop.
**Tech Stack:** Existing .NET 10.0.400 / Avalonia 12.1.1 / xunit.v3; no new dependencies.
**Spec:** ../specs/2026-10-08-color-library.md

## Global Constraints
Keep user data paths, project schema and EXE assembly identity unchanged. Never auto-clear data or silently evict colors. Work on color-library-upgrade-20261008; do not overwrite main.

## Review Focus
- Alpha-zero colors keep RGBA bytes, including nonzero RGB.
- Empty or invalid folder inputs and duplicate names must not corrupt saved state.
- Two windows interleaving folder and swatch changes must not clobber each other.
- Failed lock/replace must leave in-memory state and primary file unchanged.
- Search and paging must reset when selecting folders and after collection mutation.

## Task 1: Persistent library
Files: Application/UserPaletteStore.cs, Application/UserPaletteModels.cs,
Application/UserPaletteStore.Serialization.cs; Application.Tests/ColorLibraryUpgradeTests.cs,
Application.Tests/ColorLibraryOrganizationTests.cs.
Interfaces: Colors, Entries, Folders, TemporaryColors read-only; Add, Remove,
CreateFolder, RenameFolder, DeleteFolder, MoveColor, RenameColor, Query,
AddTemporary, RemoveTemporary, ClearTemporary.
- [x] Existing migration tests fail as expected (3 failures; 67 existing tests pass).
- [x] Add tests for folders/search/temp/interleaving/invalid state; observe red.
- [x] Implement version 2 snapshot and lossless legacy backup/atomic save.
- [x] Run application test suite and commit.

## Task 2: Picker and workspace UI
Files: Application/HsvColor.cs, Application.Tests/HsvColorTests.cs,
Desktop/ColorPickerDialog.cs, Desktop/MainWindow.UserPalette.cs,
Desktop/MainWindow.TemporaryColors.cs, Desktop/MainWindow.Convenience.cs,
Desktop/MainWindow.cs, Desktop/MainWindow.TransparentPalette.cs.
Interfaces: HsvColor.FromRgba/ToRgba; ColorPickerDialog returns nullable Rgba32.
- [x] Add HSV tests and observe missing API; implement and run.
- [x] Add Colors tab; move tool colors and library; remove Photo tab and stock grid.
- [x] Implement paginated search, folder actions and temp rack using existing UI styles.
- [x] Build and smoke-test dialog and tab layout, fix regressions.

## Task 3: Release
Files: .github/workflows/windows-release.yml, .github/workflows/ci.yml,
docs/USER_PALETTE.md, HANDOFF.md.
- [x] Correct MTP dotnet test syntax to --solution; enable feature-branch Windows build.
- [x] Full local test run; review changed code and run checks.
- [ ] Commit via GitHub using parent lease; Windows build/test/publish and UI/startup smoke.
- [ ] Retrieve EXE, verify PE header and SHA256, deliver it with update guidance.

Local UI gate: 23 checks passed, including native pointer drag and 600-color legacy upgrade. Found/fixed queued-HEX acceptance and zero-height swatch area at minimum window size. Unit safety tests observed red before implementation. Native Windows validation and artifact verification remain the release gate.

Review: full suite 297/297 passed, 0 skipped. Local GUI 23/23 checks. No independent reviewer tool is available; checked modified persistence, picker acceptance, tab bounds, selection/sampling separation and old-file failure behavior directly. Windows remains the final release gate.
