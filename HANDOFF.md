# MyLovePixel — Handoff

> 继续开发时先确认 `main` HEAD 与最新 GitHub Actions，再读本文件、`docs/IMPLEMENTATION_PLAN.md`、`docs/ARCHITECTURE.md` 和 `docs/DECISIONS/`。不要仅凭本文记录的 SHA 判断仓库是否已经继续更新。

## 1. 项目目标与永久边界

MyLovePixel 是私人游戏开发使用的可扩展 Pixel Art / Sprite 编辑器。当前技术基线：.NET 10 / C# 14、xUnit v3、SkiaSharp 4.151.1、Avalonia 12.1.1。

永久约束：

1. UI 不直接修改 `PixelDocument`；undoable mutation 统一经过 Commands / `CommandBus`。
2. Cel 只持稳定 `ResourceId`；Linked Cel 通过共享 Surface 表达。
3. Tilemap Cell 只引用稳定 `TileId`；Tile 只引用稳定 `ResourceId`；Cell 不拥有像素副本。
4. `PixelSurface` 是像素真值；GPU texture、thumbnail、composite 都是可重建缓存。
5. Core 不引用 Avalonia/Skia/UI，也不包含具体 Effect evaluator。
6. Raster / AutoTile / Effect / Renderer / Export 只读 Snapshot 或 immutable input。
7. Selection / preview / docking / zoom / recovery UI state 是 transient workspace state，不进入 `.pixelproj` 文档语义。
8. Persistence 使用独立 DTO + `schemaVersion` + 逐级 Migration；unknown JSON/plugin ZIP payload 必须保留。
9. Revision 决定缓存正确性，Dirty Region 只决定性能；历史不完整时 full fallback。
10. 高变化算法优先 Strategy/Registry。
11. Effect preview 不修改 live Surface；Bake 经过 Command，不能破坏 linked source。
12. Export UI/CLI 共用 `ExportPipeline`；click handler 不实现导出算法。
13. Desktop 只依赖 Application；Application 才负责协调 Tools/Commands/Render/Persistence/Export。
14. 不重写 Git 历史；feature branch 全 CI 绿后才 `force:false` fast-forward `main`。

## 2. 当前 solution

核心/算法项目：Core、Commands、Persistence、Raster、Selection、Render、Tools、Animation、Color、Tilemap、Effects、Export、Cli。

Batch13 新增：

- `MyLovePixel.Application`：headless editor orchestration/presentation boundary；
- `MyLovePixel.Desktop`：Avalonia Desktop shell，只引用 Application；
- `MyLovePixel.Application.Tests`。

当前 `.pixelproj` schema = **5**。Batch12/13 都没有升级 schema。

## 3. 已完成批次概览

Batch00–05：Repository/Foundation、Stable References、Command/Undo、Persistence、Raster、Selection/Transform。

Batch06：Snapshot-only RenderGraph、CPU compositor、Skia cache、dirty partial recompose、overlay/diagnostics。

Batch07：Pointer/Input、ToolHost、Pencil/Eraser/Line/Shape/Fill、preview/cancel、stale revision protection。

Batch08：Animation IDs、Frame commands、Clip/Tag/Slice、Pivot/Hitbox/Hurtbox/Socket/Event、Onion Skin、schema2。

Batch09：Palette、Indexed8、Transparent Index、quantize/dither/ramp/shading、Color Cycle、schema3。ADR-0004。

Batch10：Tile/Tileset/Tilemap stable references、sparse chunks、transforms、AutoTile、Rect renderer、schema4。ADR-0005。

Batch11：Cel ordered EffectGraph、typed/animated parameters、CPU evaluator、Outline/Shadow/Palette Map、effect cache、Bake、unknown effect preservation、schema5。ADR-0006。

Batch12：Import/Export/Atlas/Headless Pipeline。PNG importer/exporter、Separate Frames/Sprite Sheet/Atlas、trim/crop/nearest scale/padding/extrude、deterministic atlas、game JSON metadata、standalone ExportPreset JSON、CLI import/export。ADR-0007。Batch12 main baseline = `4ea1e5e08715fd141bd4154563c48e3898f7ffbd`，merge 后 main CI `33150488988` success。

## 4. Batch13 — Avalonia Desktop Shell：完成

分支：`batch13-ui`。

核心交付：

- `ActionId / ActionDescriptor / ActionRegistry`；
- `ShortcutMap`，gesture 只解析为 ActionId；
- `EditorWorkspace / DocumentSession`；
- New/Open/Save/Save As/Export actions；
- Undo/Redo enable state；
- immutable `CanvasPresentation`；
- Layer/Palette/Timeline/Tool presentation DTO；
- `TimelineWindow` bounded virtualization；
- ToolHost 接入 Pencil/Eraser/Line/Shape/Fill；
- Avalonia pointer -> `EditorPointerEvent` -> Application -> ToolHost；
- move/drag 只产生 preview，release 才经 CommandBus commit；
- Tool option inspector 由 ToolDescriptor/schema 驱动；
- Avalonia code-only Desktop app + Fluent theme；
- StorageProvider Open/Save/Export folder adapter；
- Canvas、Tools、Layers、Tool Options、Palette、Timeline、Zoom workspace；
- Desktop 项目只引用 Application；
- live `PixelDocument/PixelProject` 不作为 Desktop public API；
- 新 Layer Commands：rename / visibility / lock / opacity；
- Application layer 的 layer/palette editing facade，no-op 不创建 Undo entry；
- Palette 写入复用 `SetPaletteColorCommand`；
- Desktop theme tokens；
- ADR-0008 `desktop-application-boundary`。

关键 CI：

- Application layer gate `33150967486` success；
- ToolHost integration gate `33151555476` success；
- Desktop/tool options gate `33151734650` success；
- Batch13 final code gate `33153181356` success。

Batch13 最终 code HEAD（文档收口前）：`fbe9f36352e8ecebe3e7125a990422db3114298f`。

## 5. Persistence / semantic facts

Current schema = **5**。

Migration：1→2 Animation；2→3 Palette/Indexed/ColorCycle；3→4 seed + Tileset/Tilemap；4→5 Cel effects。

MLPX codec version 仍为 1：RGBA32 4 byte/pixel；Indexed8 1 byte/pixel；Palette 在 document JSON。

Unknown JSON 和 opaque plugin ZIP payload 必须 roundtrip。Runtime revision/cache/chunks、Desktop workspace state、ExportPreset、recovery journal 都不属于 `.pixelproj` semantic state。

## 6. 已知事故 / 不要重复踩坑

- 写 large file 前 fetch 当前版本，禁止拿旧整文件覆盖新代码。
- 分支出现并行 commit 时用正常 merge commit 收敛；不要 force/rewrite。
- Migration 逐级 deterministic；不要跳 schema。
- Frame Copy 要处理 built-in tracks + Effect parameter tracks，并 remap source FrameId。
- Indexed Surface ↔ Palette 是强引用不变量。
- Palette reorder 在 ColorCycle 引用存在时明确失败。
- Tilemap/Effect renderer revision 变化但 dirty history 不完整时必须 full fallback。
- Export 必须复用 Renderer，不重写 Palette/ColorCycle/Effect composition。
- UI 不要为了绑定方便暴露 Core internal setter 或 live writable Surface。
- Batch14 Autosave/Recovery 默认是 workspace/recovery infrastructure，不应为了 journal 擅自升级 `.pixelproj` schema。

## 7. 下一开发起点：Batch14 — Autosave / Recovery / Performance Hardening

目标：提高崩溃恢复能力，并把大量 frame/tilemap/undo/cache 情况下的资源上限与诊断做成明确机制。

按以下顺序实现：

1. Recovery subsystem：autosave checkpoint + journal metadata；checkpoint 复用现有 `.pixelproj` writer，journal 独立 versioned format。
2. Backup rotation：固定 generation/retention，不能删除最后一个已验证 checkpoint 后再写新文件。
3. Recovery discovery/load：枚举最新有效 checkpoint；损坏 candidate 返回结构化状态，不 silent overwrite 正常工程文件。
4. Desktop recovery UI：启动时或 File 工作流提供恢复入口；恢复文件默认作为 recovery copy 打开，用户显式 Save/Save As 后才覆盖正式工程。
5. Undo memory budget：有可配置上限；超限从最旧 history entry 回收，不能破坏当前 redo/transaction 语义。
6. LRU thumbnail cache：只缓存 immutable rendered thumbnail；有 entry/byte budget、hit/miss/eviction diagnostics。
7. Render cache diagnostics 补齐 hit/miss/ratio 对外快照；保持 Revision correctness 优先于 dirty optimization。
8. Dirty-region visualization：只做 transient diagnostic overlay，不进入文档或 export。
9. Stress fixtures：至少 1000-frame Timeline/thumbnail、large sparse tilemap、repeated Undo budget。
10. Crash injection tests：autosave/checkpoint/journal/rotation 任一步失败时，之前已验证恢复点仍可加载。
11. ADR + HANDOFF；完整 restore/build/test 全绿后 compare 并 `force:false` fast-forward main。

Batch14 Definition of Done：

- 正常工程 save 的 atomic guarantees 不退化；
- 至少一个最近有效 autosave 可被发现和恢复；
- retention/rotation 有确定性测试；
- Undo 历史不会无限增长；
- thumbnail cache 有真实 LRU eviction 与 diagnostics；
- dirty diagnostic overlay 不修改 Surface/Project；
- stress/crash tests 可 headless 运行；
- 不新增 Core -> Desktop/Recovery 反向依赖；
- full CI green 后才合 main。
