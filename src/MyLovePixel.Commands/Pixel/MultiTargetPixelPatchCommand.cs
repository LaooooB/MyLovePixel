using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Commands.Pixel;

public sealed record SurfacePixelPatch(ResourceId SurfaceId, IReadOnlyList<PixelWrite> Writes);

public sealed class MultiTargetPixelPatchCommand : ICommand
{
    private readonly TargetPatch[] _targets;

    public MultiTargetPixelPatchCommand(IEnumerable<SurfacePixelPatch> patches, string name = "Multi-Target Pixel Patch")
    {
        ArgumentNullException.ThrowIfNull(patches);
        Name = string.IsNullOrWhiteSpace(name) ? "Multi-Target Pixel Patch" : name;

        _targets = patches
            .GroupBy(patch => patch.SurfaceId)
            .Select(group =>
            {
                var writes = group
                    .SelectMany(patch => patch.Writes ?? throw new ArgumentException("Patch writes cannot be null.", nameof(patches)))
                    .GroupBy(write => (write.X, write.Y))
                    .Select(coordinateGroup => coordinateGroup.Last())
                    .ToArray();
                return writes.Length == 0 ? null : new TargetPatch(group.Key, writes, CalculateBounds(writes));
            })
            .Where(target => target is not null)
            .Cast<TargetPatch>()
            .OrderBy(target => target.SurfaceId.Value)
            .ToArray();

        if (_targets.Length == 0)
            throw new ArgumentException("At least one non-empty surface patch is required.", nameof(patches));
    }

    public string Name { get; }

    public CommandApplication Apply(PixelDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var prepared = new PreparedTarget[_targets.Length];

        // Resolve every target and capture every before-value before the first mutation.
        // This gives all-or-nothing validation across multiple surfaces.
        for (var targetIndex = 0; targetIndex < _targets.Length; targetIndex++)
        {
            var target = _targets[targetIndex];
            var surface = document.Resources.GetSurface(target.SurfaceId);
            var before = new PixelWrite[target.Writes.Length];
            for (var writeIndex = 0; writeIndex < target.Writes.Length; writeIndex++)
            {
                var write = target.Writes[writeIndex];
                before[writeIndex] = new PixelWrite(write.X, write.Y, surface.GetPixel(write.X, write.Y));
            }
            prepared[targetIndex] = new PreparedTarget(target, surface, before);
        }

        var appliedCount = 0;
        try
        {
            foreach (var item in prepared)
            {
                item.Surface.SetPixels(item.Target.Writes);
                appliedCount++;
            }
        }
        catch
        {
            for (var index = appliedCount - 1; index >= 0; index--)
                prepared[index].Surface.SetPixels(prepared[index].Before);
            throw;
        }

        var undo = new Undo(prepared.Select(item => new TargetUndo(item.Target.SurfaceId, item.Before)).ToArray());
        var change = new DocumentChange(_targets.Select(target => new DirtySurfaceRegion(target.SurfaceId, target.DirtyRegion)).ToArray());
        return new CommandApplication(undo, change);
    }

    public DocumentChange Revert(PixelDocument document, IUndoToken undoToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (undoToken is not Undo undo) throw new ArgumentException("Undo token type mismatch.", nameof(undoToken));

        for (var index = undo.Targets.Length - 1; index >= 0; index--)
        {
            var item = undo.Targets[index];
            document.Resources.GetSurface(item.SurfaceId).SetPixels(item.Before);
        }

        return new DocumentChange(_targets.Select(target => new DirtySurfaceRegion(target.SurfaceId, target.DirtyRegion)).ToArray());
    }

    private static IntRect CalculateBounds(IReadOnlyList<PixelWrite> writes)
    {
        var minX = writes[0].X;
        var minY = writes[0].Y;
        var maxX = minX;
        var maxY = minY;
        for (var index = 1; index < writes.Count; index++)
        {
            minX = Math.Min(minX, writes[index].X);
            minY = Math.Min(minY, writes[index].Y);
            maxX = Math.Max(maxX, writes[index].X);
            maxY = Math.Max(maxY, writes[index].Y);
        }
        return new IntRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private sealed record TargetPatch(ResourceId SurfaceId, PixelWrite[] Writes, IntRect DirtyRegion);
    private sealed record PreparedTarget(TargetPatch Target, PixelSurface Surface, PixelWrite[] Before);
    private sealed record TargetUndo(ResourceId SurfaceId, PixelWrite[] Before);
    private sealed record Undo(TargetUndo[] Targets) : IUndoToken;
}
