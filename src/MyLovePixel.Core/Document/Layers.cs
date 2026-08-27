using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Core.Document;

public abstract class Layer
{
    protected Layer(LayerId id, string name)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("LayerId cannot be empty.", nameof(id));
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? "Layer" : name;
    }

    public LayerId Id { get; }
    public string Name { get; internal set; }
    public bool Visible { get; internal set; } = true;
    public bool Locked { get; internal set; }
    public byte Opacity { get; internal set; } = byte.MaxValue;
}

public sealed class PixelLayer(LayerId id, string name) : Layer(id, name) { }
