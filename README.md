# MyLovePixel

MyLovePixel 是一个面向私人游戏开发工作流的 Pixel Art 创作软件项目。

目标不是复刻某个现有编辑器，而是建立一套长期可扩展的像素文档核心：绘画、动画、Tilemap、调色板、元数据、导出和插件都围绕稳定的数据模型与命令系统增长。

## 当前工程原则

- UI 不直接修改 Document；所有可撤销修改统一经过 CommandBus。
- Cel、Tile、Palette 等共享数据使用稳定 ID/引用，而不是对象地址或数组下标。
- PixelSurface 是像素真值；GPU 纹理、缩略图和合成图都是可重建缓存。
- Core 不依赖 Avalonia、SkiaSharp、窗口或文件对话框。
- Render 只消费只读 Snapshot，不修改 live Document。
- 所有昂贵操作从设计上支持 Dirty Region / Revision / Cache Key。
- 内部项目格式采用显式 Schema + Migration，不使用语言原生对象图序列化。
- 插件只依赖稳定 Public SDK，不引用 Core internal 类型。

## 技术栈

- Runtime: .NET 10 / C# 14
- Desktop UI（后续批次）: Avalonia 12
- 2D/GPU backend（后续批次）: SkiaSharp
- Tests: xUnit v3

> UI 与渲染后端是可替换外壳，不是 Core 的依赖。

## 当前状态

仓库骨架和第一批核心代码已经建立：

- Stable IDs
- Canvas / PixelSurface / ResourceStore
- Layer / Frame / Cel / Linked Cel 引用模型
- DocumentValidator
- CommandBus
- Undo / Redo
- Transaction
- PixelPatchCommand
- UnlinkCelCommand
- Core roundtrip tests
- Headless CLI smoke path

下一步严格按 [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md) 推进。

## 目录

```text
src/
  MyLovePixel.Core/       文档模型、资源、像素真值、验证器
  MyLovePixel.Commands/   唯一修改入口、Undo/Redo、Transaction
  MyLovePixel.Cli/        无 UI 宿主，验证 Core 可独立运行
tests/
  MyLovePixel.Core.Tests/ Core / Command 不变量测试
docs/
  ARCHITECTURE.md         架构边界与依赖规则
  IMPLEMENTATION_PLAN.md  分批实现计划和验收门槛
  reference/              原始规格文档
```

## 本地构建

需要 .NET SDK 10.0.400 或兼容的 10.0.x SDK：

```bash
dotnet restore MyLovePixel.slnx
dotnet build MyLovePixel.slnx
dotnet test MyLovePixel.slnx
dotnet run --project src/MyLovePixel.Cli
```

## 当前不做

账号、登录、云同步、多人协作、订阅、支付、遥测、在线素材商城。这些都不进入工程边界。
