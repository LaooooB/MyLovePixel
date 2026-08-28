# MyLovePixel — Handoff

> 继续开发时先确认 `main` HEAD 与最新 GitHub Actions，再读本文件、`docs/IMPLEMENTATION_PLAN.md`、`docs/ARCHITECTURE.md` 和 `docs/DECISIONS/`。不要仅凭本文记录的 SHA 判断仓库是否已经继续更新。

## 1. 项目目标与永久边界

MyLovePixel 是私人游戏开发使用的可扩展 Pixel Art / Sprite 编辑器。技术基线：.NET 10 / C# 14、xUnit v3、SkiaSharp、Avalonia。

永久约束：

1. UI 不直接修改 `PixelDocument`；undoable mutation 统一经过 Commands / `CommandBus`。
2. Cel 只持稳定 `ResourceId`；Linked Cel 通过共享 Surface 表达。
3. Tilemap Cell 只引用稳定 `TileId`；Tile 只引用 `ResourceId`；Cell 不拥有像素副本。
4. `PixelSurface` 是像素真值；GPU texture、thumbnail、composite 都是可重建缓存。
5. Core 不引用 Avalonia/Skia/UI/Recovery/Plugin SDK/Plugin Host，也不包含具体 Effect evaluator。
6. Raster / AutoTile / Effect / Renderer / Export 只读 Snapshot 或 immutable input。
7. Selection / preview / docking / zoom / recovery / diagnostics 是 transient workspace/runtime state，不进入 `.pixelproj`。
8. Persistence 使用显式 DTO + schemaVersion + 逐级 Migration；unknown JSON/plugin ZIP payload 必须保留。
9. Revision 决定缓存正确性，Dirty Region 只决定性能；历史不完整时 full fallback。
10. 高变化算法优先 Strategy/Registry。
11. Export UI/CLI/Plugin adapter 复用正常 Render/Export 语义；不能重写 Palette/ColorCycle/Effect composition。
12. Desktop 只依赖 Application；Application 协调 Tools/Commands/Render/Persistence/Recovery/Export/PluginHost。
13. Public Plugin SDK 只能暴露稳定 immutable/declarative contract，不能给插件裸 mutable document、GPU lifetime、Avalonia control 或任意 filesystem handle。
14. Plugin raster mutation 必须绑定 Host 提供的 SurfaceId + Revision，最终仍经 CommandBus。
15. 不重写 Git 历史；feature branch 全 CI 绿后才 `force:false` fast-forward `main`。

## 2. 当前 solution / schema

核心与算法项目：

- `MyLovePixel.Core`
- `MyLovePixel.Commands`
- `MyLovePixel.Persistence`
- `MyLovePixel.Recovery`
- `MyLovePixel.Raster`
- `MyLovePixel.Selection`
- `MyLovePixel.Render`
- `MyLovePixel.Tools`
- `MyLovePixel.Animation`
- `MyLovePixel.Color`
- `MyLovePixel.Tilemap`
- `MyLovePixel.Effects`
- `MyLovePixel.Export`
- `MyLovePixel.PluginSdk`
- `MyLovePixel.PluginHost`
- `MyLovePixel.Application`
- `MyLovePixel.Desktop`
- `MyLovePixel.Cli`

测试项目覆盖以上核心模块，并包含：

- `MyLovePixel.PluginHost.Tests`
- `MyLovePixel.TestPlugin`（out-of-tree style，**只引用 PluginSdk**）

当前 `.pixelproj` schema = **5**。

Migration：1→2 Animation；2→3 Palette/Indexed/ColorCycle；3→4 seed + Tileset/Tilemap；4→5 Cel effects。

Batch14 Recovery journal 是独立 versioned workspace format；Batch15 namespaced plugin opaque payload 复用现有 unknown ZIP preservation。两者都**没有**升级 `.pixelproj` schema。

## 3. 已完成批次概览

- Batch00–05：Repository/Foundation、Stable References、Command/Undo、Persistence、Raster、Selection/Transform。
- Batch06：Snapshot-only RenderGraph、CPU compositor、Skia cache、dirty partial recompose、overlay/diagnostics。
- Batch07：Pointer/Input、ToolHost、Pencil/Eraser/Line/Shape/Fill、preview/cancel、stale revision protection。
- Batch08：Animation IDs、Frame commands、Clip/Tag/Slice、Pivot/Hitbox/Hurtbox/Socket/Event、Onion Skin、schema2。
- Batch09：Palette、Indexed8、quantize/dither/ramp/shading、Color Cycle、schema3。ADR-0004。
- Batch10：Tile/Tileset/Tilemap stable references、sparse chunks、AutoTile、Rect renderer、schema4。ADR-0005。
- Batch11：EffectGraph、typed/animated parameters、CPU evaluator、Bake、unknown effect preservation、schema5。ADR-0006。
- Batch12：Import/Export/Atlas/Headless Pipeline、PNG、sheet/atlas、JSON metadata、CLI。ADR-0007。
- Batch13：Avalonia Desktop/Application boundary、ActionId/Shortcut、Canvas/ToolHost、bounded Timeline、Layer/Palette facade、theme tokens。ADR-0008。
- Batch14：Autosave/Recovery、Undo memory budget、thumbnail LRU、dirty diagnostics、stress/crash fixtures。ADR-0009。
- **Batch15：Versioned Plugin / Script SDK、Host adapters、out-of-tree plugin、opaque plugin persistence、UI-neutral panels、runtime-neutral script budgets。ADR-0010。**

Batch14 merged main baseline：`61678b0fcb490e7dfaf6bd1df9a6af9dbadce8f8`；main CI `33160382618` success。

## 4. Batch15 — Plugin / Script SDK：完成

开发分支：`batch15-plugin-sdk`。

### 4.1 Public SDK 1.0

新增 `MyLovePixel.PluginSdk`，当前 Public Plugin API = **1.0**。

关键规则：

- Plugin SDK 是唯一外部插件稳定 contract assembly。
- SDK 只依赖 BCL，不引用 Core / Commands / PluginHost / Avalonia / SkiaSharp。
- `PluginApiVersion` 决定兼容性；**不**用 implementation assembly version 代替 public API version。
- `PluginId` 必须稳定、namespace 化。
- `PluginManifest` 声明 plugin version、API min/max、capabilities。

Public registration contracts：

- Tool
- Command
- Effect
- Exporter
- Importer
- Panel
- Palette algorithm
- Dither algorithm
- AutoTile rule

Capability 未声明却尝试注册时明确失败。

### 4.2 PluginHost / lifecycle / registry

新增 `MyLovePixel.PluginHost`：

- version compatibility validation；
- duplicate PluginId / extension ID policy；
- capability enforcement；
- scoped registration tokens；
- registration 中途失败自动 rollback；
- unload 反向 dispose registrations；
- optional `IPluginLifecycle.OnUnload`；
- structured `PluginDiagnostic`；
- collectible `AssemblyLoadContext` DLL loader。

Unload 顺序固定为：

1. 从 Host registries 移除 extension registrations；
2. 调用 plugin lifecycle cleanup；
3. 释放 collectible load context。

Registry 不允许在 unload 后保留 dangling plugin instance。

### 4.3 Mutation boundary

Plugin Tool / Command / Panel 不获得 `PixelDocument` 或 writable `PixelSurface`。

Host 只给 extension copied immutable `PluginRasterTarget`：

- SurfaceId
- Surface Revision
- Size
- copied RGBA bytes

插件最终编辑返回 declarative `PluginPixelPatch`。Host 只有在以下条件全部满足时接受：

- patch SurfaceId 与 Host 给 extension 的 target 完全一致；
- `ExpectedRevision` 仍等于当前 target revision；
- target 仍为 RGBA32；
- 所有写入坐标合法。

接受后转换成正常 `PixelPatchCommand` 并由 `CommandBus` 执行。

因此 Plugin mutation 继续拥有：

- Undo / Redo；
- Surface revision；
- DirtySurfaceRegion；
- stale revision protection；
- transaction/history 的既有语义。

Tool preview 永远只作为 transient presentation，不改 live Surface。

Plugin API 1.0 刻意只开放 RGBA raster mutation。Indexed-specific mutation 以后要新增明确 public contract，不能直接暴露 internal Surface API。

### 4.4 Effect integration

Plugin Effect evaluator：

- 只吃 immutable plugin image/value DTO；
- Host 转接现有 Effect descriptor/backend；
- plugin registry/backend revision 参与 Effect/Renderer configuration revision；
- 最终仍由正常 FrameRenderer composition。

未知/缺失 plugin effect 仍遵守既有 Effect 规则：可保存，但不能在 Bake 时 silent skip。

### 4.5 Export / Import integration

Plugin Exporter：

- Host 先用正常 `FrameRenderer` 渲染选中 Frames；
- 插件只收到 immutable rendered RGBA frames；
- 不得自行重写 Indexed/Palette/ColorCycle/Effect 解释；
- artifact path 仍走现有安全 relative-path validation/writer。

Plugin Importer：

- `CanImport` 只拿 name + 最多前 64 bytes probe；
- Import 返回 immutable `PluginImage`；
- Host 用 `RgbaDocumentFactory` 建正常 RGBA Document；
- API 1.0 要求 image origin = 0；
- API 1.0 暂无 metadata 的 lossless Document mapping，因此 nonempty metadata **明确失败而不是 silent drop**；
- plugin exception 转 `AssetPipelineException(ImportFailed)` 并记录 diagnostic。

### 4.6 Namespaced plugin persistence

Plugin project data 使用 PluginId namespace 下的 opaque project entries。

规则：

- Persistence 不解释 plugin bytes；
- plugin 缺失时仍 load/save roundtrip；
- plugin 重新安装后可再次解释原 payload；
- 不把 plugin runtime object graph 序列化进 project；
- Batch15 不升级 schema，仍为 **5**。

### 4.7 UI-neutral Panel

SDK Panel contract 只定义：

- Panel model
- Section
- Field
- Action
- Context

不返回 Avalonia Control。

Application 的 `PluginWorkspaceRuntime` 将其转成 Application presentation DTO；Desktop 的 `PluginPanelView` 只消费这些 DTO。

Panel action 返回的 patch 同样必须匹配 Host 提供的 SurfaceId/revision，然后才进 CommandBus。Panel build/action throw 被隔离并记录 structured diagnostic。

### 4.8 Application / Desktop integration

Application `PluginWorkspaceRuntime` 支持：

- DLL load/unload；
- loaded plugin presentation；
- diagnostics presentation；
- builtin + plugin Tool unified palette；
- plugin Tool pointer routing；
- transient preview decoration；
- UI-neutral Panel presentation/action；
- plugin-aware Export。

Desktop 不引用 PluginSdk/PluginHost；Avalonia 仍在 Application boundary 后面。

### 4.9 Script contract

SDK 已定义 runtime-neutral Script API：

- `ScriptSandboxPolicy`
  - operation budget
  - accounted-memory budget
  - time budget
  - determinism flag
- `IPluginScriptContext`
- `IPluginScriptProgram`
- `PluginScriptExecutionResult`
- deterministic `PluginScriptValueCodec`

Host `PluginScriptRunner`：

- structured `operation-budget-exceeded`；
- structured `memory-budget-exceeded`；
- structured `time-budget-exceeded`；
- structured external `cancelled`；
- unhandled extension error -> `script-failed`；
- operation/memory violation 会 **latch**：program 即使 catch accounting exception，最终执行仍失败；
- memory accounting 不允许 release 超过已 reserve bytes。

**重要：Batch15 没有选择 Lua / JS / WASM。**

当前 Script runner 是 in-process **cooperative runtime contract**，不是 hostile-code security sandbox，也不宣称可 preempt 任意 CLR code。未来真正选择 runtime 时必须把 runtime 的 instruction/allocation/preemption 能力接到这些 budget/cancellation contract。

### 4.10 Failure isolation

覆盖：

- registration throw → scope rollback；
- missing capability → load fail + no residual registration；
- Tool throw → no document mutation；
- Command throw / invalid patch → no mutation；
- Panel build/action throw → isolated diagnostic；
- stale/different target patch → rejected before Command；
- Exporter/Importer throw → structured asset-pipeline failure；
- unload throw → registry 已先清理；
- opaque plugin data survives missing implementation。

### 4.11 Out-of-tree test plugin

`tests/MyLovePixel.TestPlugin` 只引用 `MyLovePixel.PluginSdk`。

测试明确验证它不引用：

- MyLovePixel.Core
- MyLovePixel.Commands
- MyLovePixel.PluginHost
- Avalonia

TestPlugin 注册：

- Tool
- Effect
- Exporter
- Panel

Tool preview/commit、Effect rendering、Exporter、Panel 和 unload 都通过正常 Host/Application adapter 测试。

### 4.12 Batch15 CI

关键 gates：

- Stage1 SDK/Host：`33161836016` success。
- Stage2 DLL/Application/Desktop adapters：`33162437739` success。
- Script/Importer final code gate：`33163278347` success。

Batch15 final code HEAD（文档/最终边界测试收口前）：

`da264088735793ba59e7171c1b40c9d7d9c0fc82`

ADR：`docs/DECISIONS/ADR-0010-plugin-sdk.md`。

Plugin author guide：`docs/PLUGIN_SDK.md`。

## 5. Persistence / Recovery / Plugin facts

Current `.pixelproj` schema = **5**。

MLPX codec version 仍为 1：RGBA32 4 byte/pixel；Indexed8 1 byte/pixel；Palette 在 document JSON。

Unknown JSON 和 opaque plugin ZIP payload 必须 roundtrip。Runtime revision/cache/chunks、Desktop workspace state、ExportPreset、Recovery journal、Plugin runtime registrations/diagnostics 都不属于 `.pixelproj` semantic state。

Normal project save 的 atomic guarantees 未改变：同目录 temp、write-through、reopen validation、atomic replace；失败时旧正式项目保持有效。

Autosave 发布顺序固定为：checkpoint atomic save → reload/semantic verification → journal atomic publish → rotation。

Plugin project bytes 的原则：Persistence 只保存 opaque namespaced bytes；插件缺失不影响保存。

## 6. 已知事故 / 不要重复踩坑

- 写 large file 前 fetch 当前版本，禁止拿旧整文件覆盖新代码。
- 分支出现并行 commit 时用正常 merge commit 收敛；不要 force/rewrite。
- Migration 逐级 deterministic；不要跳 schema。
- Frame Copy 要处理 built-in tracks + Effect parameter tracks，并 remap source FrameId。
- Indexed Surface ↔ Palette 是强引用不变量。
- Palette reorder 在 ColorCycle 引用存在时明确失败。
- Tilemap/Effect renderer revision 变化但 dirty history 不完整时必须 full fallback。
- Export 必须复用 Renderer，不重写 Palette/ColorCycle/Effect composition。
- UI 不要暴露 Core internal setter 或 live writable Surface。
- Recovery scan 不要放进 pointer move / Canvas refresh 高频路径。
- Recovery copy 不能自动继承正式 FilePath；必须显式 Save。
- Rotation 不能先删最后一个 verified checkpoint 再写新 generation。
- Stress test 不用机器相关 elapsed-time threshold 判 correctness。
- Plugin SDK 不得为了方便引用 Core/Commands/Application/Desktop。
- PluginHost 不得因为插件需要“高级功能”直接把 PixelDocument 交出去；应该增加窄的 immutable/declarative SDK contract。
- Panel patch 必须验证它仍是原 target/revision，不能只因为 PluginPixelPatch 本身合法就执行。
- Plugin Import metadata 没有 mapping 时明确失败，不能静默丢字段。
- Script operation/memory accounting exception 必须 latch；不能让 program catch 后继续被认为 success。
- Cooperative script budget ≠ hostile-code sandbox。不要在未选 runtime 前做这种安全承诺。

## 7. 下一开发起点

**基础编辑器计划到 Batch15 已完成。Batch16 是 Optional Advanced Modules，不应自动进入。**

只有出现明确私人工作流需求时才开始 Batch16。`docs/IMPLEMENTATION_PLAN.md` 当前候选包括：

- Audio Layer / waveform / timeline sync；
- Bone / Mesh / IK；
- 3D Guide / 3D → Pixel render；
- macro / batch composition；
- engine-specific exporters；
- procedural generators。

约束：这些模块优先通过现有 `RenderNode / AnimationTrack / Exporter / Plugin SDK` 接入，不能污染普通 Cel 模型或扩张 Core mutable API。

如果下一个需求只是一个私人工具、效果、导出器、Panel 或算法，**先尝试作为 Plugin SDK extension 实现，而不是开新的 Core feature batch。**
