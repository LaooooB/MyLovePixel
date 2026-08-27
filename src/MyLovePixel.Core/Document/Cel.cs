using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Document;

public sealed class Cel
{
    public Cel(CelId id, LayerId layerId, FrameId frameId, ResourceId surfaceId)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("CelId cannot be empty.", nameof(id));
        if (layerId.Value == Guid.Empty) throw new ArgumentException("LayerId cannot be empty.", nameof(layerId));
        if (frameId.Value == Guid.Empty) throw new ArgumentException("FrameId cannot be empty.", nameof(frameId));
        if (surfaceId.Value == Guid.Empty) throw new ArgumentException("ResourceId cannot be empty.", nameof(surfaceId));
        Id = id;
        LayerId = layerId;
        FrameId = frameId;
        SurfaceId = surfaceId;
    }

    public CelId Id { get; }
    public LayerId LayerId { get; }
    public FrameId FrameId { get; }
    public ResourceId SurfaceId { get; internal set; }
    public IntPoint Position { get; internal set; }
    public byte Opacity { get; internal set; } = byte.MaxValue;
}
