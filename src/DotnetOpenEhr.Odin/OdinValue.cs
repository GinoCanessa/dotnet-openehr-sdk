using System.Diagnostics.CodeAnalysis;
using DotnetOpenEhr.Odin.Values;

namespace DotnetOpenEhr.Odin;

/// <summary>
/// Root of the ODIN value union. ODIN values are mutable to support
/// faithful round-trip parsing and emission.
/// </summary>
public abstract class OdinValue
{
    private static readonly OdinNull s_null = new();

    /// <summary>
    /// Discriminator for the runtime kind of this value.
    /// </summary>
    public abstract OdinKind Kind { get; }

    /// <summary>
    /// Optional ODIN type marker (the <c>(TYPE_NAME)</c> annotation that
    /// precedes the opening <c>&lt;</c> of an object or list).
    /// </summary>
    public string? TypeMarker { get; set; }

    /// <summary>
    /// The shared <see cref="OdinNull"/> singleton.
    /// </summary>
    public static OdinValue Null => s_null;

    public bool IsNull => Kind == OdinKind.Null;
    public bool IsString => Kind == OdinKind.String;
    public bool IsInteger => Kind == OdinKind.Integer;
    public bool IsReal => Kind == OdinKind.Real;
    public bool IsBoolean => Kind == OdinKind.Boolean;
    public bool IsDate => Kind == OdinKind.Date;
    public bool IsTime => Kind == OdinKind.Time;
    public bool IsDateTime => Kind == OdinKind.DateTime;
    public bool IsDuration => Kind == OdinKind.Duration;
    public bool IsTerminologyCode => Kind == OdinKind.TerminologyCode;
    public bool IsInterval => Kind == OdinKind.Interval;
    public bool IsList => Kind == OdinKind.List;
    public bool IsHash => Kind == OdinKind.Hash;
    public bool IsObject => Kind == OdinKind.Object;

    public OdinString AsString() => Cast<OdinString>();
    public OdinInteger AsInteger() => Cast<OdinInteger>();
    public OdinReal AsReal() => Cast<OdinReal>();
    public OdinBoolean AsBoolean() => Cast<OdinBoolean>();
    public OdinDate AsDate() => Cast<OdinDate>();
    public OdinTime AsTime() => Cast<OdinTime>();
    public OdinDateTime AsDateTime() => Cast<OdinDateTime>();
    public OdinDuration AsDuration() => Cast<OdinDuration>();
    public OdinTerminologyCode AsTerminologyCode() => Cast<OdinTerminologyCode>();
    public OdinInterval AsInterval() => Cast<OdinInterval>();
    public OdinList AsList() => Cast<OdinList>();
    public OdinHash AsHash() => Cast<OdinHash>();
    public OdinObject AsObject() => Cast<OdinObject>();

    private T Cast<T>() where T : OdinValue
    {
        if (this is T typed)
        {
            return typed;
        }
        throw new InvalidOperationException(
            $"ODIN value of kind {Kind} cannot be accessed as {typeof(T).Name}.");
    }

    /// <summary>
    /// Structural equality on ODIN trees: kinds, type markers, scalar
    /// values, and recursive children must all match. Lists compare by
    /// position; objects and hashes compare by key with order-insensitive
    /// equality (so different insertion orders still match).
    /// </summary>
    public static bool StructurallyEqual(OdinValue? left, OdinValue? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left.Kind != right.Kind) return false;
        if (!string.Equals(left.TypeMarker, right.TypeMarker, StringComparison.Ordinal)) return false;

        switch (left.Kind)
        {
            case OdinKind.Null:
                return true;
            case OdinKind.String:
                return string.Equals(((OdinString)left).Value, ((OdinString)right).Value, StringComparison.Ordinal);
            case OdinKind.Integer:
                return ((OdinInteger)left).Value == ((OdinInteger)right).Value;
            case OdinKind.Real:
                return ((OdinReal)left).Value.Equals(((OdinReal)right).Value);
            case OdinKind.Boolean:
                return ((OdinBoolean)left).Value == ((OdinBoolean)right).Value;
            case OdinKind.Date:
                return ((OdinDate)left).Value.Equals(((OdinDate)right).Value);
            case OdinKind.Time:
                return ((OdinTime)left).Value.Equals(((OdinTime)right).Value);
            case OdinKind.DateTime:
                return ((OdinDateTime)left).Value.Equals(((OdinDateTime)right).Value);
            case OdinKind.Duration:
                return ((OdinDuration)left).Value.Equals(((OdinDuration)right).Value);
            case OdinKind.TerminologyCode:
                return ((OdinTerminologyCode)left).Value.Equals(((OdinTerminologyCode)right).Value);
            case OdinKind.Interval:
            {
                OdinInterval lv = (OdinInterval)left;
                OdinInterval rv = (OdinInterval)right;
                return lv.LowerIncluded == rv.LowerIncluded
                    && lv.UpperIncluded == rv.UpperIncluded
                    && StructurallyEqual(lv.Lower, rv.Lower)
                    && StructurallyEqual(lv.Upper, rv.Upper);
            }
            case OdinKind.List:
            {
                IList<OdinValue> ll = ((OdinList)left).Items;
                IList<OdinValue> rl = ((OdinList)right).Items;
                if (ll.Count != rl.Count) return false;
                for (int i = 0; i < ll.Count; i++)
                {
                    if (!StructurallyEqual(ll[i], rl[i])) return false;
                }
                return true;
            }
            case OdinKind.Hash:
            {
                IDictionary<string, OdinValue> le = ((OdinHash)left).Entries;
                IDictionary<string, OdinValue> re = ((OdinHash)right).Entries;
                if (le.Count != re.Count) return false;
                foreach (KeyValuePair<string, OdinValue> kvp in le)
                {
                    if (!re.TryGetValue(kvp.Key, out OdinValue? rv)) return false;
                    if (!StructurallyEqual(kvp.Value, rv)) return false;
                }
                return true;
            }
            case OdinKind.Object:
            {
                IDictionary<string, OdinValue> la = ((OdinObject)left).Attributes;
                IDictionary<string, OdinValue> ra = ((OdinObject)right).Attributes;
                if (la.Count != ra.Count) return false;
                foreach (KeyValuePair<string, OdinValue> kvp in la)
                {
                    if (!ra.TryGetValue(kvp.Key, out OdinValue? rv)) return false;
                    if (!StructurallyEqual(kvp.Value, rv)) return false;
                }
                return true;
            }
            default:
                return false;
        }
    }

    /// <summary>
    /// Convenience: serialize via <see cref="OdinWriter"/> using default
    /// options. Cheap and useful for diagnostics.
    /// </summary>
    public override string ToString() => OdinWriter.Write(this);
}

/// <summary>
/// Common interface for keyed ODIN containers (<see cref="OdinObject"/> and
/// <see cref="OdinHash"/>). Both expose <see cref="TryGet"/>.
/// </summary>
public interface IOdinKeyed
{
    bool TryGet(string key, [NotNullWhen(true)] out OdinValue? value);
}
