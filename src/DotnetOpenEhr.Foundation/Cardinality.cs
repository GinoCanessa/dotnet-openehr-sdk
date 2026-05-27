namespace DotnetOpenEhr.Foundation;

/// <summary>
/// openEHR <c>Cardinality</c> Foundation Type. Describes the cardinality
/// of a container attribute (e.g., a list, set, or bag) in terms of its
/// allowed occurrence count and structural properties.
/// </summary>
public sealed class Cardinality : IEquatable<Cardinality>
{
    public Cardinality(Interval<int> interval, bool isOrdered, bool isUnique)
    {
        ArgumentNullException.ThrowIfNull(interval);
        Interval = interval;
        IsOrdered = isOrdered;
        IsUnique = isUnique;
    }

    public Interval<int> Interval { get; }
    public bool IsOrdered { get; }
    public bool IsUnique { get; }

    public bool IsSequence => IsOrdered && IsUnique;
    public bool IsSet => !IsOrdered && IsUnique;
    public bool IsBag => !IsOrdered && !IsUnique;

    public bool Equals(Cardinality? other)
        => other is not null
        && IsOrdered == other.IsOrdered
        && IsUnique == other.IsUnique
        && Interval.Equals(other.Interval);

    public override bool Equals(object? obj) => Equals(obj as Cardinality);

    public override int GetHashCode() => HashCode.Combine(Interval, IsOrdered, IsUnique);

    public override string ToString()
        => $"{Interval}; ordered={IsOrdered}; unique={IsUnique}";
}
