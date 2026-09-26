# UX release — 2026-09-26

Approved scope: all P0/P1 items in the conversation's UX audit; preserve existing capabilities, remove redundant descriptions, ship a Windows x64 self-contained executable. No new large modules.

Baseline: 26b8a0b2460733060f4b8d700caeaa06503e09b4.

Execution batches:
1. Read-only exact pixel sampling and a shared shortcut catalog. Regression tests first.
2. Named controls, compact responsive shell, unified focus-aware input, canvas navigation, persistent notifications and close/save protection.
3. Stable panel refresh, explicit colors/alpha, layers and paged thumbnail timeline, dialog/accessible-state cleanup.
4. Whole solution tests and packaged Windows UI smoke checks, inspect failures, publish source and EXE with a checksum.

Transport note: this session has no network-enabled local .NET runtime. Reviewed, exact-match source patches are applied on the repository's isolated Windows runner and committed on the feature branch after the test suite succeeds. They are not runtime application code.

Status: regression baseline pending. Completion is recorded only against actual test/build results. Author review is used; no independent reviewer is available in this session.
