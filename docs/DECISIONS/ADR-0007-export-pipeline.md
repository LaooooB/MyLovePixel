# ADR-0007 — Snapshot-only Export Pipeline

- Status: Accepted
- Batch: 12 — Import / Export / Atlas / Headless Pipeline

## Context

MyLovePixel now has animation metadata, Indexed8 palettes/color cycling, Cel effects, tile resources, a snapshot-only renderer, persistence and a headless CLI. Asset delivery must reuse those semantics instead of letting UI buttons, CLI commands or engine-specific exporters each rebuild their own rendering logic.

The project format and the game-asset export format also have different responsibilities. `.pixelproj` preserves editable document semantics; exported PNG/atlas/JSON artifacts are derived delivery products.

## Decision

1. `IExporter` receives an `ExportRequest` containing an immutable `DocumentSnapshot` and an `ExportPreset`. Exporters never receive a mutable `PixelDocument`.
2. The built-in game-asset exporter uses the existing `FrameRenderer` to resolve RGBA/Indexed8, Palette, Color Cycle and Effect Graph semantics. Export does not implement a second compositor/effect engine.
3. Post-render image operations are owned by `MyLovePixel.Export`: crop, alpha trim, nearest-neighbor scale, padding, extrude, sprite-sheet layout and atlas packing.
4. Atlas packing is a Strategy/Registry boundary. The built-in deterministic shelf packer uses stable ordering and deterministic page placement.
5. Export metadata preserves stable document IDs where they are meaningful to game delivery: Frame, Clip, Tag and Slice IDs; per-frame Pivot, Hitbox, Hurtbox, Socket and Event values are emitted from the captured snapshot.
6. Export artifacts are immutable `ExportArtifact` values collected in an `ExportBundle`. Artifact paths are validated as safe relative paths before filesystem writes.
7. `ExportPreset` is a standalone versioned JSON format. Its version is independent of `.pixelproj` `schemaVersion`; adding export options does not require a project schema migration.
8. Import and export use registries (`IImporter` / `IExporter`) as internal extension boundaries. They are not yet a public plugin ABI; the stable plugin SDK remains Batch15 work.
9. The built-in PNG importer creates a new editable RGBA32 document. Palette PNG files are decoded correctly, but import does not guess that the user wants a persistent Indexed8 palette model.
10. PNG decode supports common non-interlaced PNG inputs used by pixel-art tools, including grayscale/RGB/gray-alpha/RGBA and 1/2/4/8-bit indexed PLTE+tRNS data. PNG export is deterministic 8-bit RGBA.
11. CLI and future Desktop UI must call the same `ExportPipeline`. UI click handlers may choose a preset/output path, but may not own asset-generation logic.
12. A snapshot is captured before an export operation. Mutating the live document after capture cannot change the export already in progress.
13. Pipeline failures surface structured `AssetPipelineErrorCode` values. Missing explicit Frame IDs are rejected as an invalid request rather than silently omitted.
14. `.pixelproj` remains schema 5 for Batch12. Export introduces no persistent runtime document fields.

## Consequences

- Headless CI can test complete asset generation without Avalonia.
- Renderer/effect/palette correctness is shared between editor preview and delivery artifacts.
- Engine-specific exporters can be added later without changing Core or Persistence.
- ExportPreset can evolve on its own compatibility schedule.
- Importing a palette PNG currently favors predictable editable RGBA semantics over automatic Indexed8 reconstruction; a future explicit indexed-import option can be added without changing this decision.

## Rejected alternatives

### Export directly from live `PixelDocument`
Rejected because concurrent edits could change output mid-run and would give exporters write-capable runtime access.

### Reimplement palette/effect composition inside Export
Rejected because editor preview and exported assets could diverge.

### Store ExportPreset inside `.pixelproj` schema 6 immediately
Rejected because export presets are workflow configuration, not required document semantic state. A later workspace/preset persistence feature can reference the standalone format.

### Let CLI implement its own PNG/sheet logic
Rejected because UI and automation would drift into separate pipelines.
