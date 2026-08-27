using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Document;

public sealed record CanvasSpec(IntSize Size, PixelFormat PixelFormat = PixelFormat.Rgba32);
