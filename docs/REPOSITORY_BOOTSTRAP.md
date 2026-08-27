# Repository Bootstrap Record

本仓库初始化时只实现了依赖链最底层的两个批次：Document Core 与 Command/Undo。没有为了“看起来像编辑器”提前加入 UI。

## 已创建的首批代码

- `MyLovePixel.Core`: 稳定 ID、几何、RGBA32 Surface、资源存储、Layer/Frame/Cel、Snapshot、Validator。
- `MyLovePixel.Commands`: Command、UndoToken、CommandBus、Transaction、PixelPatch、UnlinkCel。
- `MyLovePixel.Cli`: 无 UI smoke path。
- `MyLovePixel.Core.Tests`: 数据不变量、Undo/Redo、Transaction、Snapshot、Linked Cel 测试。

## 下一批

Persistence。原因：一旦继续增加 Raster/Selection/Timeline，项目状态会迅速扩张。如果内部 Schema、Migration 和 Atomic Save 还没固定，之后每加一个 Model 都会反复改存储边界。
