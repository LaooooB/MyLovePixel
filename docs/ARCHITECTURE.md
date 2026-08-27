# MyLovePixel 架构约束

这份文档是代码审查时的硬约束。功能需求可以增加，但不能通过破坏这些边界来换速度。

## 1. 依赖方向

```text
App / CLI
   ↓
Tools ─────→ Commands ─────→ Core
   ↓                         ↑
Render ─── read Snapshot ────┘

Persistence ── DTO/Migration ─→ Core
Plugins ───── Public SDK ─────→ Stable contracts
```

### Core Model

允许依赖：基础类型、数学、序列化抽象。

禁止依赖：Avalonia、SkiaSharp、GPU、窗口、文件对话框、具体 UI Widget。

### Commands

允许依赖：Core Model、Domain Service。

禁止：菜单、按钮、Canvas 控件。所有文档 mutation 必须由这里进入。

### Render

只读 `DocumentSnapshot` 或稳定读取接口。任何 Renderer 都不能改 live Document。

### Tool

接收统一 PointerEvent，使用 Command API 修改文档。工具不能获取可写 PixelBuffer。

### Persistence

只负责显式 DTO、Schema、Migration、Atomic Save、Recovery。不能把 UI 状态混入项目数据。

## 2. 核心对象

```text
Workspace
 └── PixelDocument
      ├── CanvasSpec
      ├── LayerTree
      ├── Frames
      ├── Cels[layerId, frameId]
      ├── ResourceStore
      │    └── PixelSurface
      ├── Palettes        (后续)
      ├── Tilesets        (后续)
      └── MetadataTracks  (后续)
```

### ID 规则

- Document / Layer / Frame / Cel / Resource 使用稳定 ID。
- ID 可序列化，禁止把数组下标当长期身份。
- Cel 只引用 `SurfaceId`，不直接拥有 RGBA 数组。
- Linked Cel = 多个 Cel 指向同一个 SurfaceId。
- 解除 Linked Cel = clone Surface + 替换当前 Cel 的 SurfaceId。

## 3. PixelSurface

第一阶段只实现 RGBA32，但 API 不把 RGBA32 写死到文档上层。

必须保持：

- Surface 可以比 Canvas 小，Cel 独立保存 Position。
- mutation 增加 monotonically increasing Revision。
- mutation 输出 Dirty Region。
- 外部调用者只能读取；写入能力只开放给受控 mutation assembly。
- Snapshot 必须是不可受 live mutation 影响的独立数据。

未来 Indexed、Chunked surface 通过同一抽象扩展，不修改 Layer/Cel 的引用关系。

## 4. Command / Undo

```text
Tool/Input
   ↓
CommandBus
   ├── Apply Command
   ├── emit DocumentChange
   ├── UndoStack
   └── Transaction
```

规则：

1. Command 必须可 `Apply → Revert`。
2. 连续笔画、Transform 拖拽等使用 Transaction 合成一个 Undo 项。
3. 像素修改记录脏区 patch，不复制整个 Document。
4. 新命令执行后清空 redo branch。
5. Undo/Redo 后仍必须通过 `DocumentValidator`。

## 5. Render / Cache

后续 RenderGraph 使用：

```text
Surface.revision
  ↓
Cel composite key
  ↓
Layer composite key
  ↓
Frame composite key
  ↓
Canvas / thumbnail / onion-skin cache
```

正确性依赖 Revision；性能优化依赖 Dirty Region。两者不能互相替代。

GPU texture 永远是缓存。设备丢失或缓存清空后，必须能只靠 Document 重建画面。

## 6. Timeline

- Frame 使用稳定 FrameId。
- Duration 使用整数 tick/微秒，禁止 float 累积。
- Cel = LayerId + FrameId + ResourceId + Position + Opacity。
- Tags、Keyframes、Events 后续都引用稳定 ID，不依赖当前数组位置。

## 7. Tilemap

Tilemap 是专用数据结构：

```text
Tileset: TileId -> Tile Surface
Tilemap Cell: TileId + flip/rotate/variant/flags
```

禁止把地图直接烘焙成一张大 PixelSurface 作为源数据。

AutoTile 必须是 `Neighborhood -> TileChoice` 的规则接口，不能写死 47-tile。

## 8. 非破坏 Effect

Effect 是参数化派生结果：

```text
input revision + effect type + parameters -> output cache
```

需要永久修改时通过 Bake Command 写入 PixelLayer；Live Effect 自身不覆盖源像素。

## 9. Persistence

项目格式目标：

```text
project.pixelproj
 ├── manifest.json/msgpack
 ├── document.json
 ├── surfaces/<id>.bin
 ├── audio/
 ├── refs/
 └── plugin-data/
```

必须具备：SchemaVersion、Migration、Atomic Save、Unknown Field Preserve、Autosave/Recovery。

## 10. Plugin SDK

核心从一开始保留注册点：Tool、Command、Effect、Importer、Exporter、Panel、Palette Algorithm、Dither、AutoTile Rule。

插件持久化数据必须 namespace 化。未知 plugin payload 必须能原样保留，不能因为插件未安装导致项目打不开。

## 11. 永久不变量

- Cel 引用的 ResourceId 必须存在。
- Frame duration > 0。
- LayerTree 不能形成环。
- Undo 后语义状态恢复。
- 保存文件能在无 UI 环境下通过 DocumentValidator。
- Renderer 不拥有文档真值。
- UI 不越过 CommandBus mutation。

## 12. 禁止写法

- Canvas 鼠标事件直接改 `byte[]`。
- Cel 保存完整 RGBA 图并由每个帧复制。
- Tilemap 用一张大图当唯一源数据。
- 每个 Tool 自己维护 Undo。
- 导出逻辑写在按钮 click 回调。
- 插件引用 Core internal 类。
- Effect 调参直接破坏源像素。
- 数组下标作为长期 ID。
- 任意小修改都 full redraw。
- 直接序列化运行时对象图作为长期文件格式。
