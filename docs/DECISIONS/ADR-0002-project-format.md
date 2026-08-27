# ADR-0002: `.pixelproj` project container

Status: Accepted

## Decision

`MyLovePixel.Persistence` owns the on-disk project format. `MyLovePixel.Core` remains unaware of ZIP, JSON, file paths, migration, and atomic-save policy.

A `.pixelproj` file is a ZIP container with these required entries:

- `manifest.json`
- `document.json`
- `surfaces/<resource-id>.mlpx` for every pixel surface

`manifest.json` contains the format marker, `schemaVersion`, the document entry name, and a SHA-256 content hash. `document.json` contains stable IDs and resource references, never embedded pixel arrays.

Pixel surfaces are stored in a small versioned binary codec (`MLPX`) rather than JSON/base64. Runtime `Revision` is cache state and is intentionally not persisted.

Unknown JSON fields are preserved on load/save. Unknown ZIP entries are also preserved as opaque payloads so future plugin data can round-trip even when the plugin is absent.

## Atomic save

Saving to a path always writes a complete temporary file in the same directory, flushes it, validates the produced package, then replaces/moves it over the destination. The old destination is never truncated in place.

## Compatibility

Schema changes are handled by a contiguous migration registry. Loading a schema newer than the runtime is rejected rather than silently dropping semantics.

## Core boundary

Persistence receives controlled internal reconstruction access through `InternalsVisibleTo("MyLovePixel.Persistence")`. These constructors/mutators remain unavailable to UI and plugin code.
