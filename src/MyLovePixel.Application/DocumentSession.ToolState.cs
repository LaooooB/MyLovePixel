namespace MyLovePixel.Application;

public sealed partial class DocumentSession
{
    private readonly Dictionary<string, Dictionary<string, object>> _rememberedToolOptions = new(StringComparer.Ordinal);

    private void RememberToolOptions()
    {
        if (_toolHost is null) return;
        _rememberedToolOptions[_activeToolId] = ToolPresentationMapper.DescribeOptions(_toolHost)
            .ToDictionary(option => option.Id, option => option.Value, StringComparer.Ordinal);
    }

    private void RestoreToolOptions()
    {
        if (_toolHost is null || !_rememberedToolOptions.TryGetValue(_activeToolId, out var values)) return;
        foreach (var option in values) _toolHost.SetOption(option.Key, option.Value);
    }
}
