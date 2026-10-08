using MyLovePixel.Core.Pixel;

namespace MyLovePixel.Application;

public sealed record UserPaletteColor(Rgba32 Color, string Name, string? FolderId);
public sealed record UserPaletteFolder(string Id, string Name);
