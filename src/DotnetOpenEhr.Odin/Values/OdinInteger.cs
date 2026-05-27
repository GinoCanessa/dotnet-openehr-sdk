namespace DotnetOpenEhr.Odin.Values;

/// <summary>
/// ODIN integer scalar (spec 7.1.3). Stored as <see cref="long"/>; any
/// trailing exponent (e.g. <c>29e6</c>) is folded into the integer value.
/// </summary>
public sealed class OdinInteger : OdinValue
{
    public OdinInteger(long value)
    {
        Value = value;
    }

    public long Value { get; set; }

    public override OdinKind Kind => OdinKind.Integer;
}
