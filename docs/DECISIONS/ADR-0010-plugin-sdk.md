# ADR-0010 — Versioned Plugin SDK and Controlled Extension Boundaries

- Status: Accepted
- Date: 2026-08-28
- Batch: 15 — Plugin / Script SDK

## Context

MyLovePixel has reached the point where private workflow-specific features should usually be added as extensions rather than by widening Core or exposing the live document graph. Existing internal registries for Effects, Exporters, Importers, Palette algorithms, Dither and AutoTile were useful implementation seams, but they were not a stable public plugin contract.

A public plugin surface introduces different risks from internal strategy interfaces: API compatibility must be explicit, unload must remove registrations, plugin exceptions must not leave partial editor state, plugin persistence must survive missing implementations, and no extension may bypass the existing Command/Revision/Dirty or Snapshot-only rendering rules.

## Decision

### 1. `MyLovePixel.PluginSdk` is the only public plugin contract assembly

External plugins compile against `MyLovePixel.PluginSdk`. The SDK has no project reference to Core, Commands, PluginHost, Avalonia or SkiaSharp and exposes BCL-only immutable/value contracts.

Public compatibility is described by `PluginApiVersion`, not by the implementation assembly version. Plugin manifests declare minimum and maximum supported API versions. API 1.0 accepts only compatible major-version ranges that overlap the host-supported range.

`PluginId` is stable and namespaced. Duplicate loaded Plugin IDs and duplicate extension IDs within a registry are rejected.

### 2. PluginHost is an adapter layer, not part of the plugin ABI

`MyLovePixel.PluginHost` may depend on Core, Commands, Effects, Render, Export and Persistence because its job is to adapt public SDK DTOs to internal editor services. Plugins themselves never receive those services.

The host owns registries for Tool, Command, Effect, Exporter, Importer, Panel, Palette, Dither and AutoTile extensions. Registration requires the corresponding manifest capability. Every registration returns a disposable token owned by the plugin load scope.

If registration fails part-way through, all earlier registrations from that load attempt are disposed in reverse order. Unload disposes every registration before invoking optional plugin lifecycle cleanup.

### 3. Plugin mutation authority is target-scoped and Command-backed

Plugin Tools, Commands and Panels receive immutable RGBA target snapshots containing a SurfaceId, revision, size and copied bytes. A mutation is expressed as a declarative `PluginPixelPatch`.

The host accepts a patch only when:

- its SurfaceId is the same target the extension received;
- its expected revision is still current;
- every pixel coordinate is in bounds;
- the target is still RGBA32.

Accepted patches are converted to the existing `PixelPatchCommand` and executed by `CommandBus`. Therefore Undo, Redo, DirtySurfaceRegion and Surface revision semantics remain unchanged. Preview writes are transient presentation data and never mutate the live Surface.

A plugin is not given `PixelDocument`, a writable `PixelSurface`, an arbitrary mutation callback, a GPU resource lifetime object, an Avalonia control, or a raw filesystem handle through the SDK.

### 4. Effects and exporters consume immutable data and reuse existing pipelines

Plugin Effect evaluators receive immutable plugin image/value DTOs. PluginHost adapts them into the existing Effect registry/backend so cache configuration revision and FrameRenderer behavior remain authoritative.

Plugin Exporters receive frames rendered through the normal FrameRenderer. They do not independently reimplement Indexed palette lookup, Color Cycle or Effect composition. Export artifacts are validated as safe relative paths before the existing export writer persists them.

Plugin Importers return an immutable RGBA `PluginImage`; PluginHost creates a normal RGBA document through `RgbaDocumentFactory`. Plugin API 1.0 rejects non-zero import image origins or importer metadata because there is no lossless document mapping for those values yet; they are not silently discarded.

### 5. Plugin project state is opaque and namespaced

Plugin project payloads are stored under a namespace derived from `PluginId`, using the existing opaque ZIP-entry preservation mechanism. Loading or saving a project does not require the plugin implementation to be installed.

Unknown plugin payload is not interpreted by Persistence and does not require a `.pixelproj` schema bump. Batch15 therefore keeps project schema at 5.

### 6. Panels use a UI-neutral presentation contract

The SDK panel contract is fields, sections and actions. It does not expose Avalonia controls. Application maps the SDK model into Application presentation DTOs, and Desktop may adapt those DTOs to Avalonia.

Panel actions that return mutations are subject to the same target/revision Command boundary as Tools and Commands. Panel build/action exceptions are isolated and recorded as plugin diagnostics.

### 7. Script API 1.0 defines a runtime-neutral cooperative budget contract

Batch15 does not select Lua, JavaScript or WASM. The public script contract defines:

- operation budget;
- accounted-memory budget;
- time budget and cancellation;
- a determinism flag;
- typed `PluginValue` inputs/results;
- deterministic JSON serialization for the public value kinds.

`PluginScriptRunner` latches operation/memory budget violations. A program cannot turn a budget failure into success merely by catching the exception raised by the accounting context.

This in-process contract is **not a security sandbox or preemptive CLR isolation boundary**. Operation and memory accounting are cooperative hooks intended for a future concrete script runtime to wire to instruction/allocation accounting. Time cancellation also requires the selected runtime to cooperate or provide its own preemption. Runtime choice remains deferred until an actual private workflow requires it.

### 8. Plugin failures are structured and isolated

Registration, execution and invalid-mutation failures produce `PluginDiagnostic` records. Tool/Command/Panel failures occur before Command execution or are rejected by the mutation gateway, so they cannot leave a half-applied document change. Exporter/Importer failures are translated into the existing structured asset-pipeline errors.

DLL loading uses a collectible AssemblyLoadContext. Unloading first removes Host registrations and then releases the load context, so registries do not retain dangling extension instances.

## Consequences

- Custom private features can be developed out-of-tree against a small versioned assembly.
- Core and the document model do not gain plugin-specific mutation hooks or reverse dependencies.
- Existing Undo/Dirty/Revision, Renderer and Persistence invariants stay authoritative for plugin work.
- Missing plugins do not make projects unsavable or destroy opaque plugin state.
- Desktop remains behind Application and does not become part of the public plugin ABI.
- API 1.0 intentionally supports RGBA raster mutation only; Indexed-specific mutation contracts can be added in a future compatible API revision rather than exposing internal Surface methods.
- Script execution is resource-accounted but not claimed to be hostile-code isolation. A concrete runtime must preserve these contracts when one is selected.
