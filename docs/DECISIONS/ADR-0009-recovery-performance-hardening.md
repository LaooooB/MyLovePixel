# ADR-0009 — Recovery and Performance Hardening Boundaries

- Status: Accepted
- Date: 2026-08-28
- Batch: 14 — Autosave / Recovery / Performance Hardening

## Context

Batch13 made the editor usable as a Desktop application. At that point three classes of risk remained: a crash could lose recent work, unbounded history/cache growth could exhaust memory, and Desktop was not forwarding complete dirty-history information into the renderer even though the renderer already supported revision-safe partial recomposition.

These concerns must be solved without changing document semantics, weakening atomic project save, or giving Desktop direct access to mutable domain objects.

## Decision

### 1. Autosave is a verified project checkpoint, not a command replay log

Autosave writes a normal `.pixelproj` checkpoint using the existing Persistence writer. The checkpoint is reopened and its semantic hash is verified before a recovery journal is published.

The recovery journal is a separate, versioned workspace format. It contains recovery metadata and a reference to the verified checkpoint. It does not change `.pixelproj` schema 5 and is not included in project semantic state.

Write order is:

1. atomically write checkpoint;
2. reopen and semantic-verify checkpoint;
3. atomically publish recovery journal;
4. rotate old generations only after the new journal is committed.

A crash may leave an orphan checkpoint, but must not leave the previous verified recovery point unavailable merely because a new generation was being written.

### 2. Recovery never silently overwrites the source project

A recovered project opens as a detached dirty `DocumentSession`:

- `IsRecovered = true`;
- `FilePath = null`;
- original source path is informational recovery metadata only.

The user must explicitly Save/Save As to establish a normal project path. Only that explicit save clears recovery state.

### 3. Recovery discovery is structured and corruption-tolerant

Invalid journal, missing checkpoint, corrupt checkpoint and semantic mismatch are represented as candidate states. Discovery continues so an older valid checkpoint can still be offered.

Recovery file scanning is not part of high-frequency canvas refresh. Desktop refreshes recovery presentation only at startup and recovery/autosave workflow boundaries.

### 4. Undo history has a byte budget

`CommandBus` owns a configurable `UndoHistoryOptions` budget. History entries estimate retained undo-token memory. When the budget is exceeded, the oldest committed undo entries are evicted first.

A single newest entry may temporarily exceed the soft budget so the action the user just performed remains undoable. Diagnostics expose budget, estimated bytes and eviction count. Active transaction semantics are unchanged.

### 5. Thumbnail cache is immutable, bounded LRU

Thumbnail cache entries are immutable rendered RGBA images. The cache has both entry and byte limits and uses true least-recently-used eviction. Oversized single thumbnails bypass the cache instead of breaking the budget.

Cache correctness keys continue to include stable frame identity plus visual structure/revisions. Diagnostics expose hit, miss, eviction, byte and ratio information.

### 6. Dirty regions are optimization and diagnostics only

`DocumentSession` accumulates `DocumentChange.DirtySurfaces` between renders and maps them to revision-covering `SurfaceInvalidation` records. The renderer still decides whether partial recomposition is safe; incomplete history causes full fallback.

Dirty-region visualization is derived from the actual partial texture upload plan and is a transient Desktop overlay. It does not mutate `PixelSurface`, affect export, or enter persistence.

### 7. Stress tests assert structural limits, not wall-clock timing

Hardening fixtures cover 1000-frame timeline/thumbnail workloads, 10,000 sparse tile cells, 5,000 repeated undoable edits, and failure injection at every recovery write stage. Tests assert bounded resources and recoverability rather than machine-dependent elapsed-time thresholds.

## Consequences

- Recovery correctness reuses the mature project validator/migration/semantic-hash path instead of creating a second mutation replay engine.
- `.pixelproj` remains schema 5.
- Recovery and cache data remain disposable workspace/runtime data.
- Memory growth from history and thumbnails is explicit and diagnosable.
- Partial redraw improves Desktop performance without becoming a correctness requirement.
- Future plugin/script work must not bypass these boundaries: plugin mutations remain Commands, plugin recovery payload remains persistence data only when explicitly namespaced in the project, and plugin caches remain rebuildable.