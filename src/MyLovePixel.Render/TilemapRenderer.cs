using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Core.Tiles;
using MyLovePixel.Tilemap;

namespace MyLovePixel.Render;

public readonly record struct TilemapInvalidation
{
    public TilemapInvalidation(
        TilemapId tilemapId,
        long fromRevision,
        long toRevision,
        IEnumerable<IntPoint> cells)
    {
        if (tilemapId.Value == Guid.Empty) throw new ArgumentException("TilemapId cannot be empty.", nameof(tilemapId));
        if (fromRevision < 0) throw new ArgumentOutOfRangeException(nameof(fromRevision));
        if (toRevision <= fromRevision) throw new ArgumentOutOfRangeException(nameof(toRevision));
        ArgumentNullException.ThrowIfNull(cells);
        var copy = cells.Distinct().ToArray();
        if (copy.Length == 0) throw new ArgumentException("Tilemap invalidation must contain at least one cell.", nameof(cells));
        TilemapId = tilemapId;
        FromRevision = fromRevision;
        ToRevision = toRevision;
        Cells = Array.AsReadOnly(copy);
    }

    public TilemapId TilemapId { get; }
    public long FromRevision { get; }
    public long ToRevision { get; }
    public IReadOnlyList<IntPoint> Cells { get; }
}

public sealed class TilemapRenderRequest
{
    public TilemapRenderRequest(
        TilemapId tilemapId,
        IntRect cellRegion,
        IReadOnlyList<TilemapInvalidation>? tilemapInvalidations = null,
        IReadOnlyList<SurfaceInvalidation>? surfaceInvalidations = null)
    {
        if (tilemapId.Value == Guid.Empty) throw new ArgumentException("TilemapId cannot be empty.", nameof(tilemapId));
        if (cellRegion.IsEmpty) throw new ArgumentException("Cell region must be non-empty.", nameof(cellRegion));
        TilemapId = tilemapId;
        CellRegion = cellRegion;
        TilemapInvalidations = tilemapInvalidations;
        SurfaceInvalidations = surfaceInvalidations;
    }

    public TilemapId TilemapId { get; }
    public IntRect CellRegion { get; }
    public IReadOnlyList<TilemapInvalidation>? TilemapInvalidations { get; }
    public IReadOnlyList<SurfaceInvalidation>? SurfaceInvalidations { get; }
}

public sealed record TilemapRenderResult(
    CpuRenderSurface Surface,
    TextureUploadPlan UploadPlan,
    RenderCacheOutcome CacheOutcome,
    RenderCacheDiagnosticsSnapshot Diagnostics)
{
    public bool UsedPartialRecompose => CacheOutcome == RenderCacheOutcome.PartialRecompose;
}

public sealed class TilemapRenderer
{
    private readonly Dictionary<TilemapCacheKey, TilemapCacheEntry> _cache = [];

    public RenderCacheDiagnostics Diagnostics { get; } = new();

    public TilemapRenderResult Render(DocumentSnapshot snapshot, TilemapRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);

        var tilemap = snapshot.GetTilemap(request.TilemapId);
        if (!string.Equals(tilemap.TopologyId, RectGridTopology.Instance.Id, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Tilemap renderer currently supports topology '{RectGridTopology.Instance.Id}', received '{tilemap.TopologyId}'.");
        var tileset = snapshot.GetTileset(tilemap.TilesetId);
        var outputSize = GetOutputSize(request.CellRegion, tileset.TileSize);
        var structure = TilemapStructureState.Capture(tilemap, tileset);
        var palettes = CapturePaletteStates(snapshot, tileset);
        var surfaces = CaptureSurfaceStates(snapshot, tileset);
        var key = new TilemapCacheKey(snapshot.Id, tilemap.Id, request.CellRegion);
        IReadOnlyList<IntRect> dirtyRegions = Array.Empty<IntRect>();
        RenderCacheOutcome outcome;
        TilemapCacheEntry entry;

        if (!_cache.TryGetValue(key, out entry!) ||
            entry.Target.Size != outputSize ||
            entry.Structure != structure ||
            !entry.Palettes.AsSpan().SequenceEqual(palettes))
        {
            entry = CreateFullEntry(snapshot, tilemap, tileset, request.CellRegion, structure, palettes, surfaces, outputSize);
            _cache[key] = entry;
            outcome = RenderCacheOutcome.FullRecompose;
            Diagnostics.RecordFull(outputSize);
        }
        else if (tilemap.Revision < entry.TilemapRevision ||
                 !TryFindChangedSurfaces(entry.Surfaces, surfaces, out var changedSurfaces))
        {
            RecomposeFull(snapshot, tilemap, tileset, request.CellRegion, entry.Target);
            entry.Structure = structure;
            entry.Palettes = palettes;
            entry.Surfaces = surfaces;
            entry.TilemapRevision = tilemap.Revision;
            outcome = RenderCacheOutcome.FullRecompose;
            Diagnostics.RecordFull(outputSize);
        }
        else if (TryBuildDirtyRegions(
                     snapshot,
                     tilemap,
                     tileset,
                     request,
                     entry.TilemapRevision,
                     changedSurfaces,
                     out dirtyRegions))
        {
            if (dirtyRegions.Count == 0)
            {
                entry.Surfaces = surfaces;
                entry.TilemapRevision = tilemap.Revision;
                outcome = RenderCacheOutcome.CacheHit;
                Diagnostics.RecordHit();
            }
            else
            {
                foreach (var region in dirtyRegions)
                    RedrawRegion(snapshot, tilemap, tileset, request.CellRegion, entry.Target, region);
                entry.Surfaces = surfaces;
                entry.TilemapRevision = tilemap.Revision;
                outcome = RenderCacheOutcome.PartialRecompose;
                Diagnostics.RecordPartial(dirtyRegions);
            }
        }
        else
        {
            RecomposeFull(snapshot, tilemap, tileset, request.CellRegion, entry.Target);
            entry.Surfaces = surfaces;
            entry.TilemapRevision = tilemap.Revision;
            outcome = RenderCacheOutcome.FullRecompose;
            Diagnostics.RecordFull(outputSize);
        }

        var uploadPlan = TextureUploadPlanner.Plan(outcome, outputSize, dirtyRegions);
        return new TilemapRenderResult(
            entry.Target.Snapshot(),
            uploadPlan,
            outcome,
            Diagnostics.Snapshot());
    }

    public void ClearCaches() => _cache.Clear();

    private static TilemapCacheEntry CreateFullEntry(
        DocumentSnapshot snapshot,
        TilemapSnapshot tilemap,
        TilesetSnapshot tileset,
        IntRect cellRegion,
        TilemapStructureState structure,
        PaletteRevisionState[] palettes,
        TileSurfaceRevisionState[] surfaces,
        IntSize outputSize)
    {
        var target = new CpuRenderTarget(outputSize);
        RecomposeFull(snapshot, tilemap, tileset, cellRegion, target);
        return new TilemapCacheEntry(
            target,
            structure,
            palettes,
            surfaces,
            tilemap.Revision);
    }

    private static void RecomposeFull(
        DocumentSnapshot snapshot,
        TilemapSnapshot tilemap,
        TilesetSnapshot tileset,
        IntRect cellRegion,
        CpuRenderTarget target)
    {
        var bounds = RenderMath.Bounds(target.Size);
        target.Clear(bounds);
        RedrawRegion(snapshot, tilemap, tileset, cellRegion, target, bounds);
    }

    private static bool TryBuildDirtyRegions(
        DocumentSnapshot snapshot,
        TilemapSnapshot tilemap,
        TilesetSnapshot tileset,
        TilemapRenderRequest request,
        long previousTilemapRevision,
        IReadOnlyList<ChangedTileSurface> changedSurfaces,
        out IReadOnlyList<IntRect> dirtyRegions)
    {
        var mapped = new List<IntRect>();
        var outputBounds = new IntRect(
            0,
            0,
            checked(request.CellRegion.Width * tileset.TileSize.Width),
            checked(request.CellRegion.Height * tileset.TileSize.Height));

        if (tilemap.Revision != previousTilemapRevision)
        {
            if (!TryCollectCoveredTilemapCells(
                    tilemap.Id,
                    previousTilemapRevision,
                    tilemap.Revision,
                    request.TilemapInvalidations,
                    out var changedCells))
            {
                dirtyRegions = Array.Empty<IntRect>();
                return false;
            }

            foreach (var coordinate in changedCells)
            {
                if (!ContainsCell(request.CellRegion, coordinate)) continue;
                mapped.Add(CellToOutputRect(coordinate, request.CellRegion, tileset.TileSize));
            }
        }

        foreach (var changed in changedSurfaces)
        {
            var referencedCells = GetCellsForSurface(tilemap, tileset, changed.SurfaceId, request.CellRegion);
            if (referencedCells.Count == 0) continue;

            if (!HasCompleteSurfaceCoverage(changed, request.SurfaceInvalidations))
            {
                dirtyRegions = Array.Empty<IntRect>();
                return false;
            }

            foreach (var coordinate in referencedCells)
                mapped.Add(CellToOutputRect(coordinate, request.CellRegion, tileset.TileSize));
        }

        dirtyRegions = RenderRegionSet.Normalize(mapped, outputBounds);
        return true;
    }

    private static bool TryCollectCoveredTilemapCells(
        TilemapId tilemapId,
        long fromRevision,
        long toRevision,
        IReadOnlyList<TilemapInvalidation>? invalidations,
        out IReadOnlyList<IntPoint> cells)
    {
        if (invalidations is null || invalidations.Count == 0)
        {
            cells = Array.Empty<IntPoint>();
            return false;
        }

        var candidates = invalidations
            .Where(item =>
                item.TilemapId == tilemapId &&
                item.ToRevision > fromRevision &&
                item.FromRevision < toRevision)
            .OrderBy(item => item.FromRevision)
            .ThenByDescending(item => item.ToRevision)
            .ToArray();
        if (candidates.Length == 0)
        {
            cells = Array.Empty<IntPoint>();
            return false;
        }

        var used = new HashSet<IntPoint>();
        var cursor = fromRevision;
        while (cursor < toRevision)
        {
            var covering = candidates
                .Where(item => item.FromRevision <= cursor && item.ToRevision > cursor)
                .ToArray();
            if (covering.Length == 0)
            {
                cells = Array.Empty<IntPoint>();
                return false;
            }

            var farthest = covering.Max(item => item.ToRevision);
            foreach (var item in covering)
            foreach (var coordinate in item.Cells)
                used.Add(coordinate);
            cursor = farthest;
        }

        cells = used.OrderBy(value => value.Y).ThenBy(value => value.X).ToArray();
        return true;
    }

    private static bool HasCompleteSurfaceCoverage(
        ChangedTileSurface changed,
        IReadOnlyList<SurfaceInvalidation>? invalidations)
    {
        if (invalidations is null || invalidations.Count == 0) return false;
        var candidates = invalidations
            .Where(item =>
                item.SurfaceId == changed.SurfaceId &&
                item.ToRevision > changed.FromRevision &&
                item.FromRevision < changed.ToRevision)
            .OrderBy(item => item.FromRevision)
            .ThenByDescending(item => item.ToRevision)
            .ToArray();
        if (candidates.Length == 0) return false;

        var cursor = changed.FromRevision;
        while (cursor < changed.ToRevision)
        {
            var farthest = cursor;
            foreach (var item in candidates)
            {
                if (item.FromRevision <= cursor && item.ToRevision > farthest)
                    farthest = item.ToRevision;
            }
            if (farthest == cursor) return false;
            cursor = farthest;
        }
        return true;
    }

    private static IReadOnlyList<IntPoint> GetCellsForSurface(
        TilemapSnapshot tilemap,
        TilesetSnapshot tileset,
        ResourceId surfaceId,
        IntRect cellRegion)
    {
        var result = new List<IntPoint>();
        foreach (var pair in tilemap.EnumerateCells())
        {
            if (!ContainsCell(cellRegion, pair.Key)) continue;
            var tile = tileset.GetTile(pair.Value.TileId);
            if (tile.SurfaceId == surfaceId) result.Add(pair.Key);
        }
        return result;
    }

    private static void RedrawRegion(
        DocumentSnapshot snapshot,
        TilemapSnapshot tilemap,
        TilesetSnapshot tileset,
        IntRect cellRegion,
        CpuRenderTarget target,
        IntRect region)
    {
        var clipped = RenderMath.Intersect(region, RenderMath.Bounds(target.Size));
        if (clipped.IsEmpty) return;
        target.Clear(clipped);

        foreach (var pair in tilemap.EnumerateCells())
        {
            if (!ContainsCell(cellRegion, pair.Key)) continue;
            var cellRect = CellToOutputRect(pair.Key, cellRegion, tileset.TileSize);
            var drawRegion = RenderMath.Intersect(cellRect, clipped);
            if (drawRegion.IsEmpty) continue;
            DrawCell(snapshot, tileset, pair.Value, cellRect, target, drawRegion);
        }
    }

    private static void DrawCell(
        DocumentSnapshot snapshot,
        TilesetSnapshot tileset,
        TileCell cell,
        IntRect cellRect,
        CpuRenderTarget target,
        IntRect drawRegion)
    {
        var tile = tileset.GetTile(cell.TileId);
        var surface = snapshot.GetSurface(tile.SurfaceId);
        PaletteSnapshot? palette = null;
        if (surface.Format == PixelFormat.Indexed8)
        {
            if (surface.PaletteId is not { } paletteId)
                throw new InvalidOperationException($"Indexed tile surface '{tile.SurfaceId}' has no palette reference.");
            palette = snapshot.GetPalette(paletteId);
        }

        for (var y = drawRegion.Y; y < drawRegion.Bottom; y++)
        for (var x = drawRegion.X; x < drawRegion.Right; x++)
        {
            var local = new IntPoint(x - cellRect.X, y - cellRect.Y);
            var source = TileCellTransform.MapDestinationToSource(local, tileset.TileSize, cell.Flags);
            var color = surface.Format switch
            {
                PixelFormat.Rgba32 => surface.GetPixel(source.X, source.Y),
                PixelFormat.Indexed8 => palette!.ResolveColor(surface.GetIndex(source.X, source.Y)),
                _ => throw new NotSupportedException($"Tile surface format '{surface.Format}' is not supported."),
            };
            target.SetPixel(x, y, color);
        }
    }

    private static bool ContainsCell(IntRect region, IntPoint coordinate) =>
        coordinate.X >= region.X &&
        coordinate.Y >= region.Y &&
        (long)coordinate.X < (long)region.X + region.Width &&
        (long)coordinate.Y < (long)region.Y + region.Height;

    private static IntRect CellToOutputRect(IntPoint coordinate, IntRect cellRegion, IntSize tileSize) =>
        new(
            checked((coordinate.X - cellRegion.X) * tileSize.Width),
            checked((coordinate.Y - cellRegion.Y) * tileSize.Height),
            tileSize.Width,
            tileSize.Height);

    private static IntSize GetOutputSize(IntRect cellRegion, IntSize tileSize) =>
        new(
            checked(cellRegion.Width * tileSize.Width),
            checked(cellRegion.Height * tileSize.Height));

    private static PaletteRevisionState[] CapturePaletteStates(DocumentSnapshot snapshot, TilesetSnapshot tileset) =>
        tileset.TileOrder
            .Select(tileId => snapshot.GetSurface(tileset.GetTile(tileId).SurfaceId))
            .Where(surface => surface.PaletteId is not null)
            .Select(surface => surface.PaletteId!.Value)
            .Distinct()
            .OrderBy(id => id.Value)
            .Select(id => new PaletteRevisionState(id, snapshot.GetPalette(id).Revision))
            .ToArray();

    private static TileSurfaceRevisionState[] CaptureSurfaceStates(DocumentSnapshot snapshot, TilesetSnapshot tileset) =>
        tileset.TileOrder
            .Select(tileId => tileset.GetTile(tileId).SurfaceId)
            .Distinct()
            .OrderBy(id => id.Value)
            .Select(id => new TileSurfaceRevisionState(id, snapshot.GetSurface(id).Revision))
            .ToArray();

    private static bool TryFindChangedSurfaces(
        TileSurfaceRevisionState[] previous,
        TileSurfaceRevisionState[] current,
        out IReadOnlyList<ChangedTileSurface> changed)
    {
        if (previous.Length != current.Length)
        {
            changed = Array.Empty<ChangedTileSurface>();
            return false;
        }

        var result = new List<ChangedTileSurface>();
        for (var index = 0; index < previous.Length; index++)
        {
            var before = previous[index];
            var after = current[index];
            if (before.SurfaceId != after.SurfaceId || after.Revision < before.Revision)
            {
                changed = Array.Empty<ChangedTileSurface>();
                return false;
            }
            if (before.Revision != after.Revision)
                result.Add(new ChangedTileSurface(after.SurfaceId, before.Revision, after.Revision));
        }
        changed = result;
        return true;
    }

    private readonly record struct TilemapCacheKey(DocumentId DocumentId, TilemapId TilemapId, IntRect CellRegion);
    private readonly record struct TileSurfaceRevisionState(ResourceId SurfaceId, long Revision);
    private readonly record struct PaletteRevisionState(PaletteId PaletteId, long Revision);
    private readonly record struct ChangedTileSurface(ResourceId SurfaceId, long FromRevision, long ToRevision);
    private readonly record struct TilemapStructureState(
        TilesetId TilesetId,
        string TopologyId,
        IntSize TileSize,
        long TilesetRevision)
    {
        public static TilemapStructureState Capture(TilemapSnapshot tilemap, TilesetSnapshot tileset) =>
            new(tilemap.TilesetId, tilemap.TopologyId, tileset.TileSize, tileset.Revision);
    }

    private sealed class TilemapCacheEntry(
        CpuRenderTarget target,
        TilemapStructureState structure,
        PaletteRevisionState[] palettes,
        TileSurfaceRevisionState[] surfaces,
        long tilemapRevision)
    {
        public CpuRenderTarget Target { get; } = target;
        public TilemapStructureState Structure { get; set; } = structure;
        public PaletteRevisionState[] Palettes { get; set; } = palettes;
        public TileSurfaceRevisionState[] Surfaces { get; set; } = surfaces;
        public long TilemapRevision { get; set; } = tilemapRevision;
    }
}
