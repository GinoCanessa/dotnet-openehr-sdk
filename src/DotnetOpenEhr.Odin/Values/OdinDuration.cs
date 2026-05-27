using DotnetOpenEhr.Foundation.Iso;

namespace DotnetOpenEhr.Odin.Values;

/// <summary>
/// ODIN duration scalar (spec 7.1.6). Backed by an <see cref="IsoDuration"/>.
/// </summary>
public sealed class OdinDuration : OdinValue
{
    public OdinDuration(IsoDuration value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    public IsoDuration Value { get; set; }

    public override OdinKind Kind => OdinKind.Duration;
}
