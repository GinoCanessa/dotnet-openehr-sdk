using DotnetOpenEhr.Foundation.Iso;

namespace DotnetOpenEhr.Odin.Values;

/// <summary>
/// ODIN time scalar (spec 7.1.6). Backed by an <see cref="IsoTime"/>.
/// </summary>
public sealed class OdinTime : OdinValue
{
    public OdinTime(IsoTime value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    public IsoTime Value { get; set; }

    public override OdinKind Kind => OdinKind.Time;
}
