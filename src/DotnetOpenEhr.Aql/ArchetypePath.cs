using System.Collections;
using System.Text;
using DotnetOpenEhr.Aql.Evaluation;
using DotnetOpenEhr.Aql.Paths;
using DotnetOpenEhr.Rm.Common;

namespace DotnetOpenEhr.Aql;

/// <summary>
/// Pre-compiled archetype path. Parse once via
/// <see cref="Parse(System.ReadOnlySpan{char})"/> /
/// <see cref="TryParse(System.ReadOnlySpan{char}, out DotnetOpenEhr.Aql.ArchetypePath?)"/>
/// and re-resolve against many <see cref="Pathable"/> roots without
/// re-tokenizing. For one-off resolution prefer the static
/// <see cref="ArchetypePathResolver"/> entry points.
/// </summary>
/// <remarks>
/// Trim- and Native-AOT-safe — no reflection, no
/// <c>Expression.Compile</c>, no runtime code generation. Resolution
/// shares the same RM attribute switch as <c>AqlEvaluator</c> via
/// <see cref="PathNavigator"/>.
/// </remarks>
public sealed class ArchetypePath
{
    private readonly ArchetypePathSegment[] _segments;
    private readonly string _canonical;

    private ArchetypePath(ArchetypePathSegment[] segments)
    {
        _segments = segments;
        _canonical = Canonicalize(segments);
    }

    /// <summary>
    /// Parse an archetype path. Throws
    /// <see cref="ArchetypePathParseException"/> on malformed input.
    /// </summary>
    public static ArchetypePath Parse(ReadOnlySpan<char> archetypePath)
    {
        ArchetypePathSegment[] segments = ArchetypePathParser.Parse(archetypePath);
        return new ArchetypePath(segments);
    }

    /// <summary>
    /// String overload of <see cref="Parse(System.ReadOnlySpan{char})"/>.
    /// Throws <see cref="ArgumentNullException"/> for a <c>null</c> input.
    /// </summary>
    public static ArchetypePath Parse(string archetypePath)
    {
        ArgumentNullException.ThrowIfNull(archetypePath);
        return Parse(archetypePath.AsSpan());
    }

    /// <summary>
    /// Try-parse an archetype path. Returns <c>false</c> and sets
    /// <paramref name="result"/> to <c>null</c> on malformed input;
    /// never throws.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> archetypePath, out ArchetypePath? result)
    {
        if (ArchetypePathParser.TryParse(archetypePath, out ArchetypePathSegment[]? segments, out _))
        {
            result = new ArchetypePath(segments!);
            return true;
        }
        result = null;
        return false;
    }

    /// <summary>
    /// String overload of <see cref="TryParse(System.ReadOnlySpan{char}, out DotnetOpenEhr.Aql.ArchetypePath?)"/>.
    /// A <c>null</c> input returns <c>false</c> instead of throwing.
    /// </summary>
    public static bool TryParse(string? archetypePath, out ArchetypePath? result)
    {
        if (archetypePath is null)
        {
            result = null;
            return false;
        }
        return TryParse(archetypePath.AsSpan(), out result);
    }

    /// <summary>
    /// Resolve to at most one value. Returns <c>null</c> when the path
    /// does not resolve; throws <see cref="InvalidOperationException"/>
    /// when the path resolves to more than one node (use
    /// <see cref="ResolveAll(Pathable)"/> instead).
    /// </summary>
    public object? Resolve(Pathable root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return ArchetypePathResolver.ResolveSingle(root, _segments, _canonical);
    }

    /// <summary>
    /// Typed overload of <see cref="Resolve(Pathable)"/>. Returns
    /// <c>default(T)</c> when the path does not resolve. A type
    /// mismatch throws <see cref="System.InvalidCastException"/>.
    /// </summary>
    public T? Resolve<T>(Pathable root)
    {
        object? value = Resolve(root);
        if (value is null) return default;
        return (T)value;
    }

    /// <summary>
    /// Resolve to all matches in RM-collection / enumeration order.
    /// </summary>
    public IEnumerable<object?> ResolveAll(Pathable root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return ArchetypePathResolver.ResolveAllCore(root, _segments);
    }

    /// <summary>
    /// Typed overload of <see cref="ResolveAll(Pathable)"/>. Null
    /// elements yield <c>default(T)</c>; type mismatch throws
    /// <see cref="System.InvalidCastException"/> on the first
    /// offending element.
    /// </summary>
    public IEnumerable<T?> ResolveAll<T>(Pathable root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return ArchetypePathResolver.ResolveAllTypedCore<T>(root, _segments);
    }

    /// <inheritdoc/>
    public override string ToString() => _canonical;

    private static string Canonicalize(ArchetypePathSegment[] segments)
    {
        if (segments.Length == 0) return "/";
        StringBuilder builder = new();
        foreach (ArchetypePathSegment segment in segments)
        {
            builder.Append('/');
            builder.Append(segment.AttributeName);
            if (segment.Predicate is null) continue;
            AppendPredicate(builder, segment.Predicate);
        }
        return builder.ToString();
    }

    private static void AppendPredicate(StringBuilder builder, ArchetypePathPredicate predicate)
    {
        builder.Append('[');
        if (predicate.NodeId is not null)
        {
            builder.Append(predicate.NodeId);
            if (predicate.Name is not null)
            {
                builder.Append(", ");
                AppendQuotedName(builder, predicate.Name);
            }
        }
        else if (predicate.Name is not null)
        {
            AppendQuotedName(builder, predicate.Name);
        }
        builder.Append(']');
    }

    private static void AppendQuotedName(StringBuilder builder, string name)
    {
        builder.Append('\'');
        foreach (char c in name)
        {
            if (c == '\\') { builder.Append('\\').Append('\\'); }
            else if (c == '\'') { builder.Append('\\').Append('\''); }
            else { builder.Append(c); }
        }
        builder.Append('\'');
    }
}
