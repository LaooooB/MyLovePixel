# ADR-0005 — Tilemap Reference, Determinism, and Cache Contract

## Status

Accepted for Batch10.

## Context

Tilemap must remain an asset-reference model. If every cell owns pixels, editing a shared tile becomes O(number of cells), project files become large bitmaps, AutoTile cannot operate on stable tile identity, and resource lifetime becomes ambiguous.

Batch10 also introduces sparse maps, deterministic weighted AutoTile selection, and a cached renderer. These features need one correctness contract shared by Core, Commands, Persistence, and Render.

## Decision

### 1. Cell references Tile; Tile references PixelSurface

The stable reference chain is:

```text
Tilemap Cell -> TileId -> TileDefinition -> ResourceId -> PixelSurface
```

A cell never owns or copies tile pixels. Multiple cells may reference one `TileId`; multiple tiles may reference surfaces when explicitly constructed that way.

Editing tile pixels mutates the referenced `PixelSurface` through a Command. All cells that resolve to that tile observe the new pixels automatically.

`MakeUniqueTileCommand` is the explicit operation that clones a tile surface and rewires one cell to a new `TileId`. Copy-on-write is never implicit.

### 2. Resource GC is explicit and reference-safe

A Tile cannot be removed while any Tilemap cell references it. A Tile Surface cannot be removed while a Tile references it. A Tileset cannot be removed while a Tilemap references it.

Unused tile/surface cleanup is an explicit undoable operation; ordinary cell edits do not silently collect resources.

### 3. Runtime chunks are not project-format semantics

Core stores Tilemap cells in sparse 32x32 chunks for locality and negative-coordinate support. `ChunkSize` is a runtime implementation detail.

Schema4 persists Tilemaps as a deterministic sparse `cells[]` list containing coordinate, `TileId`, transform flags, and variant. Chunk coordinates, chunk size, and internal chunk layout are not serialized and are not part of `ProjectSemanticHash`.

This allows future chunk/storage changes without a project migration.

### 4. Transform flags have one meaning

Cell flags are `FlipX`, `FlipY`, and `Rotate90`. Unknown flag bits are rejected.

`Rotate90` currently requires square tiles so renderer/exporter do not disagree about output dimensions. Supporting rectangular 90-degree rotation later requires an explicit destination-size/origin contract rather than a silent behavior change.

### 5. AutoTile is snapshot-only and deterministic

`IGridTopology` and `IAutoTileRule` consume immutable snapshots and produce cell patches. They never mutate `PixelDocument` directly.

Rect, isometric-diamond, and odd-row hex coordinate/topology strategies are registered independently from the document model.

Weighted variants are selected from stable inputs including document seed and cell coordinate. The same semantic document and rule input must reproduce the same selection; UI timing and process-global RNG are not inputs.

An AutoTile result is applied through one multi-cell Command, so one operation occupies one Undo entry and validates all writes before the first mutation.

### 6. Revision is correctness; Dirty is optimization

Tilemap Renderer follows the same rule as FrameRenderer:

- cache identity includes stable document/tilemap/tileset structure;
- Tilemap revision detects cell-reference changes;
- participating Surface/Palette revisions detect pixel/color changes;
- a partial recompose is allowed only when supplied invalidations continuously cover the cached revision -> current revision interval;
- any revision regression, resource-set mismatch, structure mismatch, or invalidation-history gap falls back to full recompose.

A changed Tile Surface maps back to every visible cell that references the corresponding Tile. A changed cell maps to its tile-sized canvas region. Cache loss is always recoverable from `DocumentSnapshot`.

Dirty regions never establish correctness on their own.

### 7. Renderer remains read-only

Tilemap rendering consumes `DocumentSnapshot`. It has no live `PixelDocument`, CommandBus, or mutable ResourceStore access.

Batch10's raster Tilemap renderer supports rectangular topology. Iso/Hex topology math is available through `IGridTopology`, but adding full Iso/Hex compositing later must preserve this snapshot/cache contract instead of special-casing live state.

### 8. Persistence schema4

Schema4 adds:

- `document.seed`;
- `tilesets[]` with stable Tile IDs and Surface references;
- `tilemaps[]` with stable Tileset references and sparse cells.

Schema3 -> 4 migration derives missing seed deterministically from `DocumentId` and adds empty Tileset/Tilemap collections. Existing compatible extension fields are preserved; conflicting incompatible field types make migration fail explicitly.

Unknown JSON fields on Tileset, Tile, Tilemap, and Cell continue to round-trip through extension data.

## Consequences

- Shared-tile editing is cheap and predictable.
- Cell edits do not duplicate pixel memory.
- Large sparse maps do not require giant backing bitmaps.
- AutoTile output is reproducible in tests, CLI, and future UI.
- Renderer can do partial updates without trusting incomplete dirty information.
- Project files are insulated from future chunk-layout changes.
- Future exporters can consume the same stable Tile/Cell references without reverse-engineering rendered pixels.
