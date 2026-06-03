using System.Collections;
using DotnetOpenEhr.Aql.Evaluation;
using DotnetOpenEhr.Aql.Paths;
using DotnetOpenEhr.Rm.Common;

namespace DotnetOpenEhr.Aql;

/// <summary>
/// Public, allocation-light tree-walking resolver for openEHR
/// archetype paths against a <see cref="Pathable"/> root. Mirrors the
/// AQL evaluator's path-navigation semantics (shared via the internal
/// <see cref="PathNavigator"/>) but exposes a single-root, scalar
/// (<see cref="Resolve(Pathable, System.ReadOnlySpan{char})"/>) /
/// multi-value
/// (<see cref="ResolveAll(Pathable, System.ReadOnlySpan{char})"/>)
/// surface for mapping pipelines, validators, and sample code.
/// </summary>
/// <remarks>
/// <para>
/// For repeated resolutions of the same path against many roots,
/// pre-compile once via
/// <see cref="ArchetypePath.Parse(System.ReadOnlySpan{char})"/> and
/// call the instance methods to amortize parse cost.
/// </para>
/// <para>
/// Trim- and Native-AOT-safe: no reflection, no
/// <c>Expression.Compile</c>, no runtime code generation.
/// </para>
/// </remarks>
public static class ArchetypePathResolver
{
    // ----------------------------------------------------------------
    // Resolve — at most one match.
    // ----------------------------------------------------------------

    /// <summary>
    /// Resolve <paramref name="archetypePath"/> to at most one value.
    /// Returns <c>null</c> when the path does not resolve; throws
    /// <see cref="InvalidOperationException"/> when it matches more
    /// than one node (use <see cref="ResolveAll(Pathable, System.ReadOnlySpan{char})"/>
    /// instead).
    /// </summary>
    public static object? Resolve(Pathable root, ReadOnlySpan<char> archetypePath)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArchetypePathSegment[] segments = ArchetypePathParser.Parse(archetypePath);
        // Only allocate the path-as-string for the rare multi-match
        // error message; build it lazily there.
        return ResolveSingleLazyMessage(root, segments, archetypePath);
    }

    /// <summary>
    /// String overload of <see cref="Resolve(Pathable, System.ReadOnlySpan{char})"/>.
    /// </summary>
    public static object? Resolve(Pathable root, string archetypePath)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(archetypePath);
        ArchetypePathSegment[] segments = ArchetypePathParser.Parse(archetypePath);
        return ResolveSingle(root, segments, archetypePath);
    }

    /// <summary>
    /// Typed overload of <see cref="Resolve(Pathable, System.ReadOnlySpan{char})"/>.
    /// Returns <c>default(T)</c> when the path does not resolve. A
    /// type mismatch throws <see cref="System.InvalidCastException"/>.
    /// </summary>
    public static T? Resolve<T>(Pathable root, ReadOnlySpan<char> archetypePath)
    {
        object? value = Resolve(root, archetypePath);
        if (value is null) return default;
        return (T)value;
    }

    /// <summary>
    /// String overload of
    /// <see cref="Resolve{T}(Pathable, System.ReadOnlySpan{char})"/>.
    /// </summary>
    public static T? Resolve<T>(Pathable root, string archetypePath)
    {
        object? value = Resolve(root, archetypePath);
        if (value is null) return default;
        return (T)value;
    }

    // ----------------------------------------------------------------
    // ResolveAll — every match in RM-collection order.
    // ----------------------------------------------------------------

    /// <summary>
    /// Resolve <paramref name="archetypePath"/> to every matching node
    /// in RM-collection / enumeration order. Returns an empty
    /// enumerable when the path does not resolve.
    /// </summary>
    public static IEnumerable<object?> ResolveAll(Pathable root, ReadOnlySpan<char> archetypePath)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArchetypePathSegment[] segments = ArchetypePathParser.Parse(archetypePath);
        return ResolveAllCore(root, segments);
    }

    /// <summary>
    /// String overload of <see cref="ResolveAll(Pathable, System.ReadOnlySpan{char})"/>.
    /// </summary>
    public static IEnumerable<object?> ResolveAll(Pathable root, string archetypePath)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(archetypePath);
        ArchetypePathSegment[] segments = ArchetypePathParser.Parse(archetypePath);
        return ResolveAllCore(root, segments);
    }

    /// <summary>
    /// Typed overload of <see cref="ResolveAll(Pathable, System.ReadOnlySpan{char})"/>.
    /// Null elements yield <c>default(T)</c>; type mismatch throws
    /// <see cref="System.InvalidCastException"/> on the first
    /// offending element.
    /// </summary>
    public static IEnumerable<T?> ResolveAll<T>(Pathable root, ReadOnlySpan<char> archetypePath)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArchetypePathSegment[] segments = ArchetypePathParser.Parse(archetypePath);
        return ResolveAllTypedCore<T>(root, segments);
    }

    /// <summary>
    /// String overload of
    /// <see cref="ResolveAll{T}(Pathable, System.ReadOnlySpan{char})"/>.
    /// </summary>
    public static IEnumerable<T?> ResolveAll<T>(Pathable root, string archetypePath)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(archetypePath);
        ArchetypePathSegment[] segments = ArchetypePathParser.Parse(archetypePath);
        return ResolveAllTypedCore<T>(root, segments);
    }

    // ----------------------------------------------------------------
    // Internal cores — also reused by ArchetypePath instance methods.
    // ----------------------------------------------------------------

    internal static object? ResolveSingle(Pathable root, ArchetypePathSegment[] segments, string pathForError)
    {
        object? terminal = Walk(root, segments);
        return MaterializeSingle(terminal, pathForError);
    }

    internal static IEnumerable<object?> ResolveAllCore(Pathable root, ArchetypePathSegment[] segments)
    {
        object? terminal = Walk(root, segments);
        return EnumerateTerminal(terminal);
    }

    internal static IEnumerable<T?> ResolveAllTypedCore<T>(Pathable root, ArchetypePathSegment[] segments)
    {
        foreach (object? item in ResolveAllCore(root, segments))
        {
            if (item is null) yield return default;
            else yield return (T)item;
        }
    }

    private static object? ResolveSingleLazyMessage(
        Pathable root,
        ArchetypePathSegment[] segments,
        ReadOnlySpan<char> archetypePath)
    {
        object? terminal = Walk(root, segments);
        // Fast paths first to avoid ever materializing the span as a
        // string for the success cases.
        if (terminal is null) return null;
        if (terminal is string || terminal is IDictionary) return terminal;
        if (terminal is IEnumerable seq)
        {
            object? first = null;
            int count = 0;
            foreach (object? item in seq)
            {
                count++;
                if (count == 1) first = item;
            }
            if (count == 0) return null;
            if (count == 1) return first;
            throw new InvalidOperationException(
                $"Archetype path '{archetypePath.ToString()}' resolved to {count} matches; use ResolveAll instead.");
        }
        return terminal;
    }

    private static object? MaterializeSingle(object? terminal, string pathForError)
    {
        if (terminal is null) return null;
        if (terminal is string || terminal is IDictionary) return terminal;
        if (terminal is IEnumerable seq)
        {
            object? first = null;
            int count = 0;
            foreach (object? item in seq)
            {
                count++;
                if (count == 1) first = item;
            }
            if (count == 0) return null;
            if (count == 1) return first;
            throw new InvalidOperationException(
                $"Archetype path '{pathForError}' resolved to {count} matches; use ResolveAll instead.");
        }
        return terminal;
    }

    private static IEnumerable<object?> EnumerateTerminal(object? terminal)
    {
        if (terminal is null) yield break;
        if (terminal is string || terminal is IDictionary)
        {
            yield return terminal;
            yield break;
        }
        if (terminal is IEnumerable seq)
        {
            foreach (object? item in seq) yield return item;
            yield break;
        }
        yield return terminal;
    }

    private static object? Walk(Pathable root, ArchetypePathSegment[] segments)
    {
        // A '/'-only path is parsed to an empty segment array and
        // resolves to the root itself (single match, no walk).
        object? current = root;
        foreach (ArchetypePathSegment segment in segments)
        {
            if (current is null) return null;
            current = PathNavigator.GetAttribute(current, segment.AttributeName);
            if (segment.Predicate is not null && current is not null)
            {
                current = PathNavigator.FilterByPredicate(
                    current,
                    segment.Predicate.NodeId,
                    segment.Predicate.Name);
            }
        }
        return current;
    }
}
