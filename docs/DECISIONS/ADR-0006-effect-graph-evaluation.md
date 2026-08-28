# ADR-0006 — Non-destructive Effect Graph and Snapshot Evaluation

Status: Accepted

## Context

MyLovePixel needs Outline, Shadow, Palette Map and future plugin effects without letting preview/evaluation mutate `PixelSurface`, without coupling Core to a CPU/GPU backend, and without making an unavailable plugin effect destroy project data.

Effects may also expand image bounds. A bake operation therefore cannot be modeled as an in-place pixel patch in all cases.

## Decision

### 1. Effect data belongs to the Cel

Each `Cel` owns an ordered `EffectGraph`. Each `EffectInstance` has a stable `EffectInstanceId`, namespaced `TypeId`, enabled state, static typed parameters and optional `AnimationTrack<EffectValue>` bindings.

Effect instance IDs are stable within the owning Cel effect graph. Effect parameter track IDs are stable within that graph. Copying a frame copies the graph, allocates new parameter track IDs, and remaps the source-frame keyframe to the copied frame.

### 2. Core stores semantics, not algorithms

`MyLovePixel.Core` defines `EffectDescriptor`, parameter schemas, typed values, instances, graphs and immutable snapshots. It does not know Outline/Shadow/Palette Map implementations and does not reference Skia, Avalonia or a GPU API.

Concrete evaluation lives in `MyLovePixel.Effects` behind `IEffectEvaluatorBackend`. The CPU backend is the reference implementation; a later GPU backend must obey the same immutable input/output contract.

### 3. Evaluation is snapshot-only

Effect evaluation receives `DocumentSnapshot`, `FrameId` and `CelSnapshot`. It never receives a mutable `PixelDocument` or live `PixelSurface`.

The evaluator converts RGBA32 or Indexed8 source data to an immutable RGBA `EffectImage`. Indexed sources resolve Palette and Color Cycle state from the same snapshot.

`EffectImage` carries an `Origin` in addition to size because effects such as Outline and Shadow can expand beyond source bounds.

### 4. Unknown effects are preserved, not executed

A project does not require the matching evaluator to load an EffectInstance. Unknown/unavailable effect types remain ordered document data and round-trip through persistence.

Runtime evaluation passes unavailable effects through unchanged and reports their type IDs. Bake refuses when an enabled effect is unavailable so opaque semantics are never silently discarded.

### 5. Cache correctness uses exact dependency signatures

Effect cache identity is document/cel/frame. Correctness uses exact state, including source Surface revision, relevant Palette revisions, Color Cycle value, EffectGraph revision, each EffectInstance revision and evaluator backend revision.

Frame render structure also includes effect state and palette dependencies. A changed source Surface used by a Cel with effects currently forces full frame recomposition. Effect-aware dirty-region expansion is deferred until performance hardening; correctness takes precedence over partial redraw.

### 6. Bake is a Command and creates a new Surface

Bake is two-phase:

1. `EffectBakePlanner` evaluates an immutable snapshot and returns a `BakePlan` containing the result plus captured dependency state.
2. `BakeEffectsCommand` revalidates the live Cel/source/effect/palette dependencies before applying.

If any captured dependency is stale, Bake fails rather than applying old preview pixels.

Successful Bake creates a new RGBA Surface, points only the target Cel to it, adjusts Cel position by `EffectImage.Origin`, and clears that Cel's effect graph. It never mutates a potentially linked source Surface. Undo restores the prior SurfaceId, position and EffectGraph and removes the baked Surface.

### 7. Persistence schema 5 stores effect semantics on Cel DTOs

`CelDto.effects[]` contains instances, static parameters, parameter tracks, keyframes and typed values. Unknown JSON is preserved at every extensible nested DTO level.

Schema 4→5 migration adds an empty `effects` array to legacy Cels. Semantic hash includes ordered effect semantics and animation bindings but excludes runtime revision counters.

## Consequences

- UI can preview effects without touching document pixels.
- CPU/GPU implementations can evolve independently of the Core model.
- Linked Cels are not accidentally destructively modified by Bake.
- Missing plugin evaluators do not make projects unloadable or lossy.
- Renderer may perform more full recompositions than theoretically necessary until effect-aware dirty propagation is implemented.
- Plugin SDK remains deferred; Batch11 establishes registration/evaluation boundaries without freezing a public external ABI.
