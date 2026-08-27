# MyLovePixel 分批实现计划

## 使用规则

这不是“版本路线图”。每一批只解决一组有明确依赖关系的工程问题，达到验收门槛后才进入下一批。

任何新增功能先回答七件事：**ID、生命周期、Undo、保存格式、Migration、缓存失效、Plugin 暴露方式**。回答不清楚就不进入实现。

---

## Batch 00 — Repository Foundation

**状态：已完成**

目标：先让工程本身可长期维护。

交付：

- .NET 10 / C# 14 基础配置
- solution / project 分层
- Core 不引用 UI/GPU
- xUnit 测试工程
- Headless CLI 工程
- Architecture / Implementation Plan
- 严格 nullable / warnings-as-errors / deterministic build

验收：

- `Core`、`Commands` 可以在没有 UI 项目时独立构建。
- CLI 只依赖 Core/Commands。
- 代码目录与依赖方向和架构文档一致。

---

## Batch 01 — Document Core / Stable References

**状态：已完成**

目标：把未来所有功能依赖的数据基础先做对。

交付：

- `DocumentId / LayerId / FrameId / CelId / ResourceId`
- `CanvasSpec`
- `Rgba32 / PixelFormat`
- `PixelSurface`
- `ResourceStore`
- `PixelLayer`
- `Frame`
- `Cel`
- Blank document factory
- Linked Cel 通过共享 `ResourceId` 表达
- `DocumentValidator`

当前范围刻意不加入 Palette、Tilemap、Effect、Audio。它们以后添加为新的资源和模型，不改变 Cel/Surface 的基本引用规则。

验收：

- Blank Document 必须通过 validator。
- 两个 Cel 共享一个 Surface 时，Surface 修改能被两个 Cel 同时观察到。
- Cel 不持有自己的像素数组。
- Snapshot 不受后续 live mutation 影响。

---

## Batch 02 — Command / Transaction / Undo-Redo

**状态：已完成**

目标：建立唯一 mutation 通路，在开始画笔前先把撤销语义固定。

交付：

- `ICommand`
- `IUndoToken`
- `CommandBus`
- `UndoStack`
- `CommandTransaction`
- `DocumentChange / DirtySurfaceRegion`
- `PixelPatchCommand`
- `UnlinkCelCommand`
- redo branch 规则

验收：

- `Apply -> Undo -> semantic state equal`。
- `Undo -> Redo` 恢复最终像素。
- 一个 transaction 中多个 command 只占一个 undo entry。
- Transaction cancel 能反向撤销已经执行的所有 command。
- Unlink 后 Cel 获得独立 Surface；Undo 恢复共享关系。

---

## Batch 03 — Persistence / Schema / Atomic Save

**状态：已完成**

目标：在算法越来越多之前先保证项目不会因升级或崩溃损坏。

交付：

- `.pixelproj` ZIP 容器格式
- `manifest.json` + `schemaVersion`
- Document DTO 与 runtime model 显式映射
- `MLPX` PixelSurface binary codec
- PixelSurface 内部 SHA-256 校验
- Save / Load semantic roundtrip
- `ProjectSemanticHash`
- `ProjectContentHash`
- Migration registry
- same-directory temp write / reopen validation / atomic replace
- Unknown JSON field preserve
- Unknown/Plugin opaque ZIP payload preserve
- DocumentValidator 接入 load/save
- 结构化 `PixelProjectException / PixelProjectErrorCode`
- ZIP entry 数量与解压尺寸限制

验收结果：

- save -> load -> semantic hash equal。
- Linked Cel 的共享 `SurfaceId` 经 roundtrip 不退化为复制像素。
- 全局 content hash 被篡改时加载失败。
- 即使重新计算全局 hash，Surface 内部 payload 被篡改仍因 MLPX SHA-256 失败。
- schema migration registry 逐级执行，不允许跳版本。
- 非法 Resource/Cel 引用加载失败并返回结构化错误。
- Unknown JSON fields 与 opaque plugin payload 经 load/save 后仍保留。
- atomic writer 在 commit 前失败时旧文件保持不变，临时文件被清理。
- GitHub Actions `dotnet build` + `dotnet test` 已通过。

暂不做 Autosave Journal；它仍属于 Batch 14。

---

## Batch 04 — Raster Core

**状态：已完成**

目标：所有绘画工具共享一套可测试的整数像素算法，而不是每个 Tool 自己画。

交付：

- 独立 `MyLovePixel.Raster` 程序集
- immutable `BrushMask`
- Brush connected path / spacing / endpoint policy
- Bresenham line
- integer rectangle / ellipse / polygon rasterizer
- span flood fill
- replace color
- `IColorToleranceStrategy`
- PixelPerfect `IStrokeFilter`
- symmetry `PointTransform`
- clipped / tiled coordinate policy
- `IInkStrategy`: Simple / Alpha / LockAlpha
- `RasterPatch` / exact DirtyRegion
- `RasterWorkBudget` + structured budget exception
- ASCII golden pixel fixtures

验收结果：

- Raster 只读 `PixelSurfaceSnapshot`，不持有 live surface 写权限。
- Preview/final 可调用同一 Raster 算法并得到相同 patch。
- Brush spacing 保证起点与最终端点，不因低采样率漏掉笔画尾端。
- Flood Fill 有 visited/write 双预算保护，不 silent truncate。
- Replace Color 与 Flood Fill 共用 tolerance 策略。
- Line / Rectangle / Ellipse / Polygon 有固定 golden fixture。
- RasterPatch 在执行 Command 前不修改 live surface；执行/Undo 后像素正确恢复。
- GitHub Actions `dotnet build` + `dotnet test` 已通过。

---

## Batch 05 — Selection / Transform

**状态：已完成**

目标：Selection 是独立 Mask，Transform 不依赖具体 Tool/UI。

交付：

- 独立 `MyLovePixel.Selection` 程序集
- 真正 bit-packed 的 `Bit1 SelectionMask`
- `Alpha8 SelectionMask`
- Rectangle / Ellipse / Lasso selection
- Add / Subtract / Intersect / Invert
- Select by color
- Selection translate / flip / 90° rotate / nearest scale
- immutable `FloatingContent`
- FloatingContent move / flip / rotate90 / nearest scale
- `IArbitraryRotationStrategy`
- `FloatingContentComposer`：Snapshot -> preview/final RasterPatch
- `MultiTargetPixelPatchCommand`
- Multi-target apply 前全量验证与 before capture
- `PixelSurface.SetPixels` 全量坐标与 revision 预验证

验收结果：

- Move Selection 只改变 Mask，不改变 Surface revision/像素。
- Move Content 在确认前只生成 patch，不修改 live surface/Undo 历史。
- 确认 Transform 后通过 CommandBus 修改，Undo 恢复源像素。
- FloatingContent Flip / Rotate90 / nearest scale 使用确定性像素映射。
- 多 Surface Transform 只占一个 Undo entry。
- 后一个 target 非法时，前一个 target 不发生半提交。
- Selection 是工作区瞬态状态，不写入 `.pixelproj`；只有最终像素结果进入 Document。
- GitHub Actions `dotnet build` + `dotnet test` 已通过。

---

## Batch 06 — RenderGraph / Canvas Cache

目标：建立可替换 Renderer，并把 Dirty/Revision 正确性一次做对。

交付：

- `DocumentSnapshot`
- render request / render node contracts
- CPU reference compositor
- Skia backend
- nearest-neighbor canvas presentation
- ViewTransform
- layer/frame composite cache
- dirty texture upload
- grid/guides/selection/tool overlay passes
- cache diagnostics

验收：

- 16x16 patch 不触发 full surface rebuild。
- Renderer 无权修改 live document。
- 清空所有 GPU/cache 后输出仍一致。

---

## Batch 07 — Input / ToolHost / Brush Session

目标：鼠标、笔、未来触控统一成输入事件；Tool 只做状态机。

交付：

- PointerEvent
- ToolContext
- ITool
- ToolDescriptor / ToolOptions schema
- StrokeSession
- Pencil / Eraser / Line / Shape / Fill tools
- cancel / rollback preview
- keyboard modifier routing

验收：

- 同一 Tool 可由模拟 PointerEvent 单元测试驱动。
- 连续笔画只生成一个 undo entry。
- 高频 pointer move 不生成全文档 snapshot。

---

## Batch 08 — Timeline / Animation / Metadata

目标：完成游戏 Sprite 的动画语义，而不是只做帧列表 UI。

交付：

- playback clock
- frame duration
- linked/unlinked cel operations
- frame copy/move/remove commands
- Onion Skin render pass
- Tag/Clip
- Pivot / Slice / 9-Slice
- Hitbox / Hurtbox / Socket / Animation Event tracks
- 通用 `AnimationTrack<T>` 与 easing 接口

验收：

- Frame ID 在插帧/删除后保持稳定。
- Onion Skin 只读 cache，不进入文档像素。
- Metadata 可逐帧变化并被 exporter 读取。

---

## Batch 09 — Palette / Indexed / Dithering

目标：把颜色当数据引擎，不把高级调色逻辑塞进 Color Picker。

交付：

- Palette resource
- Indexed8 Surface
- Transparent Index
- Palette remap
- quantization strategy
- ColorRamp
- Shading Ink
- Dither strategy / custom matrix
- Color Cycling tracks

验收：

- Palette reorder 后 index remap 不改变视觉结果。
- Indexed pixel 永远不越界。
- RGBA/Indexed 的编辑和 preview 合成规则明确分离。

---

## Batch 10 — Tileset / Tilemap / AutoTile

目标：Tilemap 保持引用模型，不退化为大位图。

交付：

- TileId / Tileset
- Rect grid topology
- Tilemap chunks/cells
- flip/rotate/variant flags
- edit tile pixels / unique tile
- tile resource GC
- `IGridTopology` 扩展 Iso/Hex
- `IAutoTileRule`
- 4/8 neighbor bitmask
- deterministic weighted variant

验收：

- 修改 Tile Surface 后所有引用 Cell 同步更新。
- 修改一个 Cell 不复制 Tile 像素。
- AutoTile 随机结果由 document seed + coordinate 稳定复现。

---

## Batch 11 — Effect Graph

目标：Outline、Shadow、Palette Map 等以非破坏参数存在。

交付：

- EffectDescriptor / EffectInstance
- parameter schema
- CPU/GPU evaluator abstraction
- effect cache key
- animated parameter binding
- Bake Effect command

验收：

- 调参不改源 PixelSurface。
- Undo Bake 恢复原 surface。
- 未安装未知 Effect 时项目仍能保存并保留 payload。

---

## Batch 12 — Import / Export / Atlas / Headless Pipeline

目标：把“画完”接到真正游戏资产管线。

交付：

- IImporter / IExporter
- ExportRequest
- ExportPreset
- PNG codec
- sprite sheet
- trim / crop / scale / extrude
- atlas packer strategy
- JSON metadata
- frame/tag/slice/metadata export
- CLI preset execution

验收：

- Exporter 只吃 immutable snapshot。
- 导出期间继续编辑 live document 不产生数据竞争。
- UI 和 CLI 调同一 Export Pipeline。

---

## Batch 13 — Avalonia Desktop Shell

目标：这时才开始把成熟 Core 接成真正编辑器，而不是让 UI 反向定义架构。

交付：

- Action Registry
- Shortcut Map
- Docking layout
- Canvas View
- Layer panel
- Timeline virtualized view
- Tool option inspector
- Palette panel
- project/open/save/export commands
- theme tokens

验收：

- 菜单/Toolbar/快捷键只绑定 ActionId。
- Canvas Widget 不直接拿可写 PixelSurface。
- Timeline 大量 frame 不创建海量控件。

---

## Batch 14 — Autosave / Recovery / Performance Hardening

交付：

- autosave snapshot/journal
- backup rotation
- recovery UI
- LRU thumbnail cache
- undo memory budget
- cache hit/miss diagnostics
- dirty region visualization
- stress fixtures
- crash injection tests

验收：

- 保存任意阶段中断，旧文件保持有效。
- 最近 autosave 可恢复。
- 千帧/大 Tilemap 测试不因同步重算冻结 UI。

---

## Batch 15 — Plugin / Script SDK

目标：以后定制功能优先通过扩展点增长，不继续修改 Core。

交付：

- PluginRegistry
- versioned Public SDK
- register Tool / Command / Effect / Exporter / Importer / Panel
- Palette/Dither/AutoTile algorithm registration
- namespaced plugin data
- script host（具体 Lua/JS/WASM 在这里再定）

验收：

- 外部插件能增加 Tool + Effect + Exporter，不改 Core 源码。
- 插件不能获得裸 mutable document / GPU lifetime 对象。

---

## Batch 16 — Optional Advanced Modules

只有实际工作流需要时才进入，不作为基础编辑器前置依赖。

候选：

- Audio Layer / waveform / timeline sync
- Bone / Mesh / IK
- 3D Guide / 3D -> Pixel render
- macro / batch composition
- engine-specific exporters
- procedural generators

约束：这些模块必须通过 RenderNode / AnimationTrack / Exporter / Plugin SDK 接入，不污染普通 Cel 模型。

---

# 每批统一 Definition of Done

每批功能只有同时满足以下条件才算完成：

1. 公共模型和接口有明确边界。
2. mutation 有 Command/Undo 语义。
3. 新数据有明确 ID 和生命周期。
4. Persistence 行为已定义；若 Schema 尚未进入对应批次，至少写下未来字段位置。
5. Dirty / Revision / Cache 影响已定义。
6. 单元测试覆盖主路径和边界条件。
7. 不需要 UI 才能测试核心逻辑。
8. 失败返回结构化错误，不靠 silent fallback 掩盖数据错误。
9. 不新增 Core -> UI/GPU 的反向依赖。
10. 若属于变化快的策略，优先设计成注册式接口而不是 enum switch 写死。

# 近期执行顺序

Batch 00–05 已完成并通过 GitHub Actions。下一次代码工作直接进入 **Batch 06 RenderGraph / Canvas Cache**：先做 CPU reference compositor、RenderGraph contract、Revision/Dirty cache key，再接 Skia backend；Renderer 全程只消费 Snapshot，不获得 live Document mutation 权限。
