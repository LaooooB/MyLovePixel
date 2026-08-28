namespace MyLovePixel.Color;

public sealed class ColorRamp
{
    private readonly byte[] _indices;
    private readonly Dictionary<byte, int> _positions;

    public ColorRamp(IEnumerable<byte> indices)
    {
        ArgumentNullException.ThrowIfNull(indices);
        _indices = indices.ToArray();
        if (_indices.Length == 0)
            throw new ArgumentException("Color ramp must contain at least one palette index.", nameof(indices));
        if (_indices.Distinct().Count() != _indices.Length)
            throw new ArgumentException("Color ramp indices must be unique.", nameof(indices));
        _positions = _indices
            .Select((value, position) => (value, position))
            .ToDictionary(item => item.value, item => item.position);
    }

    public IReadOnlyList<byte> Indices => Array.AsReadOnly(_indices);

    public bool Contains(byte index) => _positions.ContainsKey(index);

    public byte Shade(byte index, int stepDelta)
    {
        if (!_positions.TryGetValue(index, out var position)) return index;
        var target = Math.Clamp((long)position + stepDelta, 0, _indices.Length - 1);
        return _indices[checked((int)target)];
    }
}

public interface IIndexedInkStrategy
{
    byte Apply(byte destinationIndex);
}

public sealed class ColorRampShadingInk : IIndexedInkStrategy
{
    public ColorRampShadingInk(ColorRamp ramp, int stepDelta)
    {
        Ramp = ramp ?? throw new ArgumentNullException(nameof(ramp));
        StepDelta = stepDelta;
    }

    public ColorRamp Ramp { get; }
    public int StepDelta { get; }

    public byte Apply(byte destinationIndex) =>
        Ramp.Shade(destinationIndex, StepDelta);
}
