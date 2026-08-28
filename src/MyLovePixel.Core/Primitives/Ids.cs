namespace MyLovePixel.Core.Primitives;

public readonly record struct DocumentId(Guid Value)
{
    public static DocumentId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct LayerId(Guid Value)
{
    public static LayerId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct FrameId(Guid Value)
{
    public static FrameId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct CelId(Guid Value)
{
    public static CelId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct ResourceId(Guid Value)
{
    public static ResourceId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct PaletteId(Guid Value)
{
    public static PaletteId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct TilesetId(Guid Value)
{
    public static TilesetId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct TileId(Guid Value)
{
    public static TileId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct TilemapId(Guid Value)
{
    public static TilemapId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct AnimationClipId(Guid Value)
{
    public static AnimationClipId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct AnimationTagId(Guid Value)
{
    public static AnimationTagId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct SliceId(Guid Value)
{
    public static SliceId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct AnimationTrackId(Guid Value)
{
    public static AnimationTrackId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}
