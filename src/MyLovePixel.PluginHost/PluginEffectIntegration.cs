using MyLovePixel.Core.Document;
using MyLovePixel.Core.Effects;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Primitives;
using MyLovePixel.Effects;
using MyLovePixel.PluginSdk;

namespace MyLovePixel.PluginHost;

public static class PluginEffectIntegration
{
    public static EffectEngine CreateEffectEngine(this PluginHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var registry = EffectRegistry.CreateDefault();
        foreach (var effect in host.Effects.Values)
            registry.Register(ToCoreDescriptor(effect.Descriptor));
        return new EffectEngine(registry, new CompositeBackend(host, CpuEffectEvaluatorBackend.CreateDefault()));
    }

    public static EffectDescriptor ToCoreDescriptor(PluginEffectDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var parameters = descriptor.Parameters.Select(parameter => new EffectParameterDescriptor(
            parameter.Key,
            parameter.DisplayName,
            ToCoreKind(parameter.Kind),
            ToCoreValue(parameter.DefaultValue, parameter.Kind),
            parameter.Animatable,
            parameter.Minimum,
            parameter.Maximum));
        return new EffectDescriptor(descriptor.TypeId, descriptor.DisplayName, parameters);
    }

    private static EffectParameterKind ToCoreKind(PluginEffectParameterKind kind) => kind switch
    {
        PluginEffectParameterKind.Integer => EffectParameterKind.Integer,
        PluginEffectParameterKind.Number => EffectParameterKind.Number,
        PluginEffectParameterKind.Boolean => EffectParameterKind.Boolean,
        PluginEffectParameterKind.Color => EffectParameterKind.Color,
        PluginEffectParameterKind.Point => EffectParameterKind.Point,
        PluginEffectParameterKind.PaletteReference => EffectParameterKind.PaletteReference,
        PluginEffectParameterKind.Text => EffectParameterKind.Text,
        _ => throw new NotSupportedException($"Plugin effect parameter kind '{kind}' is not supported."),
    };

    private static EffectValue ToCoreValue(PluginValue value, PluginEffectParameterKind expectedKind)
    {
        ArgumentNullException.ThrowIfNull(value);
        return expectedKind switch
        {
            PluginEffectParameterKind.Integer when value.Kind == PluginValueKind.Integer => EffectValue.Integer(value.IntegerValue),
            PluginEffectParameterKind.Number when value.Kind == PluginValueKind.Number => EffectValue.Number(value.NumberValue),
            PluginEffectParameterKind.Boolean when value.Kind == PluginValueKind.Boolean => EffectValue.Boolean(value.BooleanValue),
            PluginEffectParameterKind.Color when value.Kind == PluginValueKind.Color => EffectValue.Color(ToCore(value.ColorValue)),
            PluginEffectParameterKind.Point when value.Kind == PluginValueKind.Point => EffectValue.Point(ToCore(value.PointValue)),
            PluginEffectParameterKind.PaletteReference when value.Kind == PluginValueKind.Identifier => EffectValue.PaletteReference(new PaletteId(value.IdentifierValue)),
            PluginEffectParameterKind.Text when value.Kind == PluginValueKind.Text => EffectValue.Text(value.TextValue ?? string.Empty),
            _ => throw new ArgumentException($"Plugin value kind '{value.Kind}' does not match effect parameter kind '{expectedKind}'.", nameof(value)),
        };
    }

    private static PluginValue ToPluginValue(EffectValue value) => value.Kind switch
    {
        EffectParameterKind.Integer => PluginValue.Integer(value.IntegerValue),
        EffectParameterKind.Number => PluginValue.Number(value.NumberValue),
        EffectParameterKind.Boolean => PluginValue.Boolean(value.BooleanValue),
        EffectParameterKind.Color => PluginValue.Color(ToPlugin(value.ColorValue)),
        EffectParameterKind.Point => PluginValue.Point(ToPlugin(value.PointValue)),
        EffectParameterKind.PaletteReference => PluginValue.Identifier(value.PaletteIdValue.Value),
        EffectParameterKind.Text => PluginValue.Text(value.TextValue ?? string.Empty),
        _ => throw new NotSupportedException($"Core effect parameter kind '{value.Kind}' is not supported by the plugin adapter."),
    };

    private static PluginImage ToPluginImage(EffectImage image)
    {
        var rgba = new byte[checked(image.Size.Width * image.Size.Height * 4)];
        for (var y = 0; y < image.Size.Height; y++)
        for (var x = 0; x < image.Size.Width; x++)
        {
            var pixel = image.GetPixel(x, y);
            var offset = checked(((y * image.Size.Width) + x) * 4);
            rgba[offset] = pixel.R;
            rgba[offset + 1] = pixel.G;
            rgba[offset + 2] = pixel.B;
            rgba[offset + 3] = pixel.A;
        }
        return new PluginImage(
            new PluginIntSize(image.Size.Width, image.Size.Height),
            rgba,
            new PluginIntPoint(image.Origin.X, image.Origin.Y));
    }

    private static EffectImage ToCoreImage(PluginImage image)
    {
        var pixels = new Rgba32[checked(image.Size.Width * image.Size.Height)];
        var rgba = image.Rgba.Span;
        for (var index = 0; index < pixels.Length; index++)
        {
            var offset = index * 4;
            pixels[index] = new Rgba32(rgba[offset], rgba[offset + 1], rgba[offset + 2], rgba[offset + 3]);
        }
        return new EffectImage(
            new IntSize(image.Size.Width, image.Size.Height),
            new IntPoint(image.Origin.X, image.Origin.Y),
            pixels);
    }

    private static Rgba32 ToCore(PluginRgba32 value) => new(value.R, value.G, value.B, value.A);
    private static IntPoint ToCore(PluginIntPoint value) => new(value.X, value.Y);
    private static PluginRgba32 ToPlugin(Rgba32 value) => new(value.R, value.G, value.B, value.A);
    private static PluginIntPoint ToPlugin(IntPoint value) => new(value.X, value.Y);

    private sealed class CompositeBackend(PluginHost host, IEffectEvaluatorBackend builtins) : IEffectEvaluatorBackend
    {
        public string Id => "plugin-composite";
        public long Revision => checked(builtins.Revision + host.Effects.Revision);

        public bool CanEvaluate(string effectTypeId) => builtins.CanEvaluate(effectTypeId) || host.Effects.TryGet(effectTypeId, out _);

        public EffectImage Evaluate(
            EffectDescriptor descriptor,
            EffectInstanceSnapshot instance,
            EffectImage source,
            EffectEvaluationContext context)
        {
            if (builtins.CanEvaluate(descriptor.TypeId))
                return builtins.Evaluate(descriptor, instance, source, context);
            if (!host.Effects.TryGet(descriptor.TypeId, out var plugin))
                throw new KeyNotFoundException($"Plugin effect '{descriptor.TypeId}' is not registered.");
            var owner = host.Effects.GetOwner(descriptor.TypeId);
            try
            {
                var parameters = new Dictionary<string, PluginValue>(StringComparer.Ordinal);
                foreach (var pair in descriptor.Parameters)
                {
                    instance.TryResolveParameter(pair.Key, context.FrameId, descriptor, out var value);
                    parameters.Add(pair.Key, ToPluginValue(value));
                }
                var request = new PluginEffectRequest(
                    context.Snapshot.Id.Value,
                    context.FrameId.Value,
                    context.Cel.Id.Value,
                    ToPluginImage(source),
                    parameters);
                return ToCoreImage(plugin.Evaluate(request));
            }
            catch (Exception ex)
            {
                host.Record(new PluginDiagnostic(
                    PluginDiagnosticCode.ExecutionFailed,
                    owner,
                    $"Plugin effect '{descriptor.TypeId}' failed.",
                    descriptor.TypeId,
                    ex));
                throw new InvalidOperationException($"Plugin effect '{descriptor.TypeId}' failed.", ex);
            }
        }
    }
}
