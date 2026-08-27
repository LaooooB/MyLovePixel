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
