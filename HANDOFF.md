# MyLovePixel — Handoff

> 用途：给下一个对话/执行者的唯一交接入口。继续开发前先读本文件，再读 `docs/IMPLEMENTATION_PLAN.md`、`docs/ARCHITECTURE.md` 和 `docs/DECISIONS/`。

## 0. 仓库与当前基线

- Repository: `LaooooB/MyLovePixel`
- Default branch: `main`
- 本文件写入前 `main` HEAD：`86b75de21961d93156f352a79834d6b0957e8746`
- 该 HEAD 是文档提交：`docs: mark raster and selection batches complete`
- 最近一次经过完整 `restore + build + test` 验证的代码基线：`b45f485cba0cfadd4c132f390b912a8e2c9010f8`
- 对应 GitHub Actions run：`33102433586`，结论 `success`
- Batch 04 完整 CI：run `33101484115`，结论 `success`
- Batch 05 第一次 CI：run `33102313525`，`build` 失败，原因见“已知事故”；随后已修复并由 `33102433586` 全绿覆盖。

如果继续开发，第一件事应该重新读取 `main` HEAD 并确认 CI；不要只依赖本文件中的 SHA，因为后续可能已有新提交。

## 1. 项目目标与范围

MyLovePixel 是私人游戏开发使用的 Pixel Art 创作软件。目标是长期可扩展的像素资产编辑核心，不是快速堆一个 UI Demo，也不是复制某个现有编辑器。

当前只考虑软件/代码层：数据模型、算法、渲染、输入、动画、Tilemap、颜色、导出、插件。明确不进入工程边界的内容：账号、登录、云同步、多人协作、支付、订阅、遥测、在线素材商城等。

技术栈当前固定为：

- Runtime: .NET 10 / C# 14
- Tests: xUnit v3
- Desktop UI（后续 Batch 13）: Avalonia 12
- 2D/GPU backend（Batch 06 起）: SkiaSharp
- `Directory.Packages.props` 当前记录 Avalonia `12.1.1`、SkiaSharp `4.151.1`

## 2. 永久架构约束

以下约束比某个具体功能实现更重要，除非先写 ADR，否则不要破坏：

1. UI 不直接修改 `PixelDocument`；所有可撤销 mutation 统一经过 Commands / `CommandBus`。
2. `Cel` 不拥有 RGBA 数组，只引用稳定 `ResourceId`。
3. Linked Cel = 多个 Cel 指向同一个 `PixelSurface` / `ResourceId`；解除链接时 clone Surface + 替换引用。
4. `PixelSurface` 是像素真值；GPU texture、thumbnail、composite 都是可重建缓存。
5. Core 不依赖 Avalonia、SkiaSharp、窗口、Widget、文件对话框。
6. Raster 不修改 live `PixelSurface`，只读取 `PixelSurfaceSnapshot` 并输出 `RasterPatch/PixelWrite[]`。
7. Renderer 只读 `DocumentSnapshot`，永远不能改 live Document。
8. Selection 当前是工作区瞬态状态，不属于 `.pixelproj`；保存项目时不保存“当前框选状态”。
9. Transform preview 不产生 live mutation；确认后才生成 patch/command。
10. Persistence 使用独立 DTO + `schemaVersion` + Migration，禁止直接序列化 runtime object graph。
11. 未知 JSON 字段和未知/plugin ZIP payload 必须原样保留。
12. Dirty Region 和 Revision 是两件事：Revision 决定正确性/缓存版本，Dirty Region 决定可否局部更新。
13. 新的高变化算法优先设计成 Strategy/Registry 接口，不要在核心里无限堆 enum switch。
14. UI 要到 Batch 13 才正式进入；不要为了“先看到窗口”提前让 UI 反向定义 Core。

详细规则在 `docs/ARCHITECTURE.md`。

## 3. 当前 solution / 模块

当前仓库已经包含：

```text
src/
  MyLovePixel.Core/
  MyLovePixel.Commands/
  MyLovePixel.Persistence/
  MyLovePixel.Raster/
  MyLovePixel.Selection/
  MyLovePixel.Cli/

tests/
  MyLovePixel.Core.Tests/
  MyLovePixel.Persistence.Tests/
  MyLovePixel.Raster.Tests/
  MyLovePixel.Selection.Tests/

docs/
  ARCHITECTURE.md
  IMPLEMENTATION_PLAN.md
  DECISIONS/
    ADR-0001-tech-stack.md
    ADR-0002-project-format.md
    ADR-0003-raster-contract.md
```

`MyLovePixel.slnx` 已包含 Raster 和 Selection 及对应测试工程。

## 4. 已完成批次

### Batch 00 — Repository Foundation — 已完成

完成：

- .NET 10 / C# 14 工程配置
- solution/project 分层
- nullable / warnings-as-errors / deterministic build
- GitHub Actions CI
- Headless CLI
- Architecture / Implementation Plan / ADR 基础

核心原则：Core 在无 UI/GPU 时可以独立构建和测试。

### Batch 01 — Document Core / Stable References — 已完成

完成：

- `DocumentId / LayerId / FrameId / CelId / ResourceId`
- `CanvasSpec`
- `Rgba32 / PixelFormat`
- `PixelSurface / PixelSurfaceSnapshot`
- `ResourceStore`
- `PixelLayer`
- `Frame`
- `Cel`
- Blank document factory
- Linked Cel 共享 `ResourceId`
- `DocumentValidator`
- `DocumentSnapshot` 基础版

关键语义：Snapshot 必须与后续 live mutation 隔离；Cel 只引用 Surface。

### Batch 02 — Command / Transaction / Undo-Redo — 已完成

完成：

- `ICommand / IUndoToken`
- `CommandApplication`
- `CommandBus`
- `UndoStack / HistoryEntry`
- `CommandTransaction`
- `DocumentChange / DirtySurfaceRegion`
- `PixelPatchCommand`
- `UnlinkCelCommand`
- redo branch 规则

已验证：

- Apply → Undo 恢复语义状态
- Undo → Redo 恢复最终状态
- Transaction 合并成一个 Undo entry
- Cancel transaction 回滚已经执行的命令
- Unlink/Undo 恢复 Linked Cel 共享关系

### Batch 03 — Persistence / Schema / Atomic Save — 已完成

独立程序集：`MyLovePixel.Persistence`。

完成：

- `.pixelproj` ZIP 容器
- `manifest.json`
- `schemaVersion`
- Runtime Model ↔ Persistence DTO 显式映射
- `MLPX` PixelSurface 二进制格式
- Surface payload SHA-256
- Project logical content hash
- Project semantic hash
- Save / Load
- Migration Registry
- same-directory temp write → reopen validation → atomic replace
- Unknown JSON field preserve
- Unknown/plugin opaque ZIP entry preserve
- Load/save 时 `DocumentValidator`
- 结构化 `PixelProjectException / PixelProjectErrorCode`
- ZIP entry 数量和解压尺寸限制
- Linked Cel roundtrip 保持共享 `SurfaceId`

重要实现边界：Persistence 通过受控 `InternalsVisibleTo` 重建 Core，不把 `AddLayer/AddFrame/AddCel` 等低层 mutation API 公开给普通调用者。

### Batch 04 — Raster Core — 已完成

独立程序集：`MyLovePixel.Raster`。

完成：

- `BrushMask`
- Brush stamp / stroke spacing
- Bresenham line
- Rectangle
- Integer ellipse
- Integer even-odd polygon
- Flood Fill
- Replace Color
- `IColorToleranceStrategy`
  - Exact
  - MaxChannel
- `IStrokeFilter`
  - Identity
  - PixelPerfect
- Coordinate policy
  - Clip
  - Tiled/Wrap
- Symmetry / point transforms
- `IInkStrategy`
  - Simple
  - Alpha Composite
  - Lock Alpha
- `RasterPatch / RasterPatchBuilder`
- `RasterWorkBudget`
- Flood/replace 超预算时明确抛异常，不静默截断
- Golden/边界测试

架构：

```text
Geometry
  -> Stroke / Brush
  -> Coordinate / Symmetry
  -> Ink
  -> RasterPatch
  -> PixelWrite[]
  -> CommandBus / PixelPatchCommand
  -> PixelSurface
```

Raster 只能计算“改哪些像素/最终颜色”，不能拥有 Undo/UI/live mutation。

真实 CI：run `33101484115` = success。

### Batch 05 — Selection / Transform — 已完成

独立程序集：`MyLovePixel.Selection`。

完成：

- `SelectionMask`
- 真正 bit-packed 的 1-bit mask (`ulong[]`)
- Alpha8 coverage mask
- 对外统一 coverage 0–255
- Rect / Ellipse / Lasso Selection
- Add / Subtract / Intersect / Invert
- Select by color
- Selection Translate
- Flip
- Rotate 90°
- Nearest-neighbor Scale
- `FloatingContent`
- “移动 Selection”与“移动 Pixel Content”分离
- Floating content preview 不修改 live surface
- Transform 最终合成成确定性 `RasterPatch`
- `MultiTargetPixelPatchCommand`
- 多 Surface 修改一个 Undo entry
- 多目标失败前先验证全部 target，禁止半提交
- `PixelSurface.SetPixels` 强化为全部坐标预验证后再写
- Revision overflow 检查在写像素前完成

Selection 当前明确为 transient workspace state，不写进 Persistence schema。

最终真实 CI：run `33102433586` = success。

## 5. 已知事故 / 不要重复踩坑

### 5.1 Batch 03 初期编译问题

曾遇到：

- `EndsWith(char, StringComparison)` 错误重载
- loop 内 `stackalloc` 被 CA2014 作为 error
- 测试里的空异常类语法问题

均已修复。不要回滚到这些中间提交。

### 5.2 Batch 05 引入的 Persistence 回归

为了强化 `PixelSurface.SetPixels` 原子性时，曾基于较早文件内容重写 `PixelSurface.cs`，误删了 Batch 03 的：

```csharp
internal static PixelSurface FromRgbaBytes(...)
```

结果 run `33102313525` 在 Persistence build 阶段失败：

```text
PixelSurfaceBinaryCodec.cs: PixelSurface does not contain FromRgbaBytes
```

已在 commit `b45f485cba0cfadd4c132f390b912a8e2c9010f8` 恢复 `FromRgbaBytes`，同时保留 `SetPixels` 的全量预验证和 revision 预检查。run `33102433586` 完整 build/test 全绿。

以后修改核心文件时，不要用陈旧版本整体覆盖；先 fetch 当前 `main` 文件再做最小修改。

## 6. Batch 06 — RenderGraph / Canvas Cache：当前真实状态

**状态：尚未实现完成，尚未提交。**

上一对话已经确定设计并创建过若干 Git blob，但这些 blob 没有挂入 `main` tree、没有路径映射、没有经过 build/test。因此：

> 下一个对话绝对不要把 Batch 06 当作已实现，也不要依赖这些 orphan blob 直接继续。最好按下面的设计重新生成并以 CI 为准。

曾创建但未挂接的 blob SHA，仅供审计：

```text
bce8afb151264edd54f4f4ada99f66d000be850a
3c23afe72fe275f47d514792730f8f7f362526bd
db0f4d21d5733a96d5e8ba7f655f38a824376d59
46e366c3804da01c05fc8dc3723763e78e4b54b2
24076cc94c3963d2bee4927daf279b682ab86f6a
e2c35b522e175bf0a5ca12136dd4dbd65195dfa4
```

上一对话已确定但尚未提交的 Render 设计：

- 增强 `DocumentSnapshot`
  - `LayerSnapshot`
  - `FrameSnapshot`
  - Layer visibility/opacity/kind
  - 真正 readonly collection
- 新建 `MyLovePixel.Render`
- `CpuRenderSurface`
- `CpuRenderTarget`
- `RenderMath / SourceOver`
- `IRenderNode`
- `RenderGraph`
- `FrameRenderRequest`
- `FrameRenderResult`
- `SurfaceInvalidation`
- `FrameCompositeRenderNode`
- `FrameStructureSignature`
- `ResourceRevisionState`
- `RenderCacheDiagnostics`

Cache 规则已经确定：

```text
结构签名变化：
Layer / Cel / Position / Opacity / Surface Size
        -> Full Recompose

结构不变，仅参与当前 Frame 的 Surface Revision 变化
且宿主提供对应 DirtyRegion
        -> Partial Recompose

结构与 Revision 均不变
        -> Cache Hit
```

注意：缓存正确性不要只依赖一个可能碰撞的哈希。可以用结构化 signature/equality + revision state；hash 只用于加速查找，不应成为唯一 correctness 条件。

Overlay（Grid / Guide / Selection / Tool preview）应作为独立 screen/view-space pass，不烘焙进 frame composite cache。Selection 或 Tool preview 改变不应让底层 Frame cache 失效。

## 7. 下一对话的准确执行起点

### 第一阶段：先确认基线

1. Fetch `main` 当前 HEAD。
2. 读 `HANDOFF.md`。
3. 读 `docs/IMPLEMENTATION_PLAN.md`。
4. 读 `docs/ARCHITECTURE.md`。
5. 读 `docs/DECISIONS/ADR-0001..0003`。
6. 检查 `MyLovePixel.slnx`。
7. 确认当前 GitHub Actions 状态。
8. 不要把 orphan Render blobs 当 source of truth。

### 第二阶段：完成 Batch 06

建议顺序：

```text
1. 增强 DocumentSnapshot
2. 创建 MyLovePixel.Render 工程
3. CPU reference compositor
4. Render contracts / RenderGraph
5. Frame composite cache
6. Structure signature + revision tracking
7. DirtyRegion -> canvas region 映射
8. Partial recompose
9. TextureUploadPlanner / dirty upload contract
10. ViewTransform
11. Grid / Guide / Selection / Tool Overlay passes
12. Skia backend/cache
13. nearest-neighbor presentation
14. Render tests
15. CI
16. 只有 CI 全绿后才标记 Batch 06 完成
```

### Batch 06 必须验收

- Renderer 没有 live document mutation 权限。
- CPU reference renderer 可以无 UI 测试。
- 16×16 Surface patch 不触发 full-surface/full-frame rebuild（结构没有变化时）。
- 清空 Render/GPU cache 后，单靠 `DocumentSnapshot` 能重建相同输出。
- Overlay 变化不污染 frame composite cache。
- nearest-neighbor 路径明确，不发生平滑插值。
- Skia 是 backend，不是文档真值。

## 8. 后续计划（不要跳批次）

`docs/IMPLEMENTATION_PLAN.md` 是权威计划。当前顺序：

```text
Batch 06 RenderGraph / Canvas Cache
Batch 07 Input / ToolHost / Brush Session
Batch 08 Timeline / Animation / Metadata
Batch 09 Palette / Indexed / Dithering
Batch 10 Tileset / Tilemap / AutoTile
Batch 11 Effect Graph
Batch 12 Import / Export / Atlas / Headless Pipeline
Batch 13 Avalonia Desktop Shell
Batch 14 Autosave / Recovery / Performance Hardening
Batch 15 Plugin / Script SDK
Batch 16 Optional Advanced Modules
```

不要提前把 Avalonia UI 作为主要实现方向。先把 Render、Input、Timeline、Palette、Tilemap、Export 等无 UI 核心做稳。

## 9. Definition of Done

每批只有同时满足以下条件才算完成：

- 模型/接口边界明确
- mutation 有 Command/Undo 语义
- 新数据 ID/生命周期明确
- Persistence 行为明确
- Dirty/Revision/Cache 影响明确
- 主路径 + 边界测试
- 不启动 UI 也能测核心逻辑
- 失败是结构化错误，不 silent fallback
- 不新增 Core -> UI/GPU 反向依赖
- 变化快的策略优先注册式接口
- GitHub Actions `restore + build + test` 实际全绿

## 10. 常用构建命令

```bash
dotnet restore MyLovePixel.slnx
dotnet build MyLovePixel.slnx --no-restore
dotnet test MyLovePixel.slnx --no-build
dotnet run --project src/MyLovePixel.Cli
```

CI 使用 `.github/workflows/ci.yml`。

## 11. 交接给下一个对话时可以直接说

```text
继续开发 GitHub 仓库 LaooooB/MyLovePixel。
先读取仓库根目录 HANDOFF.md、docs/IMPLEMENTATION_PLAN.md 和 docs/ARCHITECTURE.md。
Batch 00–05 已完成，Batch 06 RenderGraph 尚未提交完成。
必须先确认 main/CI，再从 HANDOFF 里的 Batch 06 执行起点继续；未经过 CI 不要标记完成。
```

---

最后原则：不要为了快而绕开现有边界。这个项目目前最值钱的不是已有多少按钮，而是 Document / Command / Persistence / Raster / Selection 之间已经建立的低耦合契约。后续功能应继续沿这些契约增长，而不是把行为重新绑回 UI 或可写 byte[]。
