# MyLovePixel — Handoff

> 这是继续开发时的第一入口。先确认 `main` 当前 HEAD 和 GitHub Actions，再读本文件、`docs/IMPLEMENTATION_PLAN.md`、`docs/ARCHITECTURE.md` 与 `docs/DECISIONS/`。不要只依赖本文中的 SHA / CI ID 判断仓库是否已经继续更新。

## 1. 项目目标与永久边界

MyLovePixel 是私人游戏开发使用的可扩展 Pixel Art / Sprite 资产编辑核心。当前优先建设稳定的数据模型、算法、渲染、动画、Tilemap、导出与插件；Avalonia UI 到 Batch13 才正式进入。

技术基线：.NET 10 / C# 14、xUnit v3、SkiaSharp 4.151.1；Avalonia 12.1.1 留给后续 Desktop Shell。

永久约束：

1. UI 不直接修改 `PixelDocument`；可撤销 mutation 统一经过 Commands / `CommandBus`。
2. `Cel` 只持稳定 `ResourceId`；Linked Cel 通过共享同一 Surface 表达。
3. Tilemap Cell 只引用稳定 `TileId`；Tile 只引用稳定 `ResourceId`；Cell 不拥有像素副本。
4. `PixelSurface` 是像素真值；GPU texture、thumbnail、frame/tilemap composite 都是可重建缓存。
5. Core 不引用 Avalonia/Skia/UI。
6. Raster / AutoTile 只读 Snapshot 并输出 Patch；Renderer 只读 `DocumentSnapshot`。
7. Selection/preview 是 transient state；确认后才形成 Command。
8. Persistence 使用独立 DTO + `schemaVersion` + 逐级 Migration；未知 JSON/plugin ZIP payload 必须保留。
9. Revision 决定缓存正确性，Dirty Region 只决定局部更新性能；缺完整 revision-history 时必须 full fallback。
10. 高变化算法优先 Strategy/Registry，不把算法绑进 UI。
11. 不重写 Git 历史；分支完整 CI 全绿后才 `force:false` fast-forward `main`。

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
```

## 3. 已完成批次

Batch00–05：Repository Foundation、Document/Stable References、Command/Undo、Persistence、Raster、Selection/Transform。

### Batch06 — RenderGraph / Canvas Cache

已完成 Snapshot-only Renderer、CPU compositor、RenderGraph、exact structure signature + resource revisions、dirty partial recompose、linked-cel dirty mapping、TextureUploadPlanner、ViewTransform、overlay passes、Skia cache/dirty upload、nearest-neighbor presentation、cache diagnostics。

关键规则：缺少完整 revision→revision Dirty history 时 full fallback；hash 不是唯一 correctness 条件；overlay 不烘焙进 frame composite cache。

### Batch07 — Input / ToolHost / Brush Session

已完成统一 PointerEvent、ToolHost、StrokeSession、Pencil/Eraser/Line/Shape/Fill、keyboard modifiers、preview/cancel、stale revision protection。Pointer move 不修改 live Surface；pointer-up 一次提交一个 Undo entry。

### Batch08 — Timeline / Animation / Metadata

已完成 stable Clip/Tag/Slice/Track IDs、frame duration、linked/independent copy、move/remove、Pivot/Hitbox/Hurtbox/Socket/Event tracks、`AnimationTrack<T>`、playback clock、Onion Skin、schema2 animation persistence/migration。

### Batch09 — Palette / Indexed / Dithering

已完成 stable Palette resource、Indexed8 Surface、Transparent Index、palette remap/reorder、quantization、dither、ColorRamp、Shading Ink、Color Cycling、schema3 Palette/Indexed persistence。

架构决策：`docs/DECISIONS/ADR-0004-indexed-palette.md`。

### Batch10 — Tileset / Tilemap / AutoTile

**已完成。** Batch10 schema4/全 solution 功能 gate CI：`33143628422` — restore/build/test 全部 success。

完成内容：

- stable `TileId / TilesetId / TilemapId`；
- `Tileset / TileDefinition / Tilemap / TileCell`；
- Cell → TileId → SurfaceId 两级稳定引用，不复制 Tile pixels；
- sparse 32x32 runtime chunk，支持负坐标；
- `FlipX / FlipY / Rotate90 / Variant`；
- non-square Tile 禁止 `Rotate90`，避免 Renderer/Exporter 尺寸语义分叉；
- `SetTileCellCommand`；
- `EditTilePixelsCommand`：修改共享 Surface，所有引用 Cell 同步观察；
- `MakeUniqueTileCommand`：显式 clone Surface + 新 TileId，只重连目标 Cell；
- undoable、安全引用检查的 Tile resource GC；
- multi-cell patch 一次 Undo，所有目标先预验证再 mutation；
- 新 `MyLovePixel.Tilemap` 算法程序集；
- `IGridTopology`：Rect / Isometric Diamond / Hex Odd Row；
- `IAutoTileRule`、4/8-neighbor bitmask；
- weighted variant 使用 document seed + coordinate 稳定复现，不依赖进程 RNG/UI 时序；
- AutoTile 只读 immutable Snapshot，输出 Cell patch；
- Rect Tilemap CPU renderer；
- Tilemap revision dirty-cell cache；
- Tile Surface revision 反向映射所有引用 Cell；
- Indexed8 Tile 通过 Palette 合成；
- 缺 Tilemap/Surface revision invalidation history 时 full fallback；
- cache clear 后可完全由 Snapshot 重建；
- `.pixelproj` schema4：`document.seed`、`tilesets[]`、`tilemaps[]`、sparse `cells[]`；
- runtime chunk 布局不进入项目文件/semantic hash；
- deterministic schema3→4 migration；
- Tileset/Tile/Tilemap/Cell unknown JSON roundtrip preserve；
- duplicate cell coordinates / unknown transform flags 在 load 时结构化拒绝。

Batch10 分阶段 CI：

- Core resource/sparse map：`33142182783` success
- Commands/resource lifetime：`33142334793` success
- GridTopology/AutoTile：`33142493534` success
- Patch/renderer 后最终功能基线：`33142919799` success
- schema4 + Persistence tests：`33143628422` success

架构决策：`docs/DECISIONS/ADR-0005-tilemap-reference-model.md`。

## 4. 当前 Persistence 事实

Current schema = **4**。

Migration 链：

- schema1→2：Animation metadata + stable built-in Track IDs。
- schema2→3：Palette/Indexed + Color Cycle Track。
- schema3→4：deterministic document seed + empty Tileset/Tilemap collections for legacy documents。

schema4 新语义：

- `document.seed` 显式保存，AutoTile 等确定性算法不依赖运行时随机源；
- `tilesets[]` 保存 stable Tile ID、Tile Surface reference 与 tile size；
- `tilemaps[]` 保存 Tileset reference、topology ID 和排序后的 sparse `cells[]`；
- 32x32 chunk 仅是 runtime storage，不序列化，因此未来可调整 chunk/storage 而不迁移文件；
- Tilemap semantic hash 包含 Seed、Tileset/Tile/Cell 语义，但不包含 runtime Revision/chunk layout；
- unknown JSON / opaque plugin ZIP payload 的向前兼容要求继续成立。

MLPX codec version 仍为 1：RGBA32 payload 4 byte/pixel；Indexed8 payload 1 byte/pixel。Palette 数据仍只在 document JSON 保存。

## 5. Batch10 的核心不变量

1. Cell 永远不持有 RGBA/index pixel arrays。
2. Edit Tile Pixels 修改 Tile 引用的 Surface；共享引用是默认语义。
3. Make Unique 是显式操作，不做隐式 copy-on-write。
4. 被 Cell 引用的 Tile、被 Tile 引用的 Surface、被 Tilemap 引用的 Tileset 不得提前回收。
5. AutoTile 计算与文档 mutation 分层：Snapshot → patch → CommandBus。
6. `IGridTopology` 不改变 Core Tilemap 存储模型。
7. Renderer correctness 由 exact structure + revisions + 连续 invalidation history 决定；Dirty 只是优化。
8. Rect renderer 已完成；Iso/Hex 当前完成 topology math，不应误称已完成完整 Iso/Hex compositing。
9. Persistence 保存 sparse cells，不保存 chunk。

## 6. 已知事故 / 不要重复踩坑

- 不要用陈旧整文件覆盖 Core；Batch05 曾因此误删 Persistence API 并触发 CI 回归。
- 新增程序集名称可能与 Core 类型名冲突；Batch10 的 `MyLovePixel.Tilemap` 曾让测试里的 `Tilemap` 类型产生 namespace/type 歧义，必要时用完整类型名。
- Skia bitmap lifetime 由 `SkiaFrameCache` 明确拥有。
- schema migration 必须逐级、确定性，禁止跳版本。
- Indexed Surface index 与 Palette 是强引用不变量。
- Palette reorder 在存在 Color Cycle 引用时当前明确失败；未来若支持必须原子 remap cycle semantics。
- Tilemap partial renderer 不能只看 Dirty；revision 变了但 history 不完整必须 full fallback。
- 不要把 `Tilemap.ChunkSize` 写进 exporter/project schema 作为资产语义。

## 7. 下一开发起点：Batch11 — Effect Graph

目标：Outline、Shadow、Palette Map 等作为非破坏参数存在，不改源 PixelSurface。

进入实现前先解决：

1. `EffectDescriptor / EffectInstance` 的 stable ID、生命周期与参数 schema。
2. Effect 参数是 Document 数据还是 transient preview；哪些必须持久化。
3. CPU/GPU evaluator 的共同输入/输出契约，禁止 evaluator 拿 live mutable document。
4. Effect cache key：source revision、effect parameter revision、palette/metadata dependencies 如何进入 correctness。
5. animated parameter binding 如何复用 `AnimationTrack<T>` 而不把 effect 特例塞进 Timeline。
6. Bake Effect 必须通过 Command，Undo 恢复原 Surface/reference。
7. 未安装未知 Effect 时项目仍能 load/save 并完整保留 payload。
8. Persistence schema5 是否需要；先定义 migration 和 unknown-effect forward compatibility，再写 runtime model。
9. Renderer 中 Effect Node 的位置与 cache invalidation 边界。
10. Plugin SDK 还没到 Batch15，因此 Batch11 只建立可注册接口，不提前暴露不稳定公共插件 ABI。

继续时：先 fetch `main` 和最新 CI，再读本文件与 ADR。若 Batch10 已合入 main，则从 main 建 Batch11 分支。没有 CI 绿灯不要把 Batch11 标为完成。
