# MyLovePixel — Handoff

> 这是继续开发时的第一入口。先确认 `main` 当前 HEAD 和 GitHub Actions，再读本文件、`docs/IMPLEMENTATION_PLAN.md`、`docs/ARCHITECTURE.md` 与 `docs/DECISIONS/`。不要仅依赖本文中的 SHA 判断仓库是否有更新。

## 1. 项目目标

MyLovePixel 是私人游戏开发使用的可扩展 Pixel Art / Sprite 资产编辑核心。当前优先建设稳定的数据模型、Raster、Selection、Renderer、Tools、Animation、Persistence、后续 Palette/Tilemap/Exporter/Plugin；Avalonia UI 到 Batch 13 才正式进入。

技术基线：

- .NET 10 / C# 14
- xUnit v3
- SkiaSharp 4.151.1
- Avalonia 12.1.1（后续 Desktop Shell）

## 2. 永久架构约束

1. UI 不直接修改 `PixelDocument`；可撤销 mutation 统一经过 Commands / `CommandBus`。
2. `Cel` 只持有稳定 `ResourceId`，不拥有 RGBA 数组。
3. Linked Cel = 多个 Cel 指向同一 `PixelSurface` / `ResourceId`。
4. `PixelSurface` 是像素真值；GPU texture、thumbnail、frame composite 都是可重建缓存。
5. Core 不引用 Avalonia、SkiaSharp、窗口或 UI。
6. Raster 只读 `PixelSurfaceSnapshot`，输出 `RasterPatch` / `PixelWrite[]`。
7. Renderer 只读 `DocumentSnapshot`，不能修改 live Document。
8. Selection 是 workspace transient state，不写 `.pixelproj`。
9. Tool/Transform preview 不修改 live Surface；确认时才生成 Command。
10. Persistence 使用独立 DTO + `schemaVersion` + Migration，禁止直接序列化 runtime object graph。
11. 未知 JSON 字段和未知/plugin ZIP payload 必须保留。
12. Revision 决定缓存正确性；Dirty Region 只决定是否可以局部更新。
13. 高变化算法优先 Strategy/Registry，而不是持续扩大 enum switch。
14. 稳定 ID 是跨插帧、删除、保存、导出和插件引用的基础。

## 3. 当前模块

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
  MyLovePixel.Cli/

tests/
  MyLovePixel.Core.Tests/
  MyLovePixel.Persistence.Tests/
  MyLovePixel.Raster.Tests/
  MyLovePixel.Selection.Tests/
  MyLovePixel.Render.Tests/
  MyLovePixel.Tools.Tests/
  MyLovePixel.Animation.Tests/
```

`MyLovePixel.slnx` 已包含以上全部项目。

## 4. 已完成批次

### Batch 00 — Repository Foundation

完成工程分层、CI、严格 nullable/warnings-as-errors/deterministic build、Headless CLI 与基础 ADR。

### Batch 01 — Document Core / Stable References

完成 Document/Layer/Frame/Cel/Resource 稳定 ID、PixelSurface、ResourceStore、Linked Cel、DocumentValidator、Snapshot 基础。

### Batch 02 — Command / Transaction / Undo-Redo

完成 CommandBus、Undo/Redo、Transaction、PixelPatch、Unlink Cel、DirtySurfaceRegion。

### Batch 03 — Persistence / Schema / Atomic Save

完成 `.pixelproj` ZIP、显式 DTO、MLPX Surface codec、SHA-256、semantic/content hash、Migration Registry、atomic save、unknown JSON/plugin payload preserve、结构化错误与 load limits。

### Batch 04 — Raster Core

完成 Brush/Stroke、Line/Rectangle/Ellipse/Polygon、Flood Fill、Replace Color、Tolerance、PixelPerfect、Symmetry、Coordinate Policy、Ink Strategy、RasterPatch、WorkBudget。

### Batch 05 — Selection / Transform

完成 bit-packed/Alpha8 Selection、选区布尔运算、Select-by-color、Move/Flip/Rotate/Scale、FloatingContent、MultiTargetPixelPatchCommand 与无 live mutation preview。

### Batch 06 — RenderGraph / Canvas Cache

完成：

- 增强 `DocumentSnapshot`
- `MyLovePixel.Render`
- Render contracts / RenderGraph / render node revision
- CPU reference compositor
- exact structure signature + participating resource revision state
- dirty-region partial frame recompose
- linked Cel dirty mapping
- `TextureUploadPlanner`
- `ViewTransform`
- Grid / Guide / Selection / Tool Preview overlay passes
- Skia frame bitmap cache / dirty upload
- nearest-neighbor presentation
- cache diagnostics

关键规则：缺失完整 revision→revision Dirty history时必须 full fallback；不能用 hash 作为唯一 correctness 条件；overlay 不进入 frame composite cache。

Batch06 最终修复：Skia native bitmap lifetime 由 `SkiaFrameCache` 自己拥有，不依赖不可访问的 `SKNativeObject.IsDisposed`。

### Batch 07 — Input / ToolHost / Brush Session

完成：

- Mouse/Pen/Touch 统一 `PointerEvent`
- `ToolContext / ITool / ToolDescriptor / ToolOptions`
- `ToolHost`
- `StrokeSession`
- Pencil / Eraser / Line / Shape / Fill
- keyboard modifier routing
- preview/cancel
- stale Surface revision conflict protection

交互语义：pointer-down 只抓一次目标 Surface Snapshot；move 只更新 preview；pointer-up 才提交一个 Command。因此连续 stroke 只有一个 Undo entry，Cancel 不需要反向修改 live Surface。

Batch07 完整 CI 通过后已进入 `main`；进入 Batch08 前的 main 基线为 `c3bfbb0153f40cd804f9d36a6e343d679f2e3b80`。

### Batch 08 — Timeline / Animation / Metadata

代码已完成并通过完整 CI。文档刷新前的最终代码验收 commit：

`a6395e2234f6a1468de532b3c436845065eabbf3`

对应 GitHub Actions run：

`33135789456` — `restore + build + test` 全部 `success`。

完成：

- 稳定 `AnimationClipId / AnimationTagId / SliceId / AnimationTrackId`
- `AnimationClip / AnimationTag`
- `SpriteSlice / NineSliceInsets`
- Pivot / Hitbox / Hurtbox / Socket / Event tracks
- 通用 `AnimationTrack<T>`
- `IAnimationEasing` + Linear / Step
- `DocumentSnapshot.Animation`
- Frame duration Command
- Linked/Independent Frame Copy
- Frame Move / Remove
- Link / Unlink Cel
- Clip/Tag/Slice/Track Commands + Undo/Redo
- `AnimationPlaybackClock`
  - integer ticks
  - Once / Loop / PingPong
- cache-backed `OnionSkinRenderer`
- `.pixelproj` schemaVersion **2**
- runtime Animation ↔ explicit DTO mapping
- deterministic schema 1→2 migration
- animation metadata included in `ProjectSemanticHash`
- unknown JSON preservation inside animation DTO hierarchy

重要语义：

- Copy Frame 会复制 Pivot/Hitbox/Hurtbox/Socket/Event 的逐帧值。
- Linked Copy 共享 PixelSurface；Independent Copy clone Surface。
- Move Frame 会先模拟新 FrameOrder；若会让任一 Clip/Tag 的 start 排在 end 后面，操作直接失败，不偷偷交换玩家设置的端点。
- Remove Frame 会收缩引用该端点的多帧 Clip/Tag；单帧范围随帧删除；Undo 精确恢复。
- Onion Skin 通过 `FrameRenderer` cache 读取相邻帧，绝不写源 Surface。
- PlaybackClock 是运行态，不进入项目 Persistence。
- schema 1→2 migration 为五条内置 Track 生成 deterministic stable ID。

Batch08 新增验收测试覆盖：

- Playback Loop/Once/PingPong exact tick
- Frame Copy 五条 Track + Undo
- Frame Move range inversion rejection
- Remove Frame Clip/Tag endpoint shrink + Undo
- Onion Skin repeated render cache hit + live Surface revision unchanged
- schema2 full animation semantic roundtrip
- schema1→2 deterministic migration
- unknown animation/clip/track/keyframe JSON roundtrip preservation

Persistence 专项测试 commit 也已有独立全绿 run `33135780795`。

## 5. 已知事故 / 不要重复踩坑

### 5.1 不要用陈旧整文件覆盖 Core

Batch05 曾因用旧版本整体重写 `PixelSurface.cs`，误删 Persistence 所需 `FromRgbaBytes`，导致 CI 回归。以后修改核心文件必须先 fetch 当前 branch 文件并做最小修改。

### 5.2 Skia lifetime 不依赖内部状态

SkiaSharp 4.151.1 的 `SKNativeObject.IsDisposed` 不可公开访问。`SkiaFrameCache` 明确拥有 `SKBitmap` 生命周期，借出的 bitmap 不能由调用者 dispose。

### 5.3 schema migration 必须逐级、确定性

Current schema = 2。旧 schema 1 的项目由默认 registry 执行 1→2 migration；未来 schema 升级必须继续一版一版注册，禁止跳版本。

### 5.4 FrameOrder 与 Clip/Tag 范围

Clip/Tag 端点存稳定 FrameId，但范围语义由当前 FrameOrder 决定。任何重排必须保证 start index <= end index；不要为了通过验证自动交换端点。

## 6. 下一开发起点：Batch 09 — Palette / Indexed / Dithering

目标：把颜色变成独立数据引擎，而不是 UI Color Picker 的附属逻辑。

计划交付：

- Palette resource
- Indexed8 Surface
- Transparent Index
- Palette remap
- quantization strategy
- ColorRamp
- Shading Ink
- Dither strategy / custom matrix
- Color Cycling tracks

开始前必须先解决：

1. `PixelFormat` 从当前 RGBA32-only 扩展到 Indexed8 时，`PixelSurface` 如何保持格式不变量。
2. Palette 是 `ResourceStore` 的稳定资源，Cel 仍只引用 Surface；不要让 Cel 直接拥有 Palette。
3. Indexed8 Surface 必须稳定引用一个 PaletteId，删除/替换 Palette 的生命周期要有规则。
4. Palette reorder 必须通过 index remap 保持视觉结果不变。
5. Renderer/CPU compositor 如何将 Indexed8 + Palette 解析成 RGBA，而不把 palette lookup 写进 Persistence/UI。
6. `.pixelproj` schema 3 的 DTO/migration 位置。
7. Quantization/Dither/Shading 使用 Strategy，不将算法写死进 Core。
8. Color Cycling 属于动画数据，需要复用现有 AnimationTrack/稳定 ID 思路，而不是独立时间系统。

## 7. 继续开发时的操作顺序

1. Fetch `main` 当前 HEAD，确认 Batch08 已 fast-forward 合入。
2. 查最新 `main` CI，必须 success。
3. 读本文件与 Batch09 计划。
4. 为 Batch09 建独立分支。
5. 先写 Palette/Indexed 数据不变量和 Persistence 设计，再写 quantization/dither。
6. 每个阶段以真实 GitHub Actions build/test 为准；没有 CI 不能标完成。

不要重写 Git 历史，不 force update `main`。只在分支验证全绿后 fast-forward 合并。
