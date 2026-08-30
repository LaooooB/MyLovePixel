using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;

namespace MyLovePixel.Effects;

public static class BuiltinEffectDescriptors
{
    public static EffectDescriptor Outline { get; } = new(
        "core.outline",
        "Outline",
        [
            new EffectParameterDescriptor(
                "radius",
                "Radius",
                EffectParameterKind.Integer,
                EffectValue.Integer(1),
                minimum: 1,
                maximum: 8),
            new EffectParameterDescriptor(
                "color",
                "Color",
                EffectParameterKind.Color,
                EffectValue.Color(new Rgba32(0, 0, 0, 255))),
        ]);

    public static EffectDescriptor Shadow { get; } = new(
        "core.shadow",
        "Shadow",
        [
            new EffectParameterDescriptor(
                "offset",
                "Offset",
                EffectParameterKind.Point,
                EffectValue.Point(new IntPoint(1, 1))),
            new EffectParameterDescriptor(
                "color",
                "Color",
                EffectParameterKind.Color,
                EffectValue.Color(new Rgba32(0, 0, 0, 128))),
        ]);

    public static EffectDescriptor PaletteMap { get; } = new(
        "core.palette-map",
        "Palette Map",
        [
            new EffectParameterDescriptor(
                "sourcePalette",
                "Source Palette",
                EffectParameterKind.PaletteReference,
                EffectValue.PaletteReference(new PaletteId(Guid.ParseExact("11111111111111111111111111111111", "N"))),
                animatable: false),
            new EffectParameterDescriptor(
                "targetPalette",
                "Target Palette",
                EffectParameterKind.PaletteReference,
                EffectValue.PaletteReference(new PaletteId(Guid.ParseExact("22222222222222222222222222222222", "N"))),
                animatable: false),
        ]);
}

public interface IEffectKernel
{
    string TypeId { get; }
    EffectImage Evaluate(
        EffectDescriptor descriptor,
        EffectInstanceSnapshot instance,
        EffectImage source,
        EffectEvaluationContext context);
}

public sealed class CpuEffectEvaluatorBackend : IEffectEvaluatorBackend
{
    private readonly Dictionary<string, IEffectKernel> _kernels = new(StringComparer.Ordinal);

    public string Id => "cpu-reference";
    public long Revision { get; private set; }

    public void Register(IEffectKernel kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        if (!_kernels.TryAdd(kernel.TypeId, kernel))
            throw new InvalidOperationException($"Effect kernel '{kernel.TypeId}' is already registered.");
        Revision = checked(Revision + 1);
    }

    public bool CanEvaluate(string effectTypeId) => _kernels.ContainsKey(effectTypeId);

    public EffectImage Evaluate(
        EffectDescriptor descriptor,
        EffectInstanceSnapshot instance,
        EffectImage source,
        EffectEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        if (!_kernels.TryGetValue(instance.TypeId, out var kernel))
            throw new KeyNotFoundException($"No CPU kernel is registered for effect '{instance.TypeId}'.");
        if (!string.Equals(kernel.TypeId, descriptor.TypeId, StringComparison.Ordinal))
            throw new InvalidOperationException("Effect kernel and descriptor type IDs do not match.");
        return kernel.Evaluate(descriptor, instance, source, context);
    }

    public static CpuEffectEvaluatorBackend CreateDefault()
    {
        var backend = new CpuEffectEvaluatorBackend();
        backend.Register(new OutlineEffectKernel());
        backend.Register(new ShadowEffectKernel());
        backend.Register(new PaletteMapEffectKernel());
        return backend;
    }
}

public sealed class OutlineEffectKernel : IEffectKernel
{
    public string TypeId => BuiltinEffectDescriptors.Outline.TypeId;

    public EffectImage Evaluate(
        EffectDescriptor descriptor,
        EffectInstanceSnapshot instance,
        EffectImage source,
        EffectEvaluationContext context)
    {
        var radius = Resolve(instance, descriptor, context.FrameId, "radius").IntegerValue;
        var color = Resolve(instance, descriptor, context.FrameId, "color").ColorValue;
        var r = checked((int)radius);
        var width = checked(source.Size.Width + r * 2);
        var height = checked(source.Size.Height + r * 2);
        var output = new Rgba32[checked(width * height)];

        for (var sy = 0; sy < source.Size.Height; sy++)
        for (var sx = 0; sx < source.Size.Width; sx++)
        {
            var sourcePixel = source.GetPixel(sx, sy);
            if (sourcePixel.A == 0) continue;
            for (var dy = -r; dy <= r; dy++)
            for (var dx = -r; dx <= r; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var ox = sx + r + dx;
                var oy = sy + r + dy;
                if ((uint)ox >= (uint)width || (uint)oy >= (uint)height) continue;
                var tinted = new Rgba32(color.R, color.G, color.B, ScaleByte(color.A, sourcePixel.A));
                var index = checked(oy * width + ox);
                output[index] = SourceOver(output[index], tinted);
            }
        }

        for (var sy = 0; sy < source.Size.Height; sy++)
        for (var sx = 0; sx < source.Size.Width; sx++)
        {
            var sourcePixel = source.GetPixel(sx, sy);
            var index = checked((sy + r) * width + sx + r);
            output[index] = SourceOver(output[index], sourcePixel);
        }

        return new EffectImage(
            new IntSize(width, height),
            new IntPoint(source.Origin.X - r, source.Origin.Y - r),
            output);
    }

    private static EffectValue Resolve(EffectInstanceSnapshot instance, EffectDescriptor descriptor, FrameId frameId, string key)
    {
        instance.TryResolveParameter(key, frameId, descriptor, out var value);
        return value;
    }

    internal static byte ScaleByte(byte left, byte right) =>
        checked((byte)((left * right + 127) / 255));

    internal static Rgba32 SourceOver(Rgba32 destination, Rgba32 source)
    {
        if (source.A == 0) return destination;
        if (source.A == 255) return source;
        var sourceAlpha = source.A;
        var inverse = 255 - sourceAlpha;
        var outAlpha = sourceAlpha + (destination.A * inverse + 127) / 255;
        if (outAlpha == 0) return Rgba32.Transparent;

        static byte Channel(byte src, byte srcAlpha, byte dst, byte dstAlpha, int inverse, int outAlpha)
        {
            var numerator = src * srcAlpha * 255 + dst * dstAlpha * inverse;
            var denominator = outAlpha * 255;
            return checked((byte)((numerator + denominator / 2) / denominator));
        }

        return new Rgba32(
            Channel(source.R, sourceAlpha, destination.R, destination.A, inverse, outAlpha),
            Channel(source.G, sourceAlpha, destination.G, destination.A, inverse, outAlpha),
            Channel(source.B, sourceAlpha, destination.B, destination.A, inverse, outAlpha),
            checked((byte)outAlpha));
    }
}

public sealed class ShadowEffectKernel : IEffectKernel
{
    public string TypeId => BuiltinEffectDescriptors.Shadow.TypeId;

    public EffectImage Evaluate(
        EffectDescriptor descriptor,
        EffectInstanceSnapshot instance,
        EffectImage source,
        EffectEvaluationContext context)
    {
        instance.TryResolveParameter("offset", context.FrameId, descriptor, out var offsetValue);
        instance.TryResolveParameter("color", context.FrameId, descriptor, out var colorValue);
        var offset = offsetValue.PointValue;
        var color = colorValue.ColorValue;
        var minX = Math.Min(0, offset.X);
        var minY = Math.Min(0, offset.Y);
        var maxX = Math.Max(source.Size.Width, checked(offset.X + source.Size.Width));
        var maxY = Math.Max(source.Size.Height, checked(offset.Y + source.Size.Height));
        var width = checked(maxX - minX);
        var height = checked(maxY - minY);
        var pixels = new Rgba32[checked(width * height)];
        var sourceOffsetX = -minX;
        var sourceOffsetY = -minY;
        var shadowOffsetX = checked(offset.X - minX);
        var shadowOffsetY = checked(offset.Y - minY);

        for (var y = 0; y < source.Size.Height; y++)
        for (var x = 0; x < source.Size.Width; x++)
        {
            var sourcePixel = source.GetPixel(x, y);
            if (sourcePixel.A == 0) continue;
            var shadow = new Rgba32(
                color.R,
                color.G,
                color.B,
                OutlineEffectKernel.ScaleByte(color.A, sourcePixel.A));
            var index = checked((y + shadowOffsetY) * width + x + shadowOffsetX);
            pixels[index] = OutlineEffectKernel.SourceOver(pixels[index], shadow);
        }

        for (var y = 0; y < source.Size.Height; y++)
        for (var x = 0; x < source.Size.Width; x++)
        {
            var index = checked((y + sourceOffsetY) * width + x + sourceOffsetX);
            pixels[index] = OutlineEffectKernel.SourceOver(pixels[index], source.GetPixel(x, y));
        }

        return new EffectImage(
            new IntSize(width, height),
            new IntPoint(source.Origin.X + minX, source.Origin.Y + minY),
            pixels);
    }
}

public sealed class PaletteMapEffectKernel : IEffectKernel
{
    public string TypeId => BuiltinEffectDescriptors.PaletteMap.TypeId;

    public EffectImage Evaluate(
        EffectDescriptor descriptor,
        EffectInstanceSnapshot instance,
        EffectImage source,
        EffectEvaluationContext context)
    {
        instance.TryResolveParameter("sourcePalette", context.FrameId, descriptor, out var sourcePaletteValue);
        instance.TryResolveParameter("targetPalette", context.FrameId, descriptor, out var targetPaletteValue);
        if (!context.Snapshot.Palettes.TryGetValue(sourcePaletteValue.PaletteIdValue, out var sourcePalette) ||
            !context.Snapshot.Palettes.TryGetValue(targetPaletteValue.PaletteIdValue, out var targetPalette))
            return source;
        if (targetPalette.Count < sourcePalette.Count)
            return source;

        var map = new Dictionary<Rgba32, Rgba32>();
        for (var index = 0; index < sourcePalette.Count; index++)
        {
            var byteIndex = checked((byte)index);
            map[sourcePalette.GetColor(byteIndex)] = targetPalette.GetColor(byteIndex);
        }

        var pixels = source.ClonePixels();
        for (var index = 0; index < pixels.Length; index++)
        {
            if (map.TryGetValue(pixels[index], out var replacement))
                pixels[index] = replacement;
        }
        return new EffectImage(source.Size, source.Origin, pixels);
    }
}
