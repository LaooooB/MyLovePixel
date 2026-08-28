# MyLovePixel 架构约束

这份文档是代码审查时的硬约束。功能需求可以增加，但不能通过破坏这些边界来换速度。

## 1. 依赖方向

```text
Desktop (Avalonia)
        ↓
Application ─┬─→ Tools ─────→ Commands ─────→ Core
             ├─→ Render ── read Snapshot ────→ Core
             ├─→ Persistence ─ DTO/Migration → Core
             └─→ Export ─────→ Render + Core

CLI ─────────┬─→ Persistence
             └─→ Export

Color / Animation / Tilemap / Effects
  provide domain algorithms and strategies around Core contracts
```

未来 Plugin SDK 只能接稳定公开契约，不能拿 Core internal mutable object graph。

### Core

Core 保存文档语义和稳定引用模型。

禁止依赖：Avalonia、SkiaSharp、GPU、窗口、文件对话框、具体 UI Widget、具体 Effect evaluator。

### Commands

所有可撤销 live document mutation 的唯一入口。允许依赖 Core；禁止引用菜单、按钮、Canvas 控件。

### Render

只读 `DocumentSnapshot`。Renderer 不拥有文档真值，不能修改 live Document。Revision 决定缓存正确性，Dirty Region 只决定性能；缺完整 revision history 时必须 full fallback。

### Tools

处理统一 Pointer/Input 状态机，preview 只生成 transient result；确认后通过 Command API 修改文档。Tool 不拿可写 PixelBuffer。

### Persistence

负责 `.pixelproj` 显式 DTO、Schema、Migration、Atomic Save、Unknown Field / opaque plugin payload preservation。不能把 transient UI 状态或导出产物缓存写成文档语义。

### Export

只吃 immutable `DocumentSnapshot`。最终帧像素复用 Render 的 Palette / Color Cycle / Effect 语义，之后再做 trim/crop/scale/extrude/sheet/atlas。`ExportPreset` 是独立 versioned JSON，不等同于 `.pixelproj` schema。

### Application

Application 是 UI-facing orchestration boundary。它管理 workspace/session、ActionId、file workflows、ToolHost routing、snapshot/presentation DTO。Application 可以协调 domain/services，但不能用 UI 绑定便利性绕过 Commands。

### Desktop UI

Desktop 只引用 Application。Avalonia 控件负责窗口、布局、文件选择器、平台输入和 presentation；不直接引用可写 `PixelDocument`/`PixelSurface`，不在 click handler 中实现 serializer/export/raster 算法。

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

ID 规则：Document / Layer / Frame / Cel / Resource / Palette / Tile / Tileset / Tilemap / Effect / Track / Clip / Tag / Slice 都使用稳定 ID；禁止数组下标充当长期身份。

Cel 只引用 `SurfaceId`，不直接拥有像素数组。Linked Cel = 多个 Cel 指向同一个 SurfaceId；解除 linked = 显式 clone + command 替换引用。

Tilemap Cell 不拥有 tile 像素；Tile 只引用 Surface。修改一个 Cell 不复制像素。

## 3. PixelSurface / Palette

`PixelSurface` 是像素真值：

- RGBA32 = 4 byte/pixel，无 PaletteId。
- Indexed8 = 1 byte/pixel，必须引用存在的 PaletteId。
- mutation 单调增加 Revision。
- 外部读取，写入只开放给受控 mutation assembly。
- Snapshot 是独立不可变副本。
- GPU texture / thumbnail / composites 都是可重建缓存。

Palette 是独立资源，有自己的 Revision。Transparent Index 属于 Palette。Palette reorder 必须原子 remap indexed bytes；存在 Color Cycle 引用时当前明确拒绝 reorder，而不是静默改变动画语义。

## 4. Command / Undo

```text
Input / UI Action
      ↓
Application / ToolHost
      ↓
CommandBus
 ├── Apply Command
 ├── emit DocumentChange
 ├── UndoStack
 └── Transaction
```

规则：

1. Undoable mutation 必须可 `Apply → Revert`。
2. 连续笔画/拖拽 Transform 使用 Transaction 合成一个 Undo entry。
3. 像素修改记录 patch/dirty，不复制整个 Document。
4. 新命令执行后清空 redo branch。
5. Apply 前应尽可能完成全部验证，避免半提交。
6. UI/Application 不得绕过 CommandBus 修改 live document。
7. Layer/Palette panel 若提供写入能力，必须调用相应 Command-backed session API。

## 5. Render / Effect / Cache

Renderer 输入是 `DocumentSnapshot + RenderRequest`。

Effect 是 Cel 上的 ordered non-destructive graph：参数变化不改源 Surface；Bake 通过 Command 创建新的 RGBA Surface，不能 in-place 破坏 linked source。未知 Effect 可以保存但不能在 Bake 时静默跳过。

Cache key 必须覆盖所有影响视觉结果的依赖：Surface/Palette revision、Color Cycle、Effect graph/parameter/backend revision、结构签名等。Effect-aware dirty expansion 不完整时 full fallback。

## 6. Timeline / Animation metadata

- Frame 使用稳定 FrameId。
- Duration 使用整数 tick，禁止 float 累积。
- Clip/Tag 通过 stable FrameId range 表达。
- Pivot / Hitbox / Hurtbox / Socket / Event / ColorCycle / Effect parameter animation 使用 `AnimationTrack<T>`。
- Frame copy/remove 必须同步处理所有相关 tracks，并在 copy 时 remap source FrameId。

## 7. Tilemap

```text
Tileset: TileId -> SurfaceId
Tilemap Cell: TileId + transform/variant flags
```

禁止把地图源数据退化成一张大 PixelSurface。Runtime sparse chunk 是性能结构，不进入项目语义。AutoTile 通过规则/拓扑策略工作，随机 variant 由 document seed + coordinate 稳定复现。

## 8. Import / Export

Export 的顺序：

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

- Export 期间 live document 后续编辑不能影响已经捕获的 snapshot。
- CLI 与 Desktop UI 调同一个 `ExportPipeline`。
- Stable Frame/Clip/Tag/Slice ID 不应在导出中丢失。
- Artifact path 必须是安全 relative path。
- ExportPreset 版本独立于 `.pixelproj` schema。
- PNG import 当前建立新的 RGBA32 document；不会自动猜测用户希望重建 Indexed8 palette。

## 9. Persistence

当前 `.pixelproj` schema = 5。

Migration 必须逐级确定性执行：1→2→3→4→5。Unknown JSON fields 与 opaque plugin ZIP payload 必须 roundtrip preservation。Runtime revision/cache/chunk 不进入 semantic hash。

Atomic Save 必须同目录 temp write、重新打开验证后再 replace；失败时旧文件保持有效。

Recovery/autosave journal 属于 Batch14 workspace infrastructure，默认独立 version，不因为恢复元数据升级 `.pixelproj` schema。

## 10. Desktop / Action routing

Batch13 起菜单、Toolbar、快捷键统一通过 `ActionId`。快捷键只映射 ActionId，不直接绑定 domain mutation。

`DocumentSession` 是受控 live-document 门面；Desktop 获得 Canvas/Layer/Timeline/Palette/Tool presentation 数据，不获得 mutable Document API。

Canvas Widget 只读 `CanvasPresentation`；平台 pointer 转成 `EditorPointerEvent` 后由 Application 路由 ToolHost。Preview 是 transient overlay，pointer release 才通过 CommandBus commit。

Timeline 使用 bounded `TimelineWindow`，不能按总 Frame 数创建控件树。

Dock/panel/theme/zoom/selection/preview 等 workspace presentation state 默认不属于 `.pixelproj` 文档语义，除非未来明确设计独立 workspace persistence。

## 11. Plugin SDK

Batch15 才定义 versioned Public SDK。此前内部 Registry（Effect/Exporter/Importer/Atlas/Palette/Dither/AutoTile 等）只是架构扩展边界，不宣称稳定外部 ABI。

插件持久化数据必须 namespace 化；插件缺失时未知 payload 仍需保存。

## 12. 永久不变量

- 所有稳定引用必须存在且通过 Validator。
- Indexed index 永远不越 Palette bounds。
- Frame duration > 0。
- Undo 后语义状态恢复。
- 保存文件可在无 UI 环境下通过 Validator。
- Renderer / Exporter 不拥有 live document 写权限。
- Desktop/Application 不越过 CommandBus mutation。
- Preview / Selection / GPU cache / recovery metadata 不是文档真值。

## 13. 禁止写法

- Canvas pointer handler 直接改 `byte[]` / `PixelSurface`。
- Desktop 直接引用 Core mutable document graph。
- Cel 保存自己的 RGBA 副本。
- Tilemap 用一张大图作为唯一源数据。
- 每个 Tool 自己维护 Undo。
- 导出逻辑写在按钮 click 回调。
- UI/CLI 各实现一套 exporter。
- Effect 调参直接破坏源像素。
- 数组下标作为长期 ID。
- revision 变化但 dirty history 不完整时仍强行 partial redraw。
- 直接序列化运行时对象图作为长期文件格式。
- 把 transient docking/selection/preview/cache/recovery 状态混入 `.pixelproj`。
