using MyLovePixel.Core.Document;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Render;

public sealed record ThumbnailCacheOptions(int MaxEntries = 256, long MaxBytes = 64L * 1024 * 1024)
{
    public void Validate()
    {
        if (MaxEntries < 1) throw new ArgumentOutOfRangeException(nameof(MaxEntries));
        if (MaxBytes < 1) throw new ArgumentOutOfRangeException(nameof(MaxBytes));
    }
}

public sealed class ThumbnailImage
{
    private readonly byte[] _rgba;

    internal ThumbnailImage(IntSize size, byte[] rgba)
    {
        Size = size;
        _rgba = rgba ?? throw new ArgumentNullException(nameof(rgba));
        if (_rgba.Length != checked(size.Width * size.Height * 4))
            throw new ArgumentException("Thumbnail RGBA length does not match size.", nameof(rgba));
    }

    public IntSize Size { get; }
    public ReadOnlyMemory<byte> Rgba => _rgba;
    public long ByteLength => _rgba.LongLength;
}

public sealed record ThumbnailCacheDiagnosticsSnapshot(
    long HitCount,
    long MissCount,
    long EvictionCount,
    long OversizeBypassCount,
    int EntryCount,
    long ByteCount)
{
    public long RequestCount => HitCount + MissCount;
    public double HitRatio => RequestCount == 0 ? 0d : (double)HitCount / RequestCount;
}

public sealed class ThumbnailCache
{
    private readonly ThumbnailCacheOptions _options;
    private readonly FrameRenderer _renderer;
    private readonly Dictionary<ThumbnailCacheKey, LinkedListNode<CacheEntry>> _entries = [];
    private readonly LinkedList<CacheEntry> _lru = new();
    private long _bytes;
    private long _hits;
    private long _misses;
    private long _evictions;
    private long _oversizeBypasses;

    public ThumbnailCache(ThumbnailCacheOptions? options = null, FrameRenderer? renderer = null)
    {
        _options = options ?? new ThumbnailCacheOptions();
        _options.Validate();
        _renderer = renderer ?? new FrameRenderer();
    }

    public ThumbnailCacheDiagnosticsSnapshot Diagnostics => new(
        _hits,
        _misses,
        _evictions,
        _oversizeBypasses,
        _entries.Count,
        _bytes);

    public ThumbnailImage Get(DocumentSnapshot snapshot, FrameId frameId, IntSize maxSize)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (maxSize.Width <= 0 || maxSize.Height <= 0) throw new ArgumentOutOfRangeException(nameof(maxSize));
        if (!snapshot.Frames.ContainsKey(frameId)) throw new ArgumentException($"Frame '{frameId}' does not exist.", nameof(frameId));

        var targetSize = CalculateTargetSize(snapshot.Canvas.Size, maxSize);
        var key = ThumbnailCacheKey.Capture(snapshot, frameId, targetSize);
        if (_entries.TryGetValue(key, out var node))
        {
            _hits++;
            Touch(node);
            return node.Value.Image;
        }

        _misses++;
        var rendered = _renderer.Render(snapshot, new FrameRenderRequest(frameId));
        var image = ResizeNearest(rendered.Surface.Size, rendered.Surface.Bytes.Span, targetSize);
        if (image.ByteLength > _options.MaxBytes)
        {
            _oversizeBypasses++;
            return image;
        }

        var entry = new CacheEntry(key, image);
        var added = _lru.AddFirst(entry);
        _entries.Add(key, added);
        _bytes = checked(_bytes + image.ByteLength);
        Trim();
        return image;
    }

    public void Clear()
    {
        _entries.Clear();
        _lru.Clear();
        _bytes = 0;
    }

    private void Touch(LinkedListNode<CacheEntry> node)
    {
        if (ReferenceEquals(node, _lru.First)) return;
        _lru.Remove(node);
        _lru.AddFirst(node);
    }

    private void Trim()
    {
        while (_entries.Count > _options.MaxEntries || _bytes > _options.MaxBytes)
        {
            var node = _lru.Last ?? throw new InvalidOperationException("LRU state is inconsistent.");
            _lru.RemoveLast();
            _entries.Remove(node.Value.Key);
            _bytes = checked(_bytes - node.Value.Image.ByteLength);
            _evictions++;
        }
    }

    private static IntSize CalculateTargetSize(IntSize source, IntSize maxSize)
    {
        if (source.Width <= maxSize.Width && source.Height <= maxSize.Height) return source;
        var scale = Math.Min((double)maxSize.Width / source.Width, (double)maxSize.Height / source.Height);
        return new IntSize(
            Math.Max(1, (int)Math.Floor(source.Width * scale)),
            Math.Max(1, (int)Math.Floor(source.Height * scale)));
    }

    private static ThumbnailImage ResizeNearest(IntSize sourceSize, ReadOnlySpan<byte> source, IntSize targetSize)
    {
        var expected = checked(sourceSize.Width * sourceSize.Height * 4);
        if (source.Length != expected) throw new ArgumentException("Rendered surface RGBA length is invalid.", nameof(source));
        var target = new byte[checked(targetSize.Width * targetSize.Height * 4)];
        for (var y = 0; y < targetSize.Height; y++)
        {
            var sourceY = Math.Min(sourceSize.Height - 1, (int)((long)y * sourceSize.Height / targetSize.Height));
            for (var x = 0; x < targetSize.Width; x++)
            {
                var sourceX = Math.Min(sourceSize.Width - 1, (int)((long)x * sourceSize.Width / targetSize.Width));
                var sourceOffset = ((sourceY * sourceSize.Width) + sourceX) * 4;
                var targetOffset = ((y * targetSize.Width) + x) * 4;
                source.Slice(sourceOffset, 4).CopyTo(target.AsSpan(targetOffset, 4));
            }
        }
        return new ThumbnailImage(targetSize, target);
    }

    private sealed record CacheEntry(ThumbnailCacheKey Key, ThumbnailImage Image);

    private sealed class ThumbnailCacheKey : IEquatable<ThumbnailCacheKey>
    {
        private readonly FrameStructureSignature _structure;
        private readonly ResourceRevisionState[] _revisions;

        private ThumbnailCacheKey(DocumentId documentId, FrameId frameId, IntSize size, FrameStructureSignature structure, ResourceRevisionState[] revisions)
        {
            DocumentId = documentId;
            FrameId = frameId;
            Size = size;
            _structure = structure;
            _revisions = revisions;
        }

        private DocumentId DocumentId { get; }
        private FrameId FrameId { get; }
        private IntSize Size { get; }

        public static ThumbnailCacheKey Capture(DocumentSnapshot snapshot, FrameId frameId, IntSize size) =>
            new(
                snapshot.Id,
                frameId,
                size,
                FrameStructureSignature.Capture(snapshot, frameId),
                FrameRevisionSignature.Capture(snapshot, frameId));

        public bool Equals(ThumbnailCacheKey? other) =>
            other is not null &&
            DocumentId == other.DocumentId &&
            FrameId == other.FrameId &&
            Size == other.Size &&
            _structure.Equals(other._structure) &&
            _revisions.AsSpan().SequenceEqual(other._revisions);

        public override bool Equals(object? obj) => obj is ThumbnailCacheKey other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(DocumentId);
            hash.Add(FrameId);
            hash.Add(Size);
            hash.Add(_structure);
            foreach (var revision in _revisions) hash.Add(revision);
            return hash.ToHashCode();
        }
    }
}
