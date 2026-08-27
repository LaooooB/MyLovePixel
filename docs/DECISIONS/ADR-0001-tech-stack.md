# ADR-0001: .NET 10 + Avalonia + SkiaSharp

Status: Accepted

## Decision

Core 与领域算法使用 .NET 10 / C# 14。桌面 UI 后续使用 Avalonia；渲染后端后续使用 SkiaSharp。

## Reasons

- 私人长期工具优先降低维护和重构成本。
- C# 足以承载文档模型、命令系统、序列化、算法和插件宿主。
- 性能敏感路径可以用连续内存、Span、ArrayPool、unsafe/interop 做局部优化，不需要让整个项目承担 C++ 生命周期成本。
- Avalonia 与 Core 完全隔离，未来更换 UI 不改变文档格式和算法。
- Skia 后端只是 Renderer 实现，不是像素真值存储。

## Non-goals

- 不让 Avalonia 类型进入 Core DTO。
- 不把 SKBitmap/SKImage 当项目持久化资源。
- 不承诺所有未来高级模块都必须纯 C#；必要时可通过稳定接口接 native module。
