# MyLovePixel — Handoff

> 继续开发时先确认 `main` HEAD 与最新 GitHub Actions，再读本文件、`docs/IMPLEMENTATION_PLAN.md`、`docs/ARCHITECTURE.md` 和 `docs/DECISIONS/`。不要仅凭本文记录的 SHA 判断仓库是否已经继续更新。

## 1. 项目目标与永久边界

MyLovePixel 是私人游戏开发使用的可扩展 Pixel Art / Sprite 编辑器。技术基线：.NET 10 / C# 14、xUnit v3、SkiaSharp、Avalonia。

永久约束：

1. UI 不直接修改 `PixelDocument`；undoable mutation 统一经过 Commands / `CommandBus`。
2. Cel 只持稳定 `ResourceId`；Linked Cel 通过共享 Surface 表达。
3. Tilemap Cell 只引用稳定 `TileId`；Tile 只引用 `ResourceId`；Cell 不拥有像素副本。
4. `PixelSurface` 是像素真值；GPU texture、thumbnail、composite 都是可重建缓存。
5. Core 不引用 Avalonia/Skia/UI/Recovery，也不包含具体 Effect evaluator。
6. Raster / AutoTile / Effect / Renderer / Export 只读 Snapshot 或 immutable input。
7. Selection / preview / docking / zoom / recovery / diagnostics 是 transient workspace/runtime state，不进入 `.pixelproj`。
8. Persistence 使用显式 DTO + schemaVersion + 逐级 Migration；unknown JSON/plugin ZIP payload 必须保留。
9. Revision 决定缓存正确性，Dirty Region 只决定性能；历史不完整时 full fallback。
10. 高变化算法优先 Strategy/Registry。
11. Export UI/CLI 共用 `ExportPipeline`；click handler 不实现导出算法。
12. Desktop 只依赖 Application；Application 协调 Tools/Commands/Render/Persistence/Recovery/Export。
13. 不重写 Git 历史；feature branch 全 CI 绿后才 `force:false` fast-forward `main`。

## 2. 当前 solution / schema

核心与算法项目：Core、Commands、Persistence、Recovery、Raster、Selection、Render、Tools、Animation、Color、Tilemap、Effects、Export、Application、Desktop、Cli。

测试项目覆盖 Core、Persistence、Recovery、Raster、Selection、Render、Tools、Animation、Color、Tilemap、Effects、Export、Application。

当前 `.pixelproj` schema = **5**。

Migration：1→2 Animation；2→3 Palette/Indexed/ColorCycle；3→4 seed + Tileset/Tilemap；4→5 Cel effects。

Recovery journal 是独立 versioned workspace format，**没有**升级 `.pixelproj` schema。

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

Batch13 merged main baseline：`3257ff64c20b61a2a3f65febe2e8c33c53a1022a`；merge 后 main CI `33153498128` success。

## 4. Batch14 — Autosave / Recovery / Performance Hardening：完成

分支：`batch14-hardening`。

### Recovery subsystem

新增 `MyLovePixel.Recovery`：

- `RecoveryOptions / RecoveryCandidate / RecoveryDiscovery`；
- versioned recovery journal；
- autosave checkpoint 复用正常 `.pixelproj` writer；
- checkpoint 保存后重新 Load + semantic hash 验证；
- verified checkpoint 之后才 atomically publish journal；
- 新 journal commit 后才 retention rotation；
- candidate structured states：Valid / InvalidJournal / MissingCheckpoint / CorruptCheckpoint / SemanticMismatch；
- older valid candidate 不会被损坏的新 candidate 遮蔽；
- `RecoveryStore.Recover`；
- `RecoveryStore.Discard` 先删 journal、再删 checkpoint；
- failure injection stages：BeforeCheckpoint / AfterCheckpointValidated / BeforeJournalCommit / AfterJournalCommit / BeforeRotation / AfterRotation。

Crash matrix 已覆盖所有六个 injection stage：任一步失败后至少仍有一个 verified recovery point 可加载；journal 已提交后的失败必须能发现新 checkpoint，提交前失败则旧 checkpoint 仍是有效恢复点。

### Application / Desktop Recovery workflow

新增 `RecoveryWorkspaceCoordinator`：

- `AutosavePolicy` 默认 2 分钟，retention 默认 3；
- 只 autosave dirty sessions；
- per-document interval tracking；
- structured autosave result；
- recovery discovery presentation；
- recover / dismiss workflow。

Recovered session 语义：

- `IsRecovered = true`；
- `IsDirty = true`；
- `FilePath = null`；
- original source path 只作为 `RecoverySourcePath` 提示；
- 用户显式 Save/Save As 后才清除 recovery state 并建立正常 FilePath。

Desktop：

- 30 秒 timer 触发 Application autosave tick；真正 policy 仍是 2 分钟；
- startup/Autosave/Recover/Dismiss 边界刷新 Recovery panel；
- 高频 `RefreshAll` / pointer move **不**做 recovery disk scan；
- Recovery panel 提供 Recover/Dismiss；
- recovered title/status 明确显示 recovery copy。

### Undo memory budget

`CommandBus` 支持 `UndoHistoryOptions`：

- configurable byte budget；
- `IUndoMemoryCost`；
- PixelPatch 按 retained undo patch 估算成本；
- 超预算从最旧 committed undo entries 回收；
- redo branch accounting；
- transaction 语义不变；
- 单个最新 entry 可暂时超软预算，确保刚执行的动作仍可 Undo；
- `UndoHistoryDiagnostics` 暴露 budget / estimated bytes / evictions / counts / over-budget。

### Thumbnail LRU

Render 新增 `ThumbnailCache`：

- immutable rendered RGBA thumbnails；
- `MaxEntries + MaxBytes` 双预算；
- true LRU；
- stable FrameId + visual structure/revision-aware key；
- nearest-neighbor resize；
- oversize bypass；
- hit/miss/eviction/bytes/hit ratio diagnostics。

新增 `RenderCacheRates / GetRates()`，从 FrameRenderer diagnostics 对外提供 request/miss/hit/hit-ratio summary。

### Dirty render diagnostics

`DocumentSession` 现在累计 `DocumentChange.DirtySurfaces`，并基于 last-rendered Surface revision 生成 revision-covering `SurfaceInvalidation`。

关键规则：

- 多个 revision 在下一次 render 前可合并 dirty region，但 revision coverage 不能丢；
- Renderer 仍负责最终安全判断；history 不完整就 full fallback；
- render 成功后才清 pending invalidations；
- dirty visualization 来自实际 Partial `TextureUploadPlan.Regions`；
- overlay 是 transient，不改 Surface/Project/Export。

Desktop Diagnostics 显示 render cache outcome、partial/full/hit、upload pixels、Undo memory/evictions；可切换 dirty-region overlay。

### Stress fixtures

Headless stress coverage：

- 1000-frame `TimelineWindow`：总帧 1000 时仍只物化请求的 24 items；
- 1000-frame thumbnail sweep：64-entry / 256-byte cache 最终严格保持预算，936 次 LRU eviction；
- 10,000 sparse Tilemap cells：625 chunks、一个 shared Tile Surface、一个 patch Undo entry，Undo 后 chunks 清空且 Surface 数不变；
- 5,000 repeated pixel Commands：Undo history 始终受 4096-byte budget 限制，旧 entries 被回收；
- Recovery 六阶段 crash matrix。

测试不依赖 wall-clock timing 阈值，只验证资源上限、引用模型和恢复正确性。

### Key Batch14 CI

- Recovery Stage1：`33153809269` success。
- Undo/Thumbnail corrected Stage2：`33154253929` success。
- Recovery UI / partial render Stage3：`33159499189` success。
- Final stress/code gate：`33160003149` success。

Batch14 final code HEAD（文档收口前）：`fe3b944c3657447b92719caf501cc1e3aad86c26`。

ADR：`docs/DECISIONS/ADR-0009-recovery-performance-hardening.md`。

## 5. Persistence / Recovery facts

Current `.pixelproj` schema = **5**。

MLPX codec version 仍为 1：RGBA32 4 byte/pixel；Indexed8 1 byte/pixel；Palette 在 document JSON。

Unknown JSON 和 opaque plugin ZIP payload 必须 roundtrip。Runtime revision/cache/chunks、Desktop workspace state、ExportPreset、Recovery journal 都不属于 `.pixelproj` semantic state。

Normal project save 的 atomic guarantees 未改变：同目录 temp、write-through、reopen validation、atomic replace；失败时旧正式项目保持有效。

Autosave 发布顺序固定为：checkpoint atomic save → reload/semantic verification → journal atomic publish → rotation。

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

## 7. 下一开发起点：Batch15 — Plugin / Script SDK

目标：以后定制功能优先通过稳定扩展点增长，而不是继续修改 Core 或让插件获得裸 mutable document。

先解决以下设计，再写 host：

1. **SDK versioning**：定义 `PluginApiVersion`、host compatibility policy、最低/最高支持版本；禁止把内部 assembly 版本直接当 public SDK contract。
2. **Plugin identity/lifecycle**：稳定 `PluginId`、manifest、load/unload/error isolation、duplicate id policy。
3. **Capability model**：插件只能拿窄接口；不能获得 `PixelDocument` internal mutation、raw GPU lifetime、任意 filesystem/UI handle。
4. **Registration model**：Tool / Command / Effect / Exporter / Importer / Panel / Palette / Dither / AutoTile 统一 registry contract；registration token 支持卸载。
5. **Mutation boundary**：plugin Tool/Panel 最终写入仍必须产生 Command/Transaction；不能绕过 Undo/Dirty/Revision。
6. **Persistence namespace**：plugin project data 使用 namespaced opaque payload；插件缺失时仍 roundtrip；插件重装后可重新解释。
7. **Effect/export integration**：插件 Effect evaluator/Exporter 只吃 immutable contract；unknown plugin Effect 继续遵守现有 preserve-but-no-silent-bake 规则。
8. **Panel boundary**：Panel 插件不要直接暴露 Avalonia control 作为 SDK 核心契约；先定义 UI-neutral panel/session contract，再做 Desktop adapter。
9. **Script host**：Lua/JS/WASM 不要在 Stage1 随便选。先定义 sandbox API、budget/cancellation、determinism、serialization；根据实际私人工具需求再选 runtime。
10. **Failure isolation/diagnostics**：一个插件 throw 不应损坏 Document/registry；注册和执行错误要有 structured diagnostics。
11. **Tests**：外部 test plugin 在不改 Core 源码情况下注册 Tool + Effect + Exporter；卸载后 registry 清理；unknown payload roundtrip；plugin mutation Undo 正确。
12. ADR + HANDOFF + full CI，最后 `force:false` fast-forward main。

Batch15 Definition of Done：

- Public SDK assembly/namespace 边界明确并 versioned；
- 至少一个 out-of-tree style test plugin 能注册 Tool + Effect + Exporter；
- 插件不能获得裸 mutable Document；
- plugin mutation 仍通过 CommandBus；
- plugin data 缺失实现时仍能保存；
- unload 不留下 registry dangling entries；
- plugin failure 不破坏正常 editor state；
- Core 不反向依赖 Plugin host/Desktop；
- full CI green 后才合 main。