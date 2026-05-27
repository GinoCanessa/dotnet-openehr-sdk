namespace DotnetOpenEhr.Odin.Values;

/// <summary>
/// ODIN interval scalar (spec 7.2). Pipe-delimited intervals
/// (<c>|N..M|</c>, <c>|&gt;=N|</c>, <c>|N ±M|</c>, etc.) of any ordered
/// primitive type. <see cref="Lower"/> and <see cref="Upper"/> are null
/// for unbounded sides.
/// </summary>
public sealed class OdinInterval : OdinValue
{
    public OdinInterval(
        OdinValue? lower,
        bool lowerIncluded,
        OdinValue? upper,
        bool upperIncluded)
    {
        Lower = lower;
        LowerIncluded = lowerIncluded;
        Upper = upper;
        UpperIncluded = upperIncluded;
    }

    public OdinValue? Lower { get; set; }
    public bool LowerIncluded { get; set; }
    public OdinValue? Upper { get; set; }
    public bool UpperIncluded { get; set; }

    public bool IsPointInterval =>
        Lower is not null && Upper is not null
        && LowerIncluded && UpperIncluded
        && OdinValue.StructurallyEqual(Lower, Upper);

    public override OdinKind Kind => OdinKind.Interval;
}
