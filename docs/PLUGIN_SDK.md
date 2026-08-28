# MyLovePixel Plugin SDK 1.0

`MyLovePixel.PluginSdk` is the public contract for private/out-of-tree extensions. Plugin code should reference this project/assembly only. Do not reference Core, Commands, PluginHost, Application, Desktop, Avalonia or SkiaSharp from a normal plugin.

## Manifest and compatibility

Every plugin implements `IPlugin` and exposes a `PluginManifest` with:

- stable namespaced `PluginId`, for example `com.example.sprite-tools`;
- plugin display name and plugin version;
- minimum and maximum `PluginApiVersion`;
- declared `PluginCapability` flags.

Current public API is **1.0**. Host compatibility is based on `PluginApiVersion`, not the implementation assembly version.

## Registration

`IPlugin.Register` receives `IPluginRegistrationContext`. API 1.0 can register:

- Tool;
- Command;
- Effect;
- Exporter;
- Importer;
- Panel;
- Palette algorithm;
- Dither algorithm;
- AutoTile rule.

A registration is allowed only when its capability is declared by the manifest. The Host owns registration tokens and disposes them on unload. If registration throws, the entire load scope is rolled back.

## Mutation rule

Plugins do not receive a live `PixelDocument` or writable `PixelSurface`.

Raster-facing extensions receive an immutable `PluginRasterTarget` containing copied RGBA bytes plus SurfaceId/revision. A final edit is returned as `PluginPixelPatch`. PluginHost verifies that the patch still targets the exact Surface/revision supplied to the extension and converts it to the editor's normal `PixelPatchCommand`.

Therefore plugin commits still participate in:

- CommandBus Undo/Redo;
- Surface revision increments;
- DirtySurfaceRegion invalidation;
- stale-revision rejection.

Tool preview writes are transient and are never written to the live Surface until a commit patch is accepted.

API 1.0 exposes RGBA raster mutation only. Indexed-specific write contracts should be added explicitly in a later API revision instead of exposing internal Surface APIs.

## Effects, export and import

Plugin Effects consume immutable image/value requests. PluginHost adapts them into the normal Effect engine, so renderer cache revision rules still apply.

Plugin Exporters receive frames produced by the normal FrameRenderer. Palette, Color Cycle and Effect composition therefore stay identical to built-in export.

Plugin Importers return an immutable `PluginImage`. The API 1.0 Host adapter converts a zero-origin RGBA image into a standard RGBA document. Import metadata currently has no lossless document mapping and is rejected rather than silently discarded.

## Project data

A plugin with `ProjectData` capability uses its namespaced opaque project-data session. The bytes are stored in the existing `.pixelproj` opaque ZIP area under the plugin namespace.

Persistence does not need the plugin to interpret those bytes. If the plugin is missing, load/save still preserves them. Reinstalling the plugin can interpret them again later. Batch15 does not change `.pixelproj` schema 5.

## Panels

Panels are UI-neutral. `IPluginPanelProvider` returns `PluginPanelModel` containing sections, fields and actions; it does not return an Avalonia control.

Application maps the model to its presentation DTOs. Desktop can render those DTOs using the current UI framework without making Avalonia part of the SDK ABI. Panel mutations are still target/revision checked and Command-backed.

## Script contract

API 1.0 intentionally does **not** choose Lua, JavaScript or WASM. `IPluginScriptProgram` and `ScriptSandboxPolicy` define the host/runtime boundary first:

- operation budget;
- accounted-memory budget;
- time budget;
- cancellation;
- determinism flag;
- typed `PluginValue` result;
- deterministic `PluginScriptValueCodec` JSON serialization.

`PluginScriptRunner` latches operation/memory budget failures so catching the accounting exception cannot convert an over-budget run into success.

This is a cooperative in-process runtime contract, not a hostile-code security sandbox. A future Lua/JS/WASM adapter must wire its own instruction/allocation/preemption mechanisms to these limits.

## Assembly loading and unload

`PluginAssemblyLoader` loads plugin DLLs in a collectible load context. The assembly must contain exactly one constructible `IPlugin` implementation for the simple loader path.

Unload removes all Host registrations first, invokes optional `IPluginLifecycle.OnUnload`, then releases the collectible load context. Code should not store plugin instances in global/static editor state outside the Host registration lifecycle.

## Error handling

Plugin registration/execution failures are recorded as structured `PluginDiagnostic` values. Invalid patches are rejected before Command execution. Export/import failures are surfaced through the existing structured asset-pipeline exception types.

A plugin exception must not be handled by directly repairing the Document. Fix the extension or reject the operation; the editor's normal Command/Revision/Persistence invariants remain authoritative.
