using System.Collections.ObjectModel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Export;

public static class BuiltinAtlasPackerIds
{
    public const string DeterministicShelf = "builtin.shelf";
}

public sealed record AtlasItem
{
    public AtlasItem(string key, IntSize size)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Atlas item key cannot be empty.", nameof(key));
        Key = key;
        Size = size;
    }

    public string Key { get; }
    public IntSize Size { get; }
}

public sealed record AtlasPackingOptions
{
    public AtlasPackingOptions(int maxWidth, int maxHeight, int padding, bool powerOfTwo)
    {
        if (maxWidth <= 0) throw new ArgumentOutOfRangeException(nameof(maxWidth));
        if (maxHeight <= 0) throw new ArgumentOutOfRangeException(nameof(maxHeight));
        if (padding < 0) throw new ArgumentOutOfRangeException(nameof(padding));
        MaxWidth = maxWidth;
        MaxHeight = maxHeight;
        Padding = padding;
        PowerOfTwo = powerOfTwo;
    }

    public int MaxWidth { get; }
    public int MaxHeight { get; }
    public int Padding { get; }
    public bool PowerOfTwo { get; }
}

public sealed record AtlasPlacement(string Key, int PageIndex, IntRect Rect);
public sealed record AtlasPagePacking(int PageIndex, IntSize Size, IReadOnlyList<AtlasPlacement> Placements);

public sealed class AtlasPackingResult
{
    private readonly AtlasPagePacking[] _pages;
    private readonly IReadOnlyDictionary<string, AtlasPlacement> _byKey;

    public AtlasPackingResult(IEnumerable<AtlasPagePacking> pages)
    {
        _pages = pages?.ToArray() ?? throw new ArgumentNullException(nameof(pages));
        _byKey = new ReadOnlyDictionary<string, AtlasPlacement>(_pages
            .SelectMany(page => page.Placements)
            .ToDictionary(item => item.Key, StringComparer.Ordinal));
    }

    public IReadOnlyList<AtlasPagePacking> Pages => Array.AsReadOnly(_pages);
    public AtlasPlacement GetPlacement(string key) => _byKey.TryGetValue(key, out var value)
        ? value
        : throw new KeyNotFoundException($"Atlas item '{key}' was not packed.");
}

public interface IAtlasPacker
{
    string Id { get; }
    AtlasPackingResult Pack(IReadOnlyList<AtlasItem> items, AtlasPackingOptions options);
}

public sealed class AtlasPackerRegistry
{
    private readonly Dictionary<string, IAtlasPacker> _packers = new(StringComparer.Ordinal);

    public AtlasPackerRegistry(IEnumerable<IAtlasPacker>? packers = null)
    {
        foreach (var packer in packers ?? Array.Empty<IAtlasPacker>()) Register(packer);
    }

    public void Register(IAtlasPacker packer)
    {
        ArgumentNullException.ThrowIfNull(packer);
        if (string.IsNullOrWhiteSpace(packer.Id)) throw new ArgumentException("Atlas packer Id cannot be empty.", nameof(packer));
        if (!_packers.TryAdd(packer.Id, packer)) throw new InvalidOperationException($"Atlas packer '{packer.Id}' is already registered.");
    }

    public IAtlasPacker Get(string id) => _packers.TryGetValue(id, out var packer)
        ? packer
        : throw new KeyNotFoundException($"Atlas packer '{id}' is not registered.");

    public static AtlasPackerRegistry CreateDefault() => new([new DeterministicShelfAtlasPacker()]);
}

public sealed class DeterministicShelfAtlasPacker : IAtlasPacker
{
    public string Id => BuiltinAtlasPackerIds.DeterministicShelf;

    public AtlasPackingResult Pack(IReadOnlyList<AtlasItem> items, AtlasPackingOptions options)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(options);
        if (items.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != items.Count)
            throw new ArgumentException("Atlas item keys must be unique.", nameof(items));
        if (items.Count == 0) return new AtlasPackingResult(Array.Empty<AtlasPagePacking>());

        var ordered = items
            .OrderByDescending(item => item.Size.Height)
            .ThenByDescending(item => item.Size.Width)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();

        foreach (var item in ordered)
        {
            if (item.Size.Width > options.MaxWidth || item.Size.Height > options.MaxHeight)
                throw new InvalidOperationException($"Atlas item '{item.Key}' ({item.Size.Width}x{item.Size.Height}) exceeds atlas limits {options.MaxWidth}x{options.MaxHeight}.");
        }

        var pages = new List<MutablePage>();
        foreach (var item in ordered)
        {
            var placed = pages.Any(page => page.TryPlace(item, options.Padding));
            if (placed) continue;
            var created = new MutablePage(pages.Count, options.MaxWidth, options.MaxHeight);
            if (!created.TryPlace(item, options.Padding)) throw new InvalidOperationException($"Atlas item '{item.Key}' cannot fit on an empty page.");
            pages.Add(created);
        }

        return new AtlasPackingResult(pages.Select(page => page.Freeze(options.PowerOfTwo)).ToArray());
    }

    private sealed class MutablePage
    {
        private readonly int _pageIndex;
        private readonly int _maxWidth;
        private readonly int _maxHeight;
        private readonly List<AtlasPlacement> _placements = [];
        private int _x;
        private int _y;
        private int _shelfHeight;
        private int _usedWidth;
        private int _usedHeight;

        public MutablePage(int pageIndex, int maxWidth, int maxHeight)
        {
            _pageIndex = pageIndex;
            _maxWidth = maxWidth;
            _maxHeight = maxHeight;
        }

        public bool TryPlace(AtlasItem item, int padding)
        {
            var prospectiveX = _x == 0 ? 0 : checked(_x + padding);
            if (prospectiveX + item.Size.Width > _maxWidth)
            {
                var nextY = checked(_y + _shelfHeight + (_placements.Count == 0 ? 0 : padding));
                if (nextY + item.Size.Height > _maxHeight) return false;
                _x = 0;
                _y = nextY;
                _shelfHeight = 0;
                prospectiveX = 0;
            }

            if (_y + item.Size.Height > _maxHeight) return false;
            _x = prospectiveX;
            var rect = new IntRect(_x, _y, item.Size.Width, item.Size.Height);
            _placements.Add(new AtlasPlacement(item.Key, _pageIndex, rect));
            _x = checked(_x + item.Size.Width);
            _shelfHeight = Math.Max(_shelfHeight, item.Size.Height);
            _usedWidth = Math.Max(_usedWidth, _x);
            _usedHeight = Math.Max(_usedHeight, checked(_y + item.Size.Height));
            return true;
        }

        public AtlasPagePacking Freeze(bool powerOfTwo)
        {
            var width = Math.Max(1, _usedWidth);
            var height = Math.Max(1, _usedHeight);
            if (powerOfTwo)
            {
                width = NextPowerOfTwo(width);
                height = NextPowerOfTwo(height);
            }
            return new AtlasPagePacking(_pageIndex, new IntSize(width, height), Array.AsReadOnly(_placements.ToArray()));
        }

        private static int NextPowerOfTwo(int value)
        {
            var result = 1;
            while (result < value) result = checked(result * 2);
            return result;
        }
    }
}
