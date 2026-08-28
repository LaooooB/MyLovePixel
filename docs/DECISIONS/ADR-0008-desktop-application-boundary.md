# ADR-0008 — Desktop/Application Boundary

Status: Accepted

## Context

Batch13 introduces the first interactive desktop shell after the document, command, raster, render, animation, palette, tilemap, effect and export engines already exist. The main risk is allowing Avalonia controls to become a second mutation path or to make platform input/presentation types part of domain contracts.

## Decision

1. `MyLovePixel.Desktop` depends on `MyLovePixel.Application` only. Avalonia stays in Desktop.
2. `MyLovePixel.Application` is the UI-facing orchestration boundary. It owns `EditorWorkspace`, `DocumentSession`, Action routing, file workflows and presentation DTOs.
3. `DocumentSession` owns the live `PixelProject`, `PixelDocument`, `CommandBus`, current frame/layer/tool and transient workspace state. The live document/project are not public Desktop APIs.
4. Menus, toolbar and shortcuts resolve through stable `ActionId`; platform gestures never bind directly to domain mutation.
5. Canvas consumes immutable `CanvasPresentation`. Avalonia pointer input is translated to platform-neutral `EditorPointerEvent`, then Application translates again into ToolHost input.
6. Tool preview is transient immutable overlay data. Pointer release commits through existing ToolHost -> CommandBus semantics.
7. Layer mutations use dedicated Commands. Palette mutation reuses palette Commands. UI/application helpers must not use Core internal setters.
8. Timeline exposes bounded `TimelineWindow` ranges. The Desktop creates controls only for the visible/page range, not for total frame count.
9. File Open/Save/Export delegates to existing Persistence/Export services; Desktop click handlers contain no serializer/export algorithm.
10. Theme/docking/zoom/selection/preview state remains transient workspace state and is not added to `.pixelproj` schema.

## Consequences

- Core remains free of Avalonia and windowing dependencies.
- Desktop can be replaced without changing document semantics.
- Most editor interaction can be tested headlessly through Application and ToolHost.
- Panel mutation requires a Command before a writable UI control is added.
- Batch14 recovery/workspace persistence must remain separate from document schema unless a future ADR explicitly changes that rule.
