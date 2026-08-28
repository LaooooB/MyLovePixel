# MyLovePixel — Handoff

> 这是继续开发时的第一入口。先确认 `main` 当前 HEAD 和 GitHub Actions，再读本文件、`docs/IMPLEMENTATION_PLAN.md`、`docs/ARCHITECTURE.md` 与 `docs/DECISIONS/`。不要只依赖本文中的 SHA / CI ID 判断仓库是否已经继续更新。

## 1. 项目目标与永久边界

MyLovePixel 是私人游戏开发使用的可扩展 Pixel Art / Sprite 资产编辑核心。当前优先建设稳定的数据模型、算法、渲染、动画、Tilemap、Effect、导出与插件；Avalonia UI 到 Batch13 才正式进入。

技术基线：.NET 10 / C# 14、xUnit v3、SkiaSharp 4.151.1；Avalonia 12.1.1 留给后续 Desktop Shell。

永久约束：

1. UI 不直接修改 `PixelDocument`；可撤销 mutation 统一经过 Commands / `CommandBus`。
2. `Cel` 只持稳定 `ResourceId`；Linked Cel 通过共享同一 Surface 表达。
3. Tilemap Cell 只引用稳定 `TileId`；Tile 只引用稳定 `ResourceId`；Cell 不拥有像素副本。
4. `PixelSurface` 是像素真值；GPU texture、thumbnail、frame/tilemap/effect composite 都是可重建缓存。
5. Core 不引用 Avalonia/Skia/UI，也不包含具体 Effect evaluator 算法。
6. Raster / AutoTile / Effect evaluation 只读 Snapshot；Renderer 只读 `DocumentSnapshot`。
7. Selection/preview 是 transient state；确认后才形成 Command。
8. Persistence 使用独立 DTO + `schemaVersion` + 逐级 Migration；未知 JSON/plugin ZIP payload 必须保留。
9. Revision 决定缓存正确性，Dirty Region 只决定局部更新性能；缺完整 revision-history 时必须 full fallback。
10. 高变化算法优先 Strategy/Registry，不把算法绑进 UI。
11. Effect preview 不修改 live Surface；Bake 必须经过 Command，并且不能破坏 linked source。
12. 不重写 Git 历史；分支完整 CI 全绿后才 `force:false` fast-forward `main`。

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
```

## 3. 已完成批次

Batch00–05：Repository Foundation、Document/Stable References、Command/Undo、Persistence、Raster、Selection/Transform。

### Batch06 — RenderGraph / Canvas Cache

已完成 Snapshot-only Renderer、CPU compositor、RenderGraph、exact structure signature + resource revisions、dirty partial recompose、linked-cel dirty mapping、TextureUploadPlanner、ViewTransform、overlay passes、Skia cache/dirty upload、nearest-neighbor presentation、cache diagnostics。

### Batch07 — Input / ToolHost / Brush Session

已完成统一 PointerEvent、ToolHost、StrokeSession、Pencil/Eraser/Line/Shape/Fill、keyboard modifiers、preview/cancel、stale revision protection。Pointer move 不修改 live Surface；pointer-up 一次提交一个 Undo entry。

### Batch08 — Timeline / Animation / Metadata

已完成 stable Clip/Tag/Slice/Track IDs、frame duration、linked/independent copy、move/remove、Pivot/Hitbox/Hurtbox/Socket/Event tracks、`AnimationTrack<T>`、playback clock、Onion Skin、schema2 animation persistence/migration。

### Batch09 — Palette / Indexed / Dithering

已完成 stable Palette resource、Indexed8 Surface、Transparent Index、palette remap/reorder、quantization、dither、ColorRamp、Shading Ink、Color Cycling、schema3 Palette/Indexed persistence。

架构决策：`docs/DECISIONS/ADR-0004-indexed-palette.md`。

### Batch10 — Tileset / Tilemap / AutoTile

已完成 stable Tile/Tileset/Tilemap IDs、两级引用、sparse runtime chunk、Cell transforms、Tile commands/GC、GridTopology、AutoTile、Rect renderer、Tilemap revision cache、schema4 persistence/migration。

关键规则：Cell 不复制像素；Edit Tile 修改共享 Surface；Make Unique 显式 clone；revision history 不完整时 renderer full fallback；runtime chunk 不进入项目语义。

最终功能 gate CI：`33143628422` success。

架构决策：`docs/DECISIONS/ADR-0005-tilemap-reference-model.md`。

### Batch11 — Effect Graph

**功能代码已完成，分支 `batch11-effects`。** Batch11 最新完整 code/test gate CI：`33147765622` — restore/build/test 全部 success。

完成内容：

- stable `EffectInstanceId`；
- Cel 拥有 ordered `EffectGraph`；
- `EffectDescriptor / EffectParameterDescriptor / EffectValue` typed parameter schema；
- static parameter + `AnimationTrack<EffectValue>` animated binding；
- Effect snapshot isolation；
- Effect palette/frame reference validation；
- 新 `MyLovePixel.Effects` evaluator 程序集；
- `IEffectEvaluatorBackend` CPU/GPU 共用契约；
- Registry + CPU reference backend；
- built-in Outline / Shadow / Palette Map；
- `EffectImage` 保存 size + origin，允许效果扩张边界；
- Indexed8 source 在同一 Snapshot 内解析 Palette + Color Cycle；
- unknown/unavailable Effect runtime pass-through，并报告 unavailable type；
- exact effect cache signature：source Surface revision、Palette revisions、Color Cycle、EffectGraph/instance revisions、backend revision；
- Frame structure signature 包含 Effect state / Palette dependencies；
- Effect Cel 的 source Surface dirty 当前 correctness-first full fallback，不做不完整的 effect dirty expansion；
- Add / Remove / Move / Enable Effect Commands；
- Set static parameter / Set-Clear animated keyframe Commands；
- `EffectBakePlanner`：Snapshot 上生成 immutable plan；
- `BakeEffectsCommand` apply 前重新核对 captured Surface/Palette/Effect/ColorCycle state，拒绝 stale preview；
- Bake 创建新的 RGBA Surface，调整 Cel.Position，清空 EffectGraph，不修改可能共享的 source Surface；
- Bake Undo 恢复旧 SurfaceId / Position / EffectGraph，并移除 baked Surface；
- enabled unknown Effect 存在时 Bake 明确拒绝，避免 opaque semantics 被静默丢掉；
- Frame Copy 复制 Cel EffectGraph，分配新的 effect parameter TrackId，并把 source-frame keyframe remap 到 copied FrameId；
- `.pixelproj` schema5：`CelDto.effects[]`、static parameters、tracks、keyframes、typed values；
- deterministic schema4→5 migration：legacy Cel 添加空 `effects`；
- unknown Effect type 不要求 evaluator 才能 load/save；
- Effect / parameter / value / color/point / track / keyframe nested extension JSON roundtrip preserve；
- Project semantic hash 包含 effect order、stable IDs/type/enabled、parameters、track IDs/names/keyframes，不包含 runtime Revision；
- 旧 schema2/schema3 migration chain 已继续验证到 schema5；
- schema5 unknown payload、semantic hash、animated parameter roundtrip、Frame Copy remap 均有回归测试。

Batch11 过程中有效 gate：

- Effect evaluator engine + tests：`33144585507` success
- 最新完整 code/test gate：`33147765622` success

架构决策：`docs/DECISIONS/ADR-0006-effect-graph-evaluation.md`。

## 4. 当前 Persistence 事实

Current schema = **5**。

Migration 链：

- schema1→2：Animation metadata + stable built-in Track IDs。
- schema2→3：Palette/Indexed + Color Cycle Track。
- schema3→4：deterministic document seed + empty Tileset/Tilemap collections for legacy documents。
- schema4→5：每个 legacy Cel 增加空 `effects[]`。

schema5 Effect 语义：

- Effect Graph 存在于 Cel DTO，而不是 Surface resource；Linked Cels 可共享像素但拥有不同 EffectGraph。
- Effect instance 保存 stable ID、namespaced `typeId`、enabled、static parameters、parameter tracks/keyframes。
- typed Effect values 当前支持 integer / number / boolean / color / point / paletteReference / text。
- 未安装 evaluator 不影响 load/save；未知 type 和 extension JSON 必须原样保留。
- runtime Revision 不进入 persistence semantic hash。

MLPX codec version 仍为 1：RGBA32 payload 4 byte/pixel；Indexed8 payload 1 byte/pixel。Palette 数据仍只在 document JSON 保存。

## 5. Batch11 核心不变量

1. Effect evaluator 永远只拿 immutable Snapshot，不拿 live mutable Document/Surface。
2. Effect preview/evaluation 不增加 Surface revision。
3. 未知 Effect 可以 load/save；不能执行不等于不能保存。
4. Bake unknown Effect 时失败，而不是跳过并清空它。
5. Bake 不 in-place 修改 linked source；结果是新的 RGBA Surface。
6. Effect 输出可改变 bounds，因此结果必须携带 origin。
7. Cache correctness 必须包含 Effect/Palette/ColorCycle/backend dependencies；不能只看 source Surface revision。
8. Effect-aware dirty propagation 尚未实现时必须 full fallback，不能漏重画 Outline/Shadow 扩张区域。
9. animated Effect parameter 复用 `AnimationTrack<T>`，不建立第二套 timeline/time system。
10. Plugin SDK 尚未到 Batch15；当前 Registry/evaluator contract 是内部扩展边界，不宣称稳定外部 ABI。

## 6. 已知事故 / 不要重复踩坑

- 不要用陈旧整文件覆盖 Core；Batch05 曾因此误删 Persistence API并触发回归。写文件前先 fetch 当前分支版本。
- schema migration 必须逐级、确定性；升级 schema 后旧测试不要写死旧 CurrentSchemaVersion。
- Frame Copy 除 built-in Animation tracks 外，还要考虑 Cel 内 Effect parameter tracks；复制 keyframe 时必须 remap source FrameId。
- 新增程序集名称可能与 Core 类型名冲突；必要时用完整类型名。
- Skia bitmap lifetime 由 `SkiaFrameCache` 明确拥有。
- Indexed Surface index 与 Palette 是强引用不变量。
- Palette reorder 在存在 Color Cycle 引用时当前明确失败；未来若支持必须原子 remap cycle semantics。
- Tilemap/Effect partial renderer 都不能只看 Dirty；revision 变了但 history/expansion 不完整必须 full fallback。
- 不要把 runtime chunk/cache/revision 写进项目资产语义。

## 7. 下一开发起点：Batch12 — Import / Export / Atlas / Headless Pipeline

目标：把当前 Document/Snapshot 真正接到游戏资产交付管线。

优先顺序：

1. `IImporter / IExporter / ExportRequest / ExportPreset` 契约；Exporter 只读 immutable snapshot。
2. PNG codec 与 RGBA/Indexed/Palette/ColorCycle/Effect 的明确导出语义。
3. frame/tag/slice metadata export。
4. Sprite sheet layout、trim/crop/scale/extrude。
5. Atlas packer Strategy/Registry；保证 deterministic ordering/packing。
6. JSON metadata schema；稳定 Frame/Tag/Slice IDs 不在导出过程中丢失。
7. Effect 导出必须使用与编辑器 Renderer/Bake 一致的 snapshot evaluator contract，不能重新实现一套效果算法。
8. CLI preset execution；UI 与 CLI 调同一 Export Pipeline。
9. 导出过程中 live document 继续编辑不能影响已捕获 snapshot。
10. 为未来 engine-specific exporters 保留 registry 边界，但不要提前实现 Batch15 Plugin SDK。

进入 Batch12 前仍先 fetch `main` + 最新 CI + 本文件。若 Batch11 已合入 main，则从 main 建 `batch12-export`。没有 CI 绿灯不要把 Batch12 标为完成。
