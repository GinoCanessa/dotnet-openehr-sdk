namespace DotnetOpenEhr.Odin.Values;

/// <summary>
/// ODIN real / double scalar (spec 7.1.4). Presence of a decimal point
/// (and optional exponent) in the source flags a value as real.
/// </summary>
public sealed class OdinReal : OdinValue
{
    public OdinReal(double value)
    {
        Value = value;
    }

    public double Value { get; set; }

    public override OdinKind Kind => OdinKind.Real;
}
