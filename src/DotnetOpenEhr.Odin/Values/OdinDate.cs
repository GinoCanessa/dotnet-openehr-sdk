using DotnetOpenEhr.Foundation.Iso;

namespace DotnetOpenEhr.Odin.Values;

/// <summary>
/// ODIN date scalar (spec 7.1.6). Backed by an <see cref="IsoDate"/>.
/// ODIN reduced-accuracy patterns containing <c>?</c> characters
/// (e.g. <c>yyyy-MM-??</c>) are not representable here and are returned
/// by the parser as <see cref="OdinString"/> values.
/// </summary>
public sealed class OdinDate : OdinValue
{
    public OdinDate(IsoDate value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    public IsoDate Value { get; set; }

    public override OdinKind Kind => OdinKind.Date;
}
