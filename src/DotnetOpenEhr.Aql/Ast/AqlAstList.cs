using System.Collections;

namespace DotnetOpenEhr.Aql.Ast;

/// <summary>
/// Read-only list with sequence-based <see cref="Equals(object?)"/> and
/// <see cref="GetHashCode"/>, used so that the auto-generated equality
/// on AST records produces structural deep-equality across collection
/// properties (the default <see cref="List{T}"/> equality is reference
/// equality, which would make every parse non-equal to itself).
/// </summary>
public sealed class AqlAstList<T> : IReadOnlyList<T>, IEquatable<AqlAstList<T>>
{
    private readonly List<T> _items;

    public AqlAstList(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = [.. items];
    }

    public static AqlAstList<T> Empty { get; } = new(Array.Empty<T>());

    public T this[int index] => _items[index];

    public int Count => _items.Count;

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(AqlAstList<T>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (_items.Count != other._items.Count) return false;
        EqualityComparer<T> cmp = EqualityComparer<T>.Default;
        for (int i = 0; i < _items.Count; i++)
        {
            if (!cmp.Equals(_items[i], other._items[i])) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as AqlAstList<T>);

    public override int GetHashCode()
    {
        HashCode hc = new();
        foreach (T item in _items)
        {
            hc.Add(item);
        }
        return hc.ToHashCode();
    }
}
