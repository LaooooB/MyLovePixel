# MyLovePixel — Handoff

> 继续开发时先确认 `main` HEAD 和 GitHub Actions，再读本文件、`docs/IMPLEMENTATION_PLAN.md`、`docs/ARCHITECTURE.md` 与 `docs/DECISIONS/`。不要只依赖本文记录的 SHA/CI 判断仓库是否已经继续更新。

## 1. 项目目标与永久边界

MyLovePixel 是私人游戏开发使用的可扩展 Pixel Art / Sprite 编辑器。当前技术基线：.NET 10 / C# 14、xUnit v3、SkiaSharp 4.151.1；Batch13 开始接 Avalonia 12.1.1 Desktop Shell。

永久约束：

1. UI 不直接修改 `PixelDocument`；undoable mutation 统一经过 Commands / `CommandBus`。
2. Cel 只持稳定 `ResourceId`；Linked Cel 通过共享同一 Surface 表达。
3. Tilemap Cell 只引用稳定 `TileId`；Tile 只引用稳定 `ResourceId`；Cell 不拥有像素副本。
4. `PixelSurface` 是像素真值；GPU texture、thumbnail、frame/tilemap/effect composite 都是可重建缓存。
5. Core 不引用 Avalonia/Skia/UI，也不包含具体 Effect evaluator。
6. Raster / AutoTile / Effect / Renderer / Export 都只读 Snapshot 或 immutable input。
7. Selection/preview/workspace UI state 是 transient state，不进入 `.pixelproj` 文档语义。
8. Persistence 使用独立 DTO + `schemaVersion` + 逐级 Migration；unknown JSON/plugin ZIP payload 必须保留。
9. Revision 决定缓存正确性，Dirty Region 只决定性能；历史不完整时 full fallback。
10. 高变化算法优先 Strategy/Registry。
11. Effect preview 不修改 live Surface；Bake 经过 Command，不能破坏 linked source。
12. Export UI/CLI 必须共用 `ExportPipeline`；click handler 不实现导出算法。
13. 不重写 Git 历史；feature branch 全 CI 绿后才 `force:false` fast-forward `main`。

## 2. 当前 solution

```text
src/
  MyLovePixel.Core/
  MyLovePixel.Commands/
  MyLovePixel.Persistence/
  MyLovePixel.Raster/
  MyLovePixel.Selection/
  MyLovePixel.Render/
  MyLovePixel.Tools/
  MyLovePixel.Animation/
  MyLovePixel.Color/
  MyLovePixel.Tilemap/
  MyLovePixel.Effects/
  MyLovePixel.Export/
  MyLovePixel.Cli/

tests/
  MyLovePixel.Core.Tests/
  MyLovePixel.Persistence.Tests/
  MyLovePixel.Raster.Tests/
  MyLovePixel.Selection.Tests/
  MyLovePixel.Render.Tests/
  MyLovePixel.Tools.Tests/
  MyLovePixel.Animation.Tests/
  MyLovePixel.Color.Tests/
  MyLovePixel.Tilemap.Tests/
  MyLovePixel.Effects.Tests/
  MyLovePixel.Export.Tests/
```

Batch13 会新增 Desktop/UI 相关程序集，但不能改变上述 domain dependency direction。

## 3. 已完成批次

Batch00–05：Repository Foundation、Stable Document/References、Command/Undo、Persistence、Raster、Selection/Transform。

Batch06：Snapshot-only RenderGraph / CPU compositor / Skia cache / nearest presentation / dirty partial recompose / overlays / diagnostics。

Batch07：Pointer/Input、ToolHost、StrokeSession、Pencil/Eraser/Line/Shape/Fill、preview/cancel、stale revision protection。

Batch08：stable animation IDs、Frame commands、Clip/Tag/Slice、Pivot/Hitbox/Hurtbox/Socket/Event、Onion Skin、schema2。

Batch09：Palette、Indexed8、Transparent Index、quantize/dither/ramp/shading、Color Cycle、schema3。ADR-0004。

Batch10：Tile/Tileset/Tilemap stable references、sparse chunks、transforms、AutoTile、Rect renderer、schema4。ADR-0005。

Batch11：Cel ordered EffectGraph、typed parameters/animation、CPU evaluator、Outline/Shadow/Palette Map、effect cache、Bake、unknown effect preservation、schema5。ADR-0006。最终 main baseline 在 Batch12 开始前为 `9b577cfff43e1eb41c3a1aaaaf310c1de5dc8d56`。

### Batch12 — Import / Export / Atlas / Headless Pipeline

功能代码已完成，分支 `batch12-export`。最终功能 HEAD 在文档收口前为 `b2bfaa67c0c1c7e6b44d0f20e4a6d1bae92a36c2`；该 HEAD CI `33149744717` restore/build/test 全部 success。

完成内容：

- 新 `MyLovePixel.Export` 程序集；
- `IImporter / IExporter / ExportRequest / ExportPreset`；
- importer/exporter registry；
- immutable `ExportArtifact / ExportBundle`；
- `AssetPipelineException / AssetPipelineErrorCode`；
- Exporter 只读 captured `DocumentSnapshot`；
- Separate Frames / Sprite Sheet / Atlas；
- crop / alpha trim / nearest scale / padding / extrude；
- deterministic shelf atlas packer + registry；
- multi-page / optional power-of-two atlas；
- PNG deterministic RGBA encoder；
- PNG decoder 支持 common non-interlaced grayscale/RGB/gray-alpha/RGBA 和 1/2/4/8-bit indexed PLTE+tRNS、filters 0–4、CRC；
- PNG import 创建新的 RGBA32 `PixelDocument`，不猜测 Indexed8 重建语义；
- game JSON metadata：stable Frame/Clip/Tag/Slice IDs、duration、sourceRect/sourceSize、Pivot、Hitbox/Hurtbox、Socket、Event、9-slice；
- Indexed8/Palette/ColorCycle export 复用 FrameRenderer；
- Effect export 复用现有 Effect evaluator/FrameRenderer；
- snapshot capture 后 live document 后续编辑不影响当前 export；
- standalone versioned ExportPreset JSON；
- explicit selection 中缺失 FrameId 时整体失败，不 silent omit；
- CLI：`export`、`import-png`、`preset-template`；
- CLI 与未来 Desktop UI 共用 ExportPipeline；
- CLI file-level import→`.pixelproj`→export 回归测试。

Persistence 在 Batch12 **仍为 schema 5**。ExportPreset version 与 `.pixelproj` schema 独立。

架构决策：`docs/DECISIONS/ADR-0007-export-pipeline.md`。

## 4. 当前 Persistence 事实

Current schema = **5**。

Migration：1→2 Animation；2→3 Palette/Indexed/ColorCycle；3→4 seed + Tileset/Tilemap；4→5 Cel effects。

MLPX codec version 仍为 1：RGBA32 4 byte/pixel；Indexed8 1 byte/pixel；Palette 数据在 document JSON。

Unknown JSON 和 opaque plugin ZIP payload 必须 roundtrip。Runtime revisions/cache/chunks 不进入 semantic hash。

## 5. 已知事故 / 不要重复踩坑

- 写 Core/large file 前 fetch 当前版本；不要用陈旧整文件覆盖。
- Migration 逐级且 deterministic；不要跳 schema。
- Frame Copy 要处理 built-in tracks + Effect parameter tracks，并 remap source FrameId。
- Indexed Surface ↔ Palette 是强引用不变量。
- Palette reorder 在 ColorCycle 引用存在时当前明确失败。
- Tilemap/Effect renderer revision 变化而 dirty history 不完整时必须 full fallback。
- Skia bitmap lifetime 由 `SkiaFrameCache` 明确拥有。
- Export 不能重写 Palette/ColorCycle/Effect composition；必须复用 Renderer。
- ExportPreset 是 workflow config，不要为了 UI preset 保存擅自升级 `.pixelproj` schema。
- Batch13 UI 不要为了绑定方便暴露 Core internal setter 或 live writable Surface。

## 6. 下一开发起点：Batch13 — Avalonia Desktop Shell

目标：把已经成熟的 headless Core/Commands/Render/Tools/Persistence/Export 接成真正编辑器，但 UI 只做 orchestration/presentation，不反向定义 domain architecture。

优先顺序：

1. 新 Desktop App + UI-facing application layer；Avalonia 依赖只出现在 Desktop/UI 项目。
2. `ActionId / ActionDescriptor / ActionRegistry`：菜单、Toolbar、快捷键统一绑定 ActionId。
3. `ShortcutMap`：keyboard gesture → ActionId；不直接绑定 domain mutation。
4. `EditorWorkspace / DocumentSession`：持有 live PixelDocument、CommandBus、当前 Frame/Layer/Tool、Selection/preview 等 transient state；DocumentSession 是 UI 到 domain 的受控门面。
5. File actions：New/Open/Save/Save As/Export；Persistence 与 Export 逻辑只通过现有 service/pipeline。
6. Canvas ViewModel/View：只读 Renderer 输出 + overlay/input routing；Canvas Widget 不拿 mutable Surface。
7. ToolHost 接入 Pencil/Eraser/Line/Shape/Fill；pointer move 只更新 preview，commit 仍进入 CommandBus。
8. Layer panel：读 snapshot/session state；可撤销 layer mutation 需要缺失 Command 时先补 Commands，不允许 UI 直接 setter。
9. Timeline virtualized view：frame item 数据可虚拟化；大量 frame 不创建等量控件树。Frame selection/playback/onion-skin 复用已有 Animation/Renderer。
10. Tool option inspector：基于 ToolDescriptor/options schema，不为每个 Tool 写孤立 UI mutation logic。
11. Palette panel：RGBA/Indexed palette state 只读展示；修改必须走 palette Commands。
12. Theme tokens + basic docking/workspace layout。Dock state 默认 transient，不进入 `.pixelproj`。
13. Desktop headless-testable application layer tests：Action routing、Shortcut routing、DocumentSession snapshot isolation、Undo/Redo enable state、export action delegates shared pipeline。
14. 最后才补最小 smoke UI；CI 仍必须在 Linux runner build/test，不依赖图形桌面才能测试核心交互逻辑。

Batch13 Definition of Done：

- 菜单/Toolbar/Shortcut 全部只绑定 ActionId；
- Canvas 控件不直接写 PixelSurface/PixelDocument；
- Timeline 大量 Frame 不创建海量控件；
- New/Open/Save/Export 都从统一 application service 进入；
- Undoable UI mutation 全部经 CommandBus；
- Desktop App 能启动并展示最小编辑 workspace；
- full restore/build/test CI green 后才合 main。
