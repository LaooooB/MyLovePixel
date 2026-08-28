# ADR-0004 — Indexed Surface / Palette / Color Cycling

## Status

Accepted.

## Context

Batch09 introduces palette-driven pixel data without changing the existing document rule that a `Cel` references a `PixelSurface` by stable `ResourceId`. The design must keep indexed editing deterministic, preserve visual output through palette reorder, remain snapshot-renderable, and avoid coupling color algorithms to UI or persistence objects.

## Decision

### 1. Canvas compositing remains RGBA32

`CanvasSpec.PixelFormat` remains RGBA32 as the compositor output space. Individual `PixelSurface` resources may be RGBA32 or Indexed8.

This keeps blending semantics in one color space while allowing compact indexed source assets.

### 2. Indexed8 Surface references a stable PaletteId

An Indexed8 `PixelSurface` stores exactly one byte per pixel and a stable `PaletteId`. It does not embed palette colors. `Palette` is a separate resource in `ResourceStore`.

A surface may enter `ResourceStore` only if:

- its referenced Palette exists;
- every stored index is less than `Palette.Count`;
- RGBA32 surfaces do not carry a PaletteId.

A Palette referenced by an Indexed8 surface cannot be removed directly.

### 3. Transparent Index belongs to Palette

A Palette may nominate one `TransparentIndex`. Renderer resolution treats that index as fully transparent regardless of the stored RGBA value at that slot.

Color-cycle ranges are not allowed to include the transparent index.

### 4. Palette mutation and Surface mutation have separate revisions

Palette color changes advance `Palette.Revision`; indexed pixel edits advance `PixelSurface.Revision`.

Renderer cache correctness includes the Palette revision of participating Indexed8 surfaces. A Palette color change therefore invalidates the frame even when index bytes and Surface revision are unchanged.

### 5. Palette reorder preserves visual output by remapping indices

`ReorderPaletteCommand` changes palette order and remaps every associated Indexed8 surface in one undoable operation. The resulting visual RGBA output must be byte-for-byte equivalent before and after reorder.

A Palette cannot be reordered while any Color Cycling keyframe references it. Arbitrary reorder can make an existing contiguous cycle range semantically ambiguous; the operation fails instead of silently changing animation meaning.

### 6. Color Cycling is animation metadata, not pixel mutation

Color Cycling reuses `AnimationTrack<ColorCycleFrameValue>`.

A keyframe contains non-overlapping `PaletteCycle` ranges. During render, the current frame remaps an Indexed8 source index inside the declared range before Palette lookup. It never modifies Palette colors, Surface bytes, or revisions.

The current frame's Color Cycle value is part of the frame render structure signature, so changing a cycle invalidates the cached composite even when resources are unchanged.

Frame copy/remove operations copy/remove/restore Color Cycle values with all other per-frame animation metadata.

### 7. Color algorithms live outside Core mutation paths

Quantization, nearest-color matching, remap calculation, ordered/custom-matrix dithering, `ColorRamp`, and Shading Ink live in `MyLovePixel.Color` as deterministic algorithms/strategies. They operate on snapshots/data and do not mutate live Documents.

### 8. Persistence schema 3

`.pixelproj` schema 3 adds:

- `document.palettes[]`;
- optional `surface.paletteId` for Indexed8;
- Indexed8 MLPX payloads at one byte per pixel;
- `animation.colorCycleTrack`.

Schema 2 -> 3 migration is deterministic, adds an empty palette collection, and derives a stable ID for the Color Cycle track. Unknown JSON fields remain preserved through load/save.

## Consequences

- Cel/Surface reference semantics stay unchanged.
- Indexed assets are compact without making Renderer or UI own palette truth.
- Palette edits and color-cycle playback are non-destructive.
- Cache invalidation remains correctness-first; palette/cycle changes may full-recompose a frame until a future optimization proves a narrower invalidation safe.
- Palette resize is intentionally not a casual setter. A future resize operation must atomically remap/validate all dependent Indexed8 surfaces and animation references.
