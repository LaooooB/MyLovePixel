# MyLovePixel 架构约束

这份文档是代码审查时的硬约束。功能需求可以增加，但不能通过破坏这些边界换取开发速度。

## 1. 依赖方向

```text
Desktop (Avalonia)
        ↓
Application ─┬─→ Tools ─────→ Commands ─────→ Core
             ├─→ Render ── read Snapshot ────→ Core
             ├─→ Persistence ─ DTO/Migration → Core
             ├─→ Recovery ───→ Persistence
             ├─→ Export ─────→ Render + Core
             └─→ PluginHost ─→ Commands / Effects / Export / Persistence / Render / Core
                                  ↑
External Plugin ─→ PluginSdk ─────┘  (only through Host adapters)

CLI ─────────┬─→ Persistence
             └─→ Export

Color / Animation / Tilemap / Effects
  provide domain algorithms and strategies around Core contracts
```

`MyLovePixel.PluginSdk` 是唯一 public plugin contract assembly，只依赖 BCL。外部插件不能引用 Core mutable graph；`PluginHost` 是内部适配层，允许依赖内部模块但不属于插件 ABI。

### Core

Core 保存文档语义和稳定引用模型。禁止依赖 Avalonia、SkiaSharp、GPU、窗口、文件对话框、具体 UI Widget、具体 Effect evaluator、Recovery、PluginHost/PluginSdk。

### Commands

所有可撤销 live document mutation 的唯一入口。允许依赖 Core；禁止引用 UI。Command 必须明确 Apply/Revert，尽量在 Apply 前完成全部验证。

### Render

只读 `DocumentSnapshot`。Renderer 不拥有文档真值，不能修改 live Document。Revision 决定缓存正确性，Dirty Region 只决定性能；dirty history 不完整时必须 full fallback。

### Tools

处理统一 Pointer/Input 状态机。Preview 只生成 transient result；确认后经 Command API 修改文档。Tool 不获得可写 PixelBuffer。

### Persistence

负责 `.pixelproj` 显式 DTO、Schema、Migration、Atomic Save、Unknown JSON / opaque plugin payload preservation。不能把 runtime cache、selection、preview、recovery metadata 等 transient 状态写成文档语义。

### Recovery

Recovery 负责 autosave checkpoint、独立 versioned journal、candidate discovery、retention 和 crash-safe recovery。Recovery 复用 Persistence 保存/加载/validator/semantic hash，不定义第二套项目序列化格式，也不升级 `.pixelproj` schema。

### Export

只吃 immutable `DocumentSnapshot`。最终帧像素复用 Render 的 Palette / Color Cycle / Effect 语义，再做 trim/crop/scale/extrude/sheet/atlas。`ExportPreset` 是独立 versioned JSON。

### PluginSdk / PluginHost

PluginSdk 暴露 versioned BCL-only DTO/interface：Plugin identity/capability、Tool/Command/Effect/Exporter/Importer/Panel/Palette/Dither/AutoTile、opaque project data 与 runtime-neutral Script contract。

PluginHost 负责把这些 immutable/declarative contract 适配到内部 CommandBus、Effect engine、Render/Export、Persistence。插件 raster mutation 必须是 target/revision-bound `PluginPixelPatch`，由 Host 转成 Command；Host 不把 `PixelDocument`、可写 `PixelSurface`、GPU lifetime、Avalonia control 或任意 filesystem handle 交给插件。

### Application

Application 是 UI-facing orchestration boundary。它管理 workspace/session、ActionId、ToolHost routing、file/recovery workflows、snapshot/presentation DTO，并负责把 Command dirty history 转换成 Render invalidation。Application 还提供 PluginHost 的 Tool/Panel/Export presentation adapter，但不得为了 UI 或插件便利绕过 Commands。

### Desktop UI

Desktop 只引用 Application。Avalonia 控件负责窗口、布局、平台输入、timer 和 presentation；不直接访问 mutable `PixelDocument`/`PixelSurface`，不在 click handler 中实现 serializer/export/raster/recovery/plugin domain 算法。Plugin panel 的 Avalonia adapter 只消费 Application presentation DTO，Avalonia 不进入 PluginSdk ABI。

## 2. 核心引用模型

```text
PixelDocument
 ├── CanvasSpec
 ├── Layer order / Frames
 ├── Cels[layerId, frameId]
 │    ├── SurfaceId
 │    └── EffectGraph
 ├── ResourceStore
 │    ├── PixelSurface (RGBA32 / Indexed8)
 │    ├── Palette
 │    ├── Tileset -> TileId -> SurfaceId
 │    └── Tilemap -> Cell -> TileId
 └── AnimationMetadata
      ├── Clip / Tag / Slice
      └── stable AnimationTrack<T>
```

Document / Layer / Frame / Cel / Resource / Palette / Tile / Tileset / Tilemap / Effect / Track / Clip / Tag / Slice 使用稳定 ID；禁止数组下标作为长期身份。

Cel 只引用 `SurfaceId`。Linked Cel = 多个 Cel 指向同一 Surface；解除 linked = 显式 clone + Command 替换引用。

Tilemap Cell 不拥有像素；Tile 只引用 Surface。修改 Cell 不复制 Tile 像素。

## 3. PixelSurface / Palette

`PixelSurface` 是像素真值：

- RGBA32 = 4 byte/pixel，无 PaletteId；
- Indexed8 = 1 byte/pixel，必须引用存在的 PaletteId；
- mutation 单调增加 Revision；
- Snapshot 是独立不可变副本；
- GPU texture / thumbnail / composite 都是可重建缓存。

Palette 是独立资源，有自己的 Revision。Transparent Index 属于 Palette。Palette reorder 必须原子 remap indexed bytes；存在 Color Cycle 引用时当前明确拒绝 reorder，不能静默改变动画语义。

## 4. Command / Undo

```text
Input / UI Action / Plugin Patch
      ↓
Application / ToolHost / PluginHost
      ↓
CommandBus
 ├── Apply Command
 ├── emit DocumentChange
 ├── UndoStack
 └── Transaction
```

规则：

1. Undoable mutation 必须可 `Apply → Revert`。
2. 连续笔画/拖拽 Transform 用 Transaction 合成一个 Undo entry。
3. 像素修改记录 patch/dirty，不复制整个 Document。
4. 新命令执行后清空 redo branch。
5. UI/Application/PluginHost 不得绕过 CommandBus。
6. Layer/Palette panel 写入必须调用 command-backed session API。
7. Undo history 有可配置 byte budget；超过预算从最旧 committed undo entry 回收。
8. 单个最新 entry 可暂时超过软预算，以保证刚执行的操作仍可 Undo；该状态必须可诊断。
9. Active transaction 不因 budget trimming 改变提交/回滚语义。
10. Plugin patch 必须绑定 Host 发给扩展的同一 SurfaceId + Revision；不同 target/stale patch 在 Command 前拒绝。

## 5. Render / Effect / Cache

Renderer 输入是 `DocumentSnapshot + RenderRequest`。

Effect 是 Cel 上 ordered non-destructive graph：调参不改源 Surface；Bake 通过 Command 创建新的 RGBA Surface，不能 in-place 破坏 linked source。未知 Effect 可以保存，但 Bake 不能静默跳过。

Cache key 必须覆盖所有影响视觉结果的依赖：Surface/Palette revision、Color Cycle、Effect graph/parameter/backend revision、结构签名等。Effect-aware dirty expansion 不完整时 full fallback。Plugin Effect registry/backend revision 同样属于配置 revision。

`DocumentSession` 只把已知 `DocumentChange.DirtySurfaces` 聚合成 revision-covering `SurfaceInvalidation`；最终是否 partial recompose 由 Renderer 决定。

Thumbnail cache：

- 只保存 immutable rendered RGBA thumbnail；
- 同时受 entry count 和 byte budget 限制；
- 使用 true LRU eviction；
- 单个超预算 thumbnail 直接 bypass cache；
- diagnostics 暴露 hit/miss/eviction/bytes/rates。

Dirty-region visualization 只能来自实际 partial upload plan，是 transient diagnostic overlay，不影响 Render 输出真值、Export 或 Persistence。

## 6. Timeline / Animation metadata

- Frame 使用稳定 FrameId。
- Duration 使用整数 tick，禁止 float 累积。
- Clip/Tag 使用 stable FrameId range。
- Pivot / Hitbox / Hurtbox / Socket / Event / ColorCycle / Effect parameter animation 使用 `AnimationTrack<T>`。
- Frame copy/remove 必须同步所有相关 tracks，并在 copy 时 remap source FrameId。
- Timeline UI 使用 bounded `TimelineWindow`，不能按总帧数创建完整控件树。

## 7. Tilemap

```text
Tileset: TileId -> SurfaceId
Tilemap Cell: TileId + transform/variant flags
```

禁止把地图源数据退化成一张大 PixelSurface。Sparse chunk 是 runtime 性能结构，不进入项目语义。AutoTile 通过规则/拓扑策略工作，weighted variant 由 document seed + coordinate 稳定复现。

## 8. Import / Export

```text
DocumentSnapshot
   ↓
FrameRenderer (RGBA/Indexed/Palette/ColorCycle/Effect)
   ↓
crop → trim → nearest scale
   ↓
sprite sheet / deterministic atlas packer
   ↓
PNG + JSON metadata artifacts
```

规则：

- Export 期间 live document 后续编辑不能影响已捕获 snapshot。
- CLI 与 Desktop 共用 `ExportPipeline`。
- Plugin Exporter 同样消费 normal FrameRenderer 输出，不能重写一套 Palette/ColorCycle/Effect composition。
- Stable Frame/Clip/Tag/Slice ID 不应在导出中丢失。
- Artifact path 必须是安全 relative path。
- ExportPreset 版本独立于 `.pixelproj` schema。
- PNG import 当前建立 RGBA32 document，不自动猜测 Indexed8 palette。
- Plugin Importer API 1.0 只映射 zero-origin RGBA image；无 lossless mapping 的 metadata 明确拒绝，不 silent drop。

## 9. Persistence / Recovery

当前 `.pixelproj` schema = **5**。

Migration 必须逐级确定性执行：1→2→3→4→5。Unknown JSON fields 与 opaque plugin ZIP payload 必须 roundtrip。Runtime revision/cache/chunk/recovery journal 不进入 semantic hash。

Plugin project data 使用 `PluginId` namespace 下的 opaque payload。插件缺失时 Persistence 不解释也不删除这些 bytes；因此 Batch15 不升级 schema。

正常项目 Atomic Save 必须同目录 temp write、重新打开验证后再 replace；失败时旧文件保持有效。

Autosave/Recovery 使用以下发布顺序：

```text
atomic checkpoint write
      ↓
reopen + semantic hash validation
      ↓
atomic recovery journal publish
      ↓
retention rotation
```

规则：

1. Recovery journal 独立 version，不升级 `.pixelproj` schema。
2. rotation 只能发生在新 journal 已提交之后。
3. journal 失效、checkpoint 缺失/损坏、semantic mismatch 必须成为结构化 candidate state；不能阻止发现更旧有效恢复点。
4. recovered project 作为 detached dirty copy 打开，`FilePath = null`；source path 只作为提示元数据。
5. 只有用户显式 Save/Save As 后，recovered session 才成为正常项目并清除 recovery 状态。
6. Dismiss 先删除 journal，再删除 checkpoint；最坏只留下 orphan checkpoint，不留下 dangling journal。
7. Recovery 扫描不能进入 pointer-move/Canvas 高频刷新路径。

## 10. Desktop / Diagnostics

菜单、Toolbar、快捷键统一通过 `ActionId`。快捷键只映射 ActionId，不直接绑定 domain mutation。

`DocumentSession` 是受控 live-document 门面；Desktop 只获得 Canvas/Layer/Timeline/Palette/Tool/Recovery/Plugin presentation 数据。

Canvas pointer 转为 `EditorPointerEvent` 后由 Application 路由 ToolHost。Preview 是 transient overlay，release 才经 CommandBus commit。Plugin Tool 由 Application 的 PluginWorkspaceRuntime 走同样的 preview/Command 边界。

Autosave timer 可以由 Desktop 触发，但策略与恢复语义属于 Application/Recovery。Recovery UI 只能调用 Application coordinator。

Diagnostics 包括 Render cache hit/miss/rates、partial/full recompose、texture upload pixels、Undo byte budget/eviction、thumbnail LRU stats 和 Plugin structured diagnostics。Diagnostics 不进入 `.pixelproj`。

## 11. Stress / Failure testing

Hardening tests 不使用机器相关的 elapsed-time 阈值作为正确性条件，而验证结构上限：

- 1000-frame Timeline 仍只物化请求窗口；
- 1000-frame thumbnail sweep 受 LRU entry/byte budget 限制；
- 10,000 sparse Tilemap cells 保持 chunk/reference 模型，Undo 不复制 tile pixels；
- 5,000 repeated commands 后 Undo history 仍受 byte budget 限制；
- Recovery 在每个 write/verify/journal/rotation failure injection stage 后至少保留一个 verified candidate。

Plugin failure tests additionally assert registration rollback, execution isolation, unload registry cleanup, stale/different target rejection, opaque payload roundtrip, out-of-tree SDK-only references and script budget termination.

## 12. Plugin / Script SDK

Public Plugin API current version = **1.0**。

核心规则：

1. `MyLovePixel.PluginSdk` 是唯一稳定 public contract；不引用任何 MyLovePixel internal assembly、Avalonia、SkiaSharp。
2. `PluginManifest` 明确 PluginId、plugin version、API min/max 和 capability；不把 assembly version 当 SDK compatibility。
3. PluginHost 统一管理 Tool / Command / Effect / Exporter / Importer / Panel / Palette / Dither / AutoTile registry；registration token 决定 unload lifecycle。
4. 注册中途 throw 必须 rollback 已注册 extension；unload 后 registry 不留 dangling instance。
5. Plugin raster input 是 immutable copied target；mutation 是 declarative patch，最终只能经 CommandBus。
6. Plugin Effect/Exporter/Importer 只吃 immutable contract；Exporter 复用 FrameRenderer。
7. Plugin project bytes namespaced opaque preserve；missing implementation 不影响 load/save。
8. Panel 核心契约是 UI-neutral model/action，不是 Avalonia control。
9. Script API 不在 1.0 选择 Lua/JS/WASM；先固定 operation/accounted-memory/time/cancellation/determinism/PluginValue serialization contract。
10. Script budget 是 cooperative runtime contract，不宣称 hostile-code CLR sandbox。未来 runtime 必须把 instruction/allocation/preemption 对接这些限制。
11. Plugin failures 使用 `PluginDiagnostic` / existing asset-pipeline structured errors，不 silent repair Document。

详细决策见 `docs/DECISIONS/ADR-0010-plugin-sdk.md`，插件作者入口见 `docs/PLUGIN_SDK.md`。

## 13. 永久不变量

- 所有稳定引用存在且通过 Validator。
- Indexed index 永不越 Palette bounds。
- Frame duration > 0。
- Undo 后语义状态恢复。
- 保存文件可在无 UI 环境下通过 Validator。
- Renderer / Exporter / Plugin extension 不拥有 live document 写权限。
- Desktop/Application/PluginHost 不越过 CommandBus mutation。
- Revision correctness 优先于 dirty optimization。
- Preview / Selection / GPU cache / thumbnail / recovery metadata / diagnostics 不是文档真值。
- Missing plugin 不得导致 namespaced opaque project payload 丢失。

## 14. 禁止写法

- Canvas pointer handler 直接改 `byte[]` / `PixelSurface`。
- Desktop 直接引用 Core mutable document graph。
- PluginSdk 引用 Core/Commands/PluginHost/Avalonia/SkiaSharp。
- Plugin 返回 patch 后 Host 不检查 target/revision 就执行。
- Plugin Panel 直接返回 Avalonia control 作为 public SDK contract。
- 把 cooperative script budget 宣称为 hostile-code security sandbox。
- Cel 保存自己的 RGBA 副本。
- Tilemap 用一张大图作为唯一源数据。
- 每个 Tool 自己维护 Undo。
- 导出逻辑写在按钮 click 回调。
- UI/CLI 各实现一套 exporter。
- Effect 调参直接破坏源像素。
- 数组下标作为长期 ID。
- revision 变化但 dirty history 不完整时强行 partial redraw。
- 直接序列化运行时对象图作为长期文件格式。
- Autosave 用未验证 checkpoint 覆盖最后一个有效恢复点。
- Recovery 自动把 checkpoint 覆盖回 source project。
- 把 docking/selection/preview/cache/recovery/diagnostics 混入 `.pixelproj`。
