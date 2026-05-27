using DotnetOpenEhr.Foundation.Iso;

namespace DotnetOpenEhr.Odin.Values;

/// <summary>
/// ODIN date/time scalar (spec 7.1.6). Backed by an <see cref="IsoDateTime"/>.
/// </summary>
public sealed class OdinDateTime : OdinValue
{
    public OdinDateTime(IsoDateTime value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    public IsoDateTime Value { get; set; }

    public override OdinKind Kind => OdinKind.DateTime;
}
