# ADR-0003: Raster algorithms are read/compute only

Status: Accepted

## Decision

`MyLovePixel.Raster` contains deterministic integer-pixel algorithms. It references `MyLovePixel.Core` for geometry, colors, snapshots, and `PixelWrite`, but it does not receive write access to `PixelSurface` and does not reference `Commands`, UI, or Renderer code.

Raster operations return coordinates or a `RasterPatch`. Applying a patch to the live document remains the responsibility of `CommandBus` / `PixelPatchCommand`.

This keeps three concerns separate:

1. **Rasterization** — what pixels should be affected.
2. **Ink** — how a paint color combines with an existing pixel.
3. **Mutation/Undo** — when those writes enter the live document and history.

## Consequences

- Preview and final commit can call the same raster algorithm.
- Raster tests run without UI and without a mutable document.
- Future Tool implementations cannot bypass Undo by obtaining a writable surface from Raster.
- Indexed color, shading ink, selection clipping, and plugin strategies can extend separate interfaces without rewriting line/fill/shape algorithms.
- Raster algorithms must use integer/deterministic rules; floating point is not part of the base pixel geometry contract.
