namespace DotnetOpenEhr.Foundation;

/// <summary>
/// openEHR <c>Interval&lt;T&gt;</c> Foundation Type.
/// Represents a contiguous range of comparable values; either bound may be
/// open or closed, and either bound may be unbounded (i.e., -∞ or +∞).
/// </summary>
/// <remarks>
/// Construct via <see cref="Bounded"/>, <see cref="LowerOpen"/>,
/// <see cref="UpperOpen"/>, <see cref="Unbounded"/>, or the four "Including"
/// / "Excluding" factories. All instances are immutable; equality is by
/// shape.
/// </remarks>
public sealed class Interval<T> : IEquatable<Interval<T>>
    where T : IComparable<T>
{
    private readonly T _lower;
    private readonly T _upper;

    private Interval(
        T lower,
        T upper,
        bool hasLower,
        bool hasUpper,
        bool lowerIncluded,
        bool upperIncluded)
    {
        _lower = lower;
        _upper = upper;
        HasLower = hasLower;
        HasUpper = hasUpper;
        LowerIncluded = hasLower && lowerIncluded;
        UpperIncluded = hasUpper && upperIncluded;

        if (hasLower && hasUpper && lower.CompareTo(upper) > 0)
        {
            throw new ArgumentException(
                $"Interval lower bound ({lower}) is greater than upper bound ({upper}).",
                nameof(lower));
        }
    }

    public bool HasLower { get; }
    public bool HasUpper { get; }
    public bool LowerIncluded { get; }
    public bool UpperIncluded { get; }

    public T Lower => _lower;
    public T Upper => _upper;

    public static Interval<T> Bounded(T lower, T upper)
        => new Interval<T>(lower, upper, true, true, true, true);

    public static Interval<T> LowerOpen(T lower, T upper)
        => new Interval<T>(lower, upper, true, true, false, true);

    public static Interval<T> UpperOpen(T lower, T upper)
        => new Interval<T>(lower, upper, true, true, true, false);

    public static Interval<T> Open(T lower, T upper)
        => new Interval<T>(lower, upper, true, true, false, false);

    public static Interval<T> AtLeast(T lower)
        => new Interval<T>(lower, default!, true, false, true, false);

    public static Interval<T> GreaterThan(T lower)
        => new Interval<T>(lower, default!, true, false, false, false);

    public static Interval<T> AtMost(T upper)
        => new Interval<T>(default!, upper, false, true, false, true);

    public static Interval<T> LessThan(T upper)
        => new Interval<T>(default!, upper, false, true, false, false);

    public static Interval<T> Unbounded()
        => new Interval<T>(default!, default!, false, false, false, false);

    public bool Contains(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (HasLower)
        {
            int compareLower = value.CompareTo(_lower);
            if (LowerIncluded ? compareLower < 0 : compareLower <= 0)
            {
                return false;
            }
        }

        if (HasUpper)
        {
            int compareUpper = value.CompareTo(_upper);
            if (UpperIncluded ? compareUpper > 0 : compareUpper >= 0)
            {
                return false;
            }
        }

        return true;
    }

    public bool Intersects(Interval<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (HasUpper && other.HasLower)
        {
            int cmp = _upper.CompareTo(other._lower);
            if (cmp < 0)
            {
                return false;
            }
            if (cmp == 0 && !(UpperIncluded && other.LowerIncluded))
            {
                return false;
            }
        }

        if (HasLower && other.HasUpper)
        {
            int cmp = _lower.CompareTo(other._upper);
            if (cmp > 0)
            {
                return false;
            }
            if (cmp == 0 && !(LowerIncluded && other.UpperIncluded))
            {
                return false;
            }
        }

        return true;
    }

    public bool Equals(Interval<T>? other)
    {
        if (other is null) return false;
        if (HasLower != other.HasLower) return false;
        if (HasUpper != other.HasUpper) return false;
        if (LowerIncluded != other.LowerIncluded) return false;
        if (UpperIncluded != other.UpperIncluded) return false;
        if (HasLower && _lower.CompareTo(other._lower) != 0) return false;
        if (HasUpper && _upper.CompareTo(other._upper) != 0) return false;
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as Interval<T>);

    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(HasLower);
        hash.Add(HasUpper);
        hash.Add(LowerIncluded);
        hash.Add(UpperIncluded);
        if (HasLower) hash.Add(_lower);
        if (HasUpper) hash.Add(_upper);
        return hash.ToHashCode();
    }

    public override string ToString()
    {
        string lowerBracket = LowerIncluded ? "[" : "(";
        string upperBracket = UpperIncluded ? "]" : ")";
        string lowerText = HasLower ? _lower!.ToString() ?? string.Empty : "-∞";
        string upperText = HasUpper ? _upper!.ToString() ?? string.Empty : "+∞";
        return $"{lowerBracket}{lowerText}, {upperText}{upperBracket}";
    }
}
