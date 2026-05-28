using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DotnetOpenEhr.Aql.Ast;
using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes;
using DotnetOpenEhr.Rm.DataTypes.Basic;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Encapsulated;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.DataTypes.Uri;
using DotnetOpenEhr.Rm.Support;
using RmEvaluation = DotnetOpenEhr.Rm.Composition.Evaluation;
using RmAction = DotnetOpenEhr.Rm.Composition.Action;
using RmEvent = DotnetOpenEhr.Rm.DataStructures.Event;

namespace DotnetOpenEhr.Aql.Evaluation;

/// <summary>
/// Tree-walking interpreter that evaluates a parsed <see cref="AqlQuery"/>
/// over an in-memory sequence of <see cref="Composition"/> instances.
/// Implements FROM / CONTAINS source binding, WHERE filtering with
/// three-valued logic, SELECT projection, DISTINCT row de-duplication,
/// ORDER BY (multi-column ASC/DESC, stable), and LIMIT / OFFSET.
/// A streaming overload — <see cref="EvaluateAsync(AqlQuery, IAsyncEnumerable{Composition}, CancellationToken)"/>
/// — yields rows as the source streams in.
/// </summary>
/// <remarks>
/// The evaluator is hand-written (no Reflection.Emit, no Expression.Compile,
/// no runtime code generation) so it is safe under trimming and Native
/// AOT. Path navigation uses a closed switch over the supported RM types
/// instead of reflection.
/// </remarks>
public sealed class AqlEvaluator
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyParameters
        = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Evaluate <paramref name="query"/> against <paramref name="source"/> with no parameters.</summary>
    public IReadOnlyList<object?[]> Evaluate(
        AqlQuery query,
        IEnumerable<Composition> source,
        CancellationToken ct = default)
        => Evaluate(query, source, EmptyParameters, ct);

    /// <summary>Evaluate <paramref name="query"/> against <paramref name="source"/> using the supplied parameter values.</summary>
    public IReadOnlyList<object?[]> Evaluate(
        AqlQuery query,
        IEnumerable<Composition> source,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(parameters);

        List<object?[]> rows = [];
        List<object?[]>? sortKeys = query.OrderBy is not null ? [] : null;
        HashSet<RowKey>? seen = query.Select.Distinct ? [] : null;

        foreach (Composition comp in source)
        {
            ct.ThrowIfCancellationRequested();
            ProjectComposition(query, comp, parameters, seen, rows, sortKeys, ct);
        }

        if (query.OrderBy is not null && sortKeys is not null)
        {
            SortRowsByKeys(rows, sortKeys, query.OrderBy);
        }
        return ApplyOffsetLimit(rows, query.Offset, query.Limit);
    }

    /// <summary>
    /// Async streaming evaluation: yields rows as the source streams in.
    /// </summary>
    /// <remarks>
    /// When <see cref="AqlQuery.OrderBy"/> is set the implementation must
    /// buffer the entire projected result set before yielding any row
    /// (a sort needs to see every key). Without an ORDER BY clause, rows
    /// are streamed as the source produces them; <c>LIMIT</c> stops
    /// requesting rows after the cap is reached and <c>OFFSET</c> skips
    /// rows without materialising them.
    /// </remarks>
    public IAsyncEnumerable<object?[]> EvaluateAsync(
        AqlQuery query,
        IAsyncEnumerable<Composition> source,
        CancellationToken ct = default)
        => EvaluateAsync(query, source, EmptyParameters, ct);

    /// <summary>
    /// Async streaming evaluation with parameter bindings.
    /// </summary>
    /// <remarks>
    /// See the parameterless overload for ORDER BY buffering semantics.
    /// </remarks>
    public IAsyncEnumerable<object?[]> EvaluateAsync(
        AqlQuery query,
        IAsyncEnumerable<Composition> source,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(parameters);
        return EvaluateAsyncCore(query, source, parameters, ct);
    }

    private async IAsyncEnumerable<object?[]> EvaluateAsyncCore(
        AqlQuery query,
        IAsyncEnumerable<Composition> source,
        IReadOnlyDictionary<string, object?> parameters,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        int offset = query.Offset ?? 0;
        int limit = query.Limit ?? int.MaxValue;
        if (limit <= 0) yield break;

        HashSet<RowKey>? seen = query.Select.Distinct ? [] : null;

        if (query.OrderBy is not null)
        {
            // ORDER BY forces a full buffer before any yield: we can't
            // know the first sorted row until every key is in hand.
            List<object?[]> buffered = [];
            List<object?[]> sortKeys = [];
            await foreach (Composition comp in source.WithCancellation(ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                ProjectComposition(query, comp, parameters, seen, buffered, sortKeys, ct);
            }
            SortRowsByKeys(buffered, sortKeys, query.OrderBy);
            IReadOnlyList<object?[]> sliced = ApplyOffsetLimit(buffered, query.Offset, query.Limit);
            foreach (object?[] row in sliced)
            {
                ct.ThrowIfCancellationRequested();
                yield return row;
            }
            yield break;
        }

        int skipped = 0;
        int yielded = 0;
        List<object?[]> perComp = [];
        await foreach (Composition comp in source.WithCancellation(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            perComp.Clear();
            ProjectComposition(query, comp, parameters, seen, perComp, sortKeys: null, ct);
            foreach (object?[] row in perComp)
            {
                ct.ThrowIfCancellationRequested();
                if (skipped < offset)
                {
                    skipped++;
                    continue;
                }
                yield return row;
                yielded++;
                if (yielded >= limit) yield break;
            }
        }
    }

    // -----------------------------------------------------------------
    // Shared projection: emits zero or more rows for a single
    // Composition, optionally also recording the matching ORDER BY
    // sort-key tuple in a parallel list.
    // -----------------------------------------------------------------

    private void ProjectComposition(
        AqlQuery query,
        Composition comp,
        IReadOnlyDictionary<string, object?> parameters,
        HashSet<RowKey>? seen,
        List<object?[]> rows,
        List<object?[]>? sortKeys,
        CancellationToken ct)
    {
        foreach (Binding binding in ExpandFrom(query.From, comp, parameters))
        {
            ct.ThrowIfCancellationRequested();
            if (query.Where is not null)
            {
                object? result = EvalExpr(query.Where.Predicate, binding, parameters);
                if (result is not bool b || !b)
                {
                    continue;
                }
            }
            object?[] row = new object?[query.Select.Columns.Count];
            for (int i = 0; i < query.Select.Columns.Count; i++)
            {
                row[i] = EvalExpr(query.Select.Columns[i].Expr, binding, parameters);
            }
            if (seen is not null)
            {
                if (!seen.Add(new RowKey(row)))
                {
                    continue;
                }
            }
            rows.Add(row);
            if (sortKeys is not null && query.OrderBy is not null)
            {
                object?[] keys = new object?[query.OrderBy.Items.Count];
                for (int i = 0; i < query.OrderBy.Items.Count; i++)
                {
                    keys[i] = EvalExpr(query.OrderBy.Items[i].Expr, binding, parameters);
                }
                sortKeys.Add(keys);
            }
        }
    }

    // -----------------------------------------------------------------
    // ORDER BY / OFFSET / LIMIT post-processing.
    // -----------------------------------------------------------------

    private static void SortRowsByKeys(
        List<object?[]> rows,
        List<object?[]> sortKeys,
        OrderByClause orderBy)
    {
        int n = rows.Count;
        if (n < 2) return;
        int[] indices = new int[n];
        for (int i = 0; i < n; i++) indices[i] = i;

        // Index-based sort with explicit tie-break on original
        // position. Array.Sort is not documented as stable so we
        // enforce stability ourselves.
        Comparison<int> cmp = (a, b) =>
        {
            for (int k = 0; k < orderBy.Items.Count; k++)
            {
                int c = CompareOrderKeys(sortKeys[a][k], sortKeys[b][k], orderBy.Items[k].Direction);
                if (c != 0) return c;
            }
            return a.CompareTo(b);
        };
        Array.Sort(indices, cmp);

        object?[][] reordered = new object?[n][];
        for (int i = 0; i < n; i++) reordered[i] = rows[indices[i]];
        rows.Clear();
        rows.AddRange(reordered);
    }

    // Null-handling convention: nulls sort *last* on ASC and *first*
    // on DESC. (Equivalent to treating null as +infinity.)
    private static int CompareOrderKeys(object? a, object? b, AqlOrderDirection dir)
    {
        if (a is null && b is null) return 0;
        if (a is null) return dir == AqlOrderDirection.Ascending ? 1 : -1;
        if (b is null) return dir == AqlOrderDirection.Ascending ? -1 : 1;
        int? cmp = CompareValues(a, b);
        int v = cmp ?? 0;
        return dir == AqlOrderDirection.Ascending ? v : -v;
    }

    private static IReadOnlyList<object?[]> ApplyOffsetLimit(
        List<object?[]> rows,
        int? offsetN,
        int? limitN)
    {
        int offset = offsetN ?? 0;
        int limit = limitN ?? int.MaxValue;
        if (offset <= 0 && limit >= rows.Count) return rows;
        if (offset >= rows.Count || limit <= 0) return [];
        int take = Math.Min(limit, rows.Count - offset);
        List<object?[]> result = new(take);
        for (int i = 0; i < take; i++) result.Add(rows[offset + i]);
        return result;
    }

    // -----------------------------------------------------------------
    // Binding context
    // -----------------------------------------------------------------

    private sealed class Binding
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

        public Binding() { }

        private Binding(Dictionary<string, object?> values)
        {
            _values = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase);
        }

        public Binding With(string? alias, object? value)
        {
            Binding copy = new(_values);
            if (!string.IsNullOrEmpty(alias))
            {
                copy._values[alias] = value;
            }
            return copy;
        }

        public bool TryGet(string name, out object? value) => _values.TryGetValue(name, out value);
    }

    // -----------------------------------------------------------------
    // FROM / CONTAINS expansion
    // -----------------------------------------------------------------

    private IEnumerable<Binding> ExpandFrom(
        FromClause from,
        Composition comp,
        IReadOnlyDictionary<string, object?> parameters)
    {
        if (from.Sources.Count == 0)
        {
            yield return new Binding();
            yield break;
        }

        // Multiple top-level sources joined by ',' produce a cross
        // product. AQL queries we care about have a single source.
        IEnumerable<Binding> current = ExpandClass(from.Sources[0], comp, new Binding(), comp, parameters);
        for (int i = 1; i < from.Sources.Count; i++)
        {
            ClassExpression next = from.Sources[i];
            current = current.SelectMany(b => ExpandClass(next, comp, b, comp, parameters));
        }
        foreach (Binding b in current)
        {
            yield return b;
        }
    }

    private IEnumerable<Binding> ExpandClass(
        ClassExpression cls,
        Composition rootComp,
        Binding parent,
        object? scope,
        IReadOnlyDictionary<string, object?> parameters)
    {
        IEnumerable<object> candidates = FindCandidates(cls.RmTypeName, scope, rootComp);
        foreach (object candidate in candidates)
        {
            if (cls.Predicate is not null && !PredicateMatches(cls.Predicate, candidate, cls.Alias, parent, parameters))
            {
                continue;
            }
            Binding bound = parent.With(cls.Alias, candidate);
            if (cls.Contains.Count == 0)
            {
                yield return bound;
                continue;
            }
            // Multiple sibling CONTAINS within a class expression are
            // implicitly AND'd: every child must yield at least one
            // binding for the candidate to survive.
            foreach (Binding combined in CombineContainsSiblings(cls.Contains, 0, bound, candidate, rootComp, parameters))
            {
                yield return combined;
            }
        }
    }

    private IEnumerable<Binding> CombineContainsSiblings(
        IReadOnlyList<ContainsExpression> siblings,
        int index,
        Binding b,
        object scope,
        Composition rootComp,
        IReadOnlyDictionary<string, object?> parameters)
    {
        if (index >= siblings.Count)
        {
            yield return b;
            yield break;
        }
        foreach (Binding next in ExpandContains(siblings[index], b, scope, rootComp, parameters))
        {
            foreach (Binding tail in CombineContainsSiblings(siblings, index + 1, next, scope, rootComp, parameters))
            {
                yield return tail;
            }
        }
    }

    private IEnumerable<Binding> ExpandContains(
        ContainsExpression ce,
        Binding b,
        object scope,
        Composition rootComp,
        IReadOnlyDictionary<string, object?> parameters)
    {
        switch (ce)
        {
            case ContainsClassExpression cce:
                foreach (Binding x in ExpandClass(cce.Class, rootComp, b, scope, parameters))
                {
                    yield return x;
                }
                break;
            case ContainsAndExpression and:
                foreach (Binding left in ExpandContains(and.Left, b, scope, rootComp, parameters))
                {
                    foreach (Binding right in ExpandContains(and.Right, left, scope, rootComp, parameters))
                    {
                        yield return right;
                    }
                }
                break;
            case ContainsOrExpression or:
                foreach (Binding left in ExpandContains(or.Left, b, scope, rootComp, parameters))
                {
                    yield return left;
                }
                foreach (Binding right in ExpandContains(or.Right, b, scope, rootComp, parameters))
                {
                    yield return right;
                }
                break;
            case ContainsNotExpression not:
                bool any = false;
                foreach (Binding _ in ExpandContains(not.Inner, b, scope, rootComp, parameters))
                {
                    any = true;
                    break;
                }
                if (!any) yield return b;
                break;
            default:
                throw new AqlEvaluationException($"Unsupported CONTAINS expression: {ce.GetType().Name}");
        }
    }

    private static bool PredicateMatches(
        Predicate predicate,
        object candidate,
        string? alias,
        Binding parent,
        IReadOnlyDictionary<string, object?> parameters)
    {
        // Fast path: the most common predicate is a bare archetype HRID
        // literal — e.g. [openEHR-EHR-OBSERVATION.blood_pressure.v2].
        if (predicate.Body is LiteralExpression lit)
        {
            string? expected = lit.Value as string;
            if (expected is null) return false;
            string actualNodeId = candidate is Locatable loc ? loc.ArchetypeNodeId : string.Empty;
            return string.Equals(actualNodeId, expected, StringComparison.Ordinal);
        }
        // Arbitrary boolean predicate body: evaluate with the
        // candidate temporarily bound to the class alias.
        Binding withAlias = parent.With(alias, candidate);
        object? result = EvalExpr(predicate.Body, withAlias, parameters);
        return result is bool b && b;
    }

    // -----------------------------------------------------------------
    // Candidate discovery: walk an RM tree and yield all instances of
    // the requested RM type name (case-insensitive). EHR / COMPOSITION
    // are handled specially so the top of the chain binds correctly.
    // -----------------------------------------------------------------

    private static IEnumerable<object> FindCandidates(string rmTypeName, object? scope, Composition rootComp)
    {
        if (rmTypeName.Equals("EHR", StringComparison.OrdinalIgnoreCase))
        {
            // The SDK does not model an EHR root object. Bind the
            // EHR alias to the source Composition so paths starting
            // with `e/ehr_id` etc. degrade gracefully to null.
            yield return rootComp;
            yield break;
        }
        if (rmTypeName.Equals("COMPOSITION", StringComparison.OrdinalIgnoreCase))
        {
            if (scope is Composition c) yield return c;
            else yield return rootComp;
            yield break;
        }

        Type? targetType = ResolveRmType(rmTypeName);
        foreach (object node in EnumerateLocatables(scope ?? rootComp))
        {
            if (targetType is not null)
            {
                if (targetType.IsInstanceOfType(node)) yield return node;
            }
            else
            {
                // Unknown type name: match by the runtime _type
                // discriminator inferred from the C# type name.
                string runtime = RmTypeDiscriminator(node);
                if (string.Equals(runtime, rmTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return node;
                }
            }
        }
    }

    private static Type? ResolveRmType(string rmTypeName)
        => rmTypeName.ToUpperInvariant() switch
        {
            "COMPOSITION" => typeof(Composition),
            "SECTION" => typeof(Section),
            "OBSERVATION" => typeof(Observation),
            "EVALUATION" => typeof(RmEvaluation),
            "INSTRUCTION" => typeof(Instruction),
            "ACTION" => typeof(RmAction),
            "ADMIN_ENTRY" => typeof(AdminEntry),
            "ENTRY" => typeof(Entry),
            "CARE_ENTRY" => typeof(CareEntry),
            "CONTENT_ITEM" => typeof(ContentItem),
            "ACTIVITY" => typeof(Activity),
            "CLUSTER" => typeof(Cluster),
            "ELEMENT" => typeof(Element),
            "ITEM" => typeof(Item),
            "ITEM_TREE" => typeof(ItemTree),
            "ITEM_LIST" => typeof(ItemList),
            "ITEM_SINGLE" => typeof(ItemSingle),
            "ITEM_TABLE" => typeof(ItemTable),
            "ITEM_STRUCTURE" => typeof(ItemStructure),
            "HISTORY" => typeof(History),
            "POINT_EVENT" => typeof(PointEvent),
            "INTERVAL_EVENT" => typeof(IntervalEvent),
            "EVENT" => typeof(RmEvent),
            "LOCATABLE" => typeof(Locatable),
            _ => null,
        };

    private static string RmTypeDiscriminator(object node) => node switch
    {
        Composition => "COMPOSITION",
        Section => "SECTION",
        Observation => "OBSERVATION",
        RmEvaluation => "EVALUATION",
        Instruction => "INSTRUCTION",
        RmAction => "ACTION",
        AdminEntry => "ADMIN_ENTRY",
        Activity => "ACTIVITY",
        Cluster => "CLUSTER",
        Element => "ELEMENT",
        ItemTree => "ITEM_TREE",
        ItemList => "ITEM_LIST",
        ItemSingle => "ITEM_SINGLE",
        ItemTable => "ITEM_TABLE",
        History => "HISTORY",
        PointEvent => "POINT_EVENT",
        IntervalEvent => "INTERVAL_EVENT",
        _ => node.GetType().Name,
    };

    private static IEnumerable<object> EnumerateLocatables(object root)
    {
        Stack<object> stack = new();
        stack.Push(root);
        HashSet<object> visited = new(ReferenceEqualityComparer.Instance);
        while (stack.Count > 0)
        {
            object node = stack.Pop();
            if (!visited.Add(node)) continue;
            yield return node;

            // Push children for all known container shapes.
            switch (node)
            {
                case Composition c:
                    if (c.Context is not null) stack.Push(c.Context);
                    PushList(stack, c.Content);
                    break;
                case Section s:
                    PushList(stack, s.Items);
                    break;
                case Observation o:
                    if (o.Data is not null) stack.Push(o.Data);
                    if (o.State is not null) stack.Push(o.State);
                    if (o.Protocol is not null) stack.Push(o.Protocol);
                    break;
                case RmEvaluation ev:
                    if (ev.Data is not null) stack.Push(ev.Data);
                    if (ev.Protocol is not null) stack.Push(ev.Protocol);
                    break;
                case Instruction ins:
                    PushList(stack, ins.Activities);
                    if (ins.Protocol is not null) stack.Push(ins.Protocol);
                    break;
                case RmAction act:
                    if (act.Description is not null) stack.Push(act.Description);
                    if (act.Protocol is not null) stack.Push(act.Protocol);
                    break;
                case AdminEntry ae:
                    if (ae.Data is not null) stack.Push(ae.Data);
                    break;
                case Activity a:
                    if (a.Description is not null) stack.Push(a.Description);
                    break;
                case History h:
                    PushList(stack, h.Events);
                    if (h.Summary is not null) stack.Push(h.Summary);
                    break;
                case RmEvent e:
                    if (e.Data is not null) stack.Push(e.Data);
                    if (e.State is not null) stack.Push(e.State);
                    break;
                case ItemTree it:
                    PushList(stack, it.Items);
                    break;
                case ItemList il:
                    PushList(stack, il.Items);
                    break;
                case ItemSingle isi:
                    stack.Push(isi.Item);
                    break;
                case ItemTable itb:
                    PushList(stack, itb.Rows);
                    break;
                case Cluster cl:
                    PushList(stack, cl.Items);
                    break;
                case EventContext ec:
                    if (ec.OtherContext is not null) stack.Push(ec.OtherContext);
                    break;
                // Element is a leaf for containment purposes.
            }
        }
    }

    private static void PushList<T>(Stack<object> stack, IEnumerable<T>? items)
        where T : class
    {
        if (items is null) return;
        foreach (T item in items)
        {
            if (item is not null) stack.Push(item);
        }
    }

    // -----------------------------------------------------------------
    // Expression evaluation
    // -----------------------------------------------------------------

    private static object? EvalExpr(
        Expression expr,
        Binding binding,
        IReadOnlyDictionary<string, object?> parameters)
    {
        switch (expr)
        {
            case LiteralExpression lit:
                return EvalLiteral(lit, parameters);

            case IdentifierExpression id:
                if (binding.TryGet(id.Name, out object? v)) return v;
                return null;

            case PathExpression path:
                return EvalPath(path, binding, parameters);

            case BinaryExpression bin:
                return EvalBinary(bin, binding, parameters);

            case UnaryExpression un:
                return EvalUnary(un, binding, parameters);

            case ExistsExpression ex:
                return EvalExists(ex.Operand, binding, parameters);

            case MatchesExpression m:
                return EvalMatches(m, binding, parameters);

            case FunctionCallExpression fc:
                return EvalFunction(fc, binding, parameters);

            default:
                throw new AqlEvaluationException($"Unsupported expression type: {expr.GetType().Name}");
        }
    }

    private static object? EvalLiteral(LiteralExpression lit, IReadOnlyDictionary<string, object?> parameters)
    {
        if (lit.Kind == AqlLiteralKind.Placeholder)
        {
            string name = (lit.Value as string) ?? string.Empty;
            if (!parameters.TryGetValue(name, out object? v))
            {
                throw new AqlEvaluationException($"Missing parameter binding: ${name}");
            }
            return v;
        }
        return lit.Value;
    }

    private static object? EvalPath(
        PathExpression path,
        Binding binding,
        IReadOnlyDictionary<string, object?> parameters)
    {
        object? current = EvalExpr(path.Root, binding, parameters);
        if (path.Steps.Count == 0) return current;

        int startIdx = 0;
        // The parser emits a synthetic first step when the path's root
        // identifier carries a bracket predicate (e.g. `c[at0001]`).
        // Detect that case so we apply the predicate to the alias
        // binding instead of trying to look up an attribute named
        // after the alias.
        if (path.Root is IdentifierExpression rootId
            && path.Steps.Count > 0
            && string.Equals(path.Steps[0].AttributeName, rootId.Name, StringComparison.OrdinalIgnoreCase)
            && path.Steps[0].NodeIdPredicate is not null)
        {
            current = FilterByNodeId(current, path.Steps[0].NodeIdPredicate!);
            startIdx = 1;
        }

        for (int i = startIdx; i < path.Steps.Count; i++)
        {
            if (current is null) return null;
            PathStep step = path.Steps[i];
            current = GetAttribute(current, step.AttributeName);
            if (step.NodeIdPredicate is not null && current is not null)
            {
                current = FilterByNodeId(current, step.NodeIdPredicate);
            }
        }
        return Unwrap(current);
    }

    private static object? Unwrap(object? value)
    {
        // A path that resolved to a single-element collection is most
        // naturally consumed as that element; callers that want the
        // whole collection (e.g. count()) should use the function on
        // the path directly without unwrapping.
        if (value is IList<object?> list && list.Count == 1)
        {
            return list[0];
        }
        return value;
    }

    private static object? FilterByNodeId(object? value, string nodeId)
    {
        if (value is null) return null;
        if (value is IEnumerable seq and not string)
        {
            List<object?> filtered = [];
            foreach (object? item in seq)
            {
                if (item is Locatable loc
                    && string.Equals(loc.ArchetypeNodeId, nodeId, StringComparison.Ordinal))
                {
                    filtered.Add(item);
                }
            }
            return filtered;
        }
        if (value is Locatable single
            && string.Equals(single.ArchetypeNodeId, nodeId, StringComparison.Ordinal))
        {
            return single;
        }
        return null;
    }

    private static object? GetAttribute(object? value, string name)
    {
        if (value is null) return null;
        if (value is string) return null;
        if (value is IEnumerable seq and not string and not IDictionary)
        {
            List<object?> results = [];
            foreach (object? item in seq)
            {
                object? sub = GetAttribute(item, name);
                if (sub is null) continue;
                if (sub is IEnumerable subSeq and not string)
                {
                    foreach (object? x in subSeq) results.Add(x);
                }
                else
                {
                    results.Add(sub);
                }
            }
            return results;
        }
        return GetSingleAttribute(value, name);
    }

    private static object? GetSingleAttribute(object value, string name)
    {
        string n = name.ToLowerInvariant();
        // Two-step lookup: try the RM-specific switch first so the
        // canonical openEHR attribute names (snake_case) work, then
        // fall back to a couple of common Pascal-cased aliases for
        // ergonomics in expression strings.
        object? result = GetCanonicalAttribute(value, n);
        if (result is not null) return result;
        return GetCanonicalAttribute(value, name);
    }

    private static object? GetCanonicalAttribute(object value, string name) => value switch
    {
        Composition c => name switch
        {
            "content" => c.Content,
            "context" => c.Context,
            "name" => c.Name,
            "uid" => c.Uid,
            "archetype_node_id" => c.ArchetypeNodeId,
            "archetype_details" => c.ArchetypeDetails,
            "language" => c.Language,
            "territory" => c.Territory,
            "category" => c.Category,
            "composer" => c.Composer,
            "links" => c.Links,
            "feeder_audit" => c.FeederAudit,
            _ => null,
        },
        EventContext ec => name switch
        {
            "start_time" => ec.StartTime,
            "end_time" => ec.EndTime,
            "location" => ec.Location,
            "setting" => ec.Setting,
            "other_context" => ec.OtherContext,
            "health_care_facility" => ec.HealthCareFacility,
            "participations" => ec.Participations,
            _ => null,
        },
        Section s => name switch
        {
            "items" => s.Items,
            "name" => s.Name,
            "uid" => s.Uid,
            "archetype_node_id" => s.ArchetypeNodeId,
            "archetype_details" => s.ArchetypeDetails,
            "links" => s.Links,
            _ => null,
        },
        Observation o => name switch
        {
            "data" => o.Data,
            "state" => o.State,
            "protocol" => o.Protocol,
            "subject" => o.Subject,
            "encoding" => o.Encoding,
            "language" => o.Language,
            "other_participations" => o.OtherParticipations,
            "workflow_id" => o.WorkflowId,
            "guideline_id" => o.GuidelineId,
            "name" => o.Name,
            "uid" => o.Uid,
            "archetype_node_id" => o.ArchetypeNodeId,
            "archetype_details" => o.ArchetypeDetails,
            _ => null,
        },
        RmEvaluation ev => name switch
        {
            "data" => ev.Data,
            "protocol" => ev.Protocol,
            "subject" => ev.Subject,
            "encoding" => ev.Encoding,
            "language" => ev.Language,
            "name" => ev.Name,
            "uid" => ev.Uid,
            "archetype_node_id" => ev.ArchetypeNodeId,
            _ => null,
        },
        Instruction ins => name switch
        {
            "activities" => ins.Activities,
            "narrative" => ins.Narrative,
            "expiry_time" => ins.ExpiryTime,
            "protocol" => ins.Protocol,
            "subject" => ins.Subject,
            "encoding" => ins.Encoding,
            "language" => ins.Language,
            "name" => ins.Name,
            "uid" => ins.Uid,
            "archetype_node_id" => ins.ArchetypeNodeId,
            _ => null,
        },
        RmAction act => name switch
        {
            "time" => act.Time,
            "description" => act.Description,
            "ism_transition" => act.IsmTransition,
            "instruction_details" => act.InstructionDetails,
            "protocol" => act.Protocol,
            "subject" => act.Subject,
            "encoding" => act.Encoding,
            "language" => act.Language,
            "name" => act.Name,
            "uid" => act.Uid,
            "archetype_node_id" => act.ArchetypeNodeId,
            _ => null,
        },
        AdminEntry ae => name switch
        {
            "data" => ae.Data,
            "subject" => ae.Subject,
            "encoding" => ae.Encoding,
            "language" => ae.Language,
            "name" => ae.Name,
            "uid" => ae.Uid,
            "archetype_node_id" => ae.ArchetypeNodeId,
            _ => null,
        },
        Activity a => name switch
        {
            "description" => a.Description,
            "timing" => a.Timing,
            "action_archetype_id" => a.ActionArchetypeId,
            "name" => a.Name,
            "archetype_node_id" => a.ArchetypeNodeId,
            "uid" => a.Uid,
            _ => null,
        },
        History h => name switch
        {
            "origin" => h.Origin,
            "events" => h.Events,
            "period" => h.Period,
            "duration" => h.Duration,
            "summary" => h.Summary,
            "name" => h.Name,
            "archetype_node_id" => h.ArchetypeNodeId,
            _ => null,
        },
        IntervalEvent iev => name switch
        {
            "time" => iev.Time,
            "data" => iev.Data,
            "state" => iev.State,
            "width" => iev.Width,
            "sample_count" => iev.SampleCount,
            "math_function" => iev.MathFunction,
            "name" => iev.Name,
            "archetype_node_id" => iev.ArchetypeNodeId,
            _ => null,
        },
        RmEvent e => name switch
        {
            "time" => e.Time,
            "data" => e.Data,
            "state" => e.State,
            "name" => e.Name,
            "archetype_node_id" => e.ArchetypeNodeId,
            _ => null,
        },
        ItemTree it => name switch
        {
            "items" => it.Items,
            "name" => it.Name,
            "archetype_node_id" => it.ArchetypeNodeId,
            _ => null,
        },
        ItemList il => name switch
        {
            "items" => il.Items,
            "name" => il.Name,
            "archetype_node_id" => il.ArchetypeNodeId,
            _ => null,
        },
        ItemSingle iss => name switch
        {
            "item" => iss.Item,
            "name" => iss.Name,
            "archetype_node_id" => iss.ArchetypeNodeId,
            _ => null,
        },
        ItemTable itb => name switch
        {
            "rows" => itb.Rows,
            "name" => itb.Name,
            "archetype_node_id" => itb.ArchetypeNodeId,
            _ => null,
        },
        Cluster cl => name switch
        {
            "items" => cl.Items,
            "name" => cl.Name,
            "archetype_node_id" => cl.ArchetypeNodeId,
            "uid" => cl.Uid,
            _ => null,
        },
        Element el => name switch
        {
            "value" => el.Value,
            "null_flavour" => el.NullFlavour,
            "null_reason" => el.NullReason,
            "name" => el.Name,
            "archetype_node_id" => el.ArchetypeNodeId,
            _ => null,
        },
        DvCodedText dct => name switch
        {
            "value" => dct.Value,
            "defining_code" => dct.DefiningCode,
            "mappings" => dct.Mappings,
            "language" => dct.Language,
            "encoding" => dct.Encoding,
            "formatting" => dct.Formatting,
            "hyperlink" => dct.Hyperlink,
            _ => null,
        },
        DvText dt => name switch
        {
            "value" => dt.Value,
            "mappings" => dt.Mappings,
            "language" => dt.Language,
            "encoding" => dt.Encoding,
            "formatting" => dt.Formatting,
            "hyperlink" => dt.Hyperlink,
            _ => null,
        },
        DvQuantity dq => name switch
        {
            "magnitude" => dq.Magnitude,
            "units" => dq.Units,
            "precision" => dq.Precision,
            "units_system" => dq.UnitsSystem,
            "units_display_name" => dq.UnitsDisplayName,
            "normal_range" => dq.NormalRange,
            "normal_status" => dq.NormalStatus,
            "accuracy" => dq.Accuracy,
            "accuracy_is_percent" => dq.AccuracyIsPercent,
            "magnitude_status" => dq.MagnitudeStatus,
            _ => null,
        },
        DvCount dc => name switch
        {
            "magnitude" => (object?)dc.Magnitude,
            _ => null,
        },
        DvProportion dp => name switch
        {
            "numerator" => dp.Numerator,
            "denominator" => dp.Denominator,
            "type" => dp.Type,
            "precision" => dp.Precision,
            _ => null,
        },
        DvOrdinal dor => name switch
        {
            "value" => dor.Value,
            "symbol" => dor.Symbol,
            _ => null,
        },
        DvScale dsc => name switch
        {
            "value" => dsc.Value,
            "symbol" => dsc.Symbol,
            _ => null,
        },
        DvDate dd => name switch { "value" => dd.Value, _ => null },
        DvTime dtm => name switch { "value" => dtm.Value, _ => null },
        DvDateTime ddt => name switch { "value" => ddt.Value, _ => null },
        DvDuration ddu => name switch { "value" => ddu.Value, _ => null },
        DvBoolean db => name switch { "value" => (object?)db.Value, _ => null },
        DvUri du => name switch { "value" => du.Value, _ => null },
        DvIdentifier di => name switch
        {
            "id" => di.Id,
            "type" => di.Type,
            "issuer" => di.Issuer,
            "assigner" => di.Assigner,
            _ => null,
        },
        CodePhrase cp => name switch
        {
            "code_string" => cp.CodeString,
            "terminology_id" => cp.TerminologyId,
            "preferred_term" => cp.PreferredTerm,
            _ => null,
        },
        UidBasedId u => name switch { "value" => u.Value, _ => null },
        ObjectId oid => name switch { "value" => oid.Value, _ => null },
        Archetyped ar => name switch
        {
            "archetype_id" => ar.ArchetypeId,
            "template_id" => ar.TemplateId,
            "rm_version" => ar.RmVersion,
            _ => null,
        },
        PartyIdentified pi => name switch
        {
            "name" => pi.Name,
            "identifiers" => pi.Identifiers,
            "external_ref" => pi.ExternalRef,
            _ => null,
        },
        Locatable loc => name switch
        {
            "name" => loc.Name,
            "uid" => loc.Uid,
            "archetype_node_id" => loc.ArchetypeNodeId,
            "archetype_details" => loc.ArchetypeDetails,
            "links" => loc.Links,
            "feeder_audit" => loc.FeederAudit,
            _ => null,
        },
        _ => null,
    };

    // -----------------------------------------------------------------
    // Operators
    // -----------------------------------------------------------------

    private static object? EvalBinary(
        BinaryExpression bin,
        Binding binding,
        IReadOnlyDictionary<string, object?> parameters)
    {
        // Short-circuit boolean operators with three-valued logic.
        if (bin.Op == BinaryOp.And)
        {
            object? l = EvalExpr(bin.Left, binding, parameters);
            if (l is bool lb && !lb) return false;
            object? r = EvalExpr(bin.Right, binding, parameters);
            if (r is bool rb && !rb) return false;
            if (l is null || r is null) return null;
            return (bool)l && (bool)r;
        }
        if (bin.Op == BinaryOp.Or)
        {
            object? l = EvalExpr(bin.Left, binding, parameters);
            if (l is bool lb && lb) return true;
            object? r = EvalExpr(bin.Right, binding, parameters);
            if (r is bool rb && rb) return true;
            if (l is null || r is null) return null;
            return (bool)l || (bool)r;
        }

        object? left = EvalExpr(bin.Left, binding, parameters);
        object? right = EvalExpr(bin.Right, binding, parameters);

        switch (bin.Op)
        {
            case BinaryOp.Eq:
            case BinaryOp.NotEq:
                if (left is null || right is null)
                {
                    bool bothNull = left is null && right is null;
                    return bin.Op == BinaryOp.Eq ? bothNull : !bothNull;
                }
                bool eq = AreEqual(left, right);
                return bin.Op == BinaryOp.Eq ? eq : !eq;

            case BinaryOp.Lt:
            case BinaryOp.Lte:
            case BinaryOp.Gt:
            case BinaryOp.Gte:
            {
                int? cmp = CompareValues(left, right);
                if (cmp is null) return null;
                return bin.Op switch
                {
                    BinaryOp.Lt => cmp.Value < 0,
                    BinaryOp.Lte => cmp.Value <= 0,
                    BinaryOp.Gt => cmp.Value > 0,
                    BinaryOp.Gte => cmp.Value >= 0,
                    _ => null,
                };
            }

            case BinaryOp.Concat:
                if (left is null || right is null) return null;
                return ToConcatString(left) + ToConcatString(right);

            case BinaryOp.Plus:
            case BinaryOp.Minus:
            case BinaryOp.Multiply:
            case BinaryOp.Divide:
            {
                if (left is null || right is null) return null;
                double a = ToDouble(left);
                double b = ToDouble(right);
                return bin.Op switch
                {
                    BinaryOp.Plus => (object)(a + b),
                    BinaryOp.Minus => a - b,
                    BinaryOp.Multiply => a * b,
                    BinaryOp.Divide => b == 0 ? null : a / b,
                    _ => null,
                };
            }

            case BinaryOp.Like:
                if (left is null || right is null) return null;
                return LikeMatch(left.ToString() ?? string.Empty, right.ToString() ?? string.Empty);

            case BinaryOp.Matches:
                // Regex / terminology MATCHES with a single RHS.
                if (left is null || right is null) return null;
                return Regex.IsMatch(left.ToString() ?? string.Empty, right.ToString() ?? string.Empty);

            default:
                throw new AqlEvaluationException($"Unsupported binary operator: {bin.Op}");
        }
    }

    private static object? EvalUnary(
        UnaryExpression un,
        Binding binding,
        IReadOnlyDictionary<string, object?> parameters)
    {
        object? v = EvalExpr(un.Operand, binding, parameters);
        if (v is null)
        {
            return un.Op == UnaryOp.Not ? null : null;
        }
        return un.Op switch
        {
            UnaryOp.Not => v is bool b ? !b : null,
            UnaryOp.Negate => -ToDouble(v),
            _ => throw new AqlEvaluationException($"Unsupported unary operator: {un.Op}"),
        };
    }

    private static object? EvalExists(
        Expression operand,
        Binding binding,
        IReadOnlyDictionary<string, object?> parameters)
    {
        object? v = EvalExpr(operand, binding, parameters);
        if (v is null) return false;
        if (v is string s) return s.Length > 0;
        if (v is IEnumerable seq)
        {
            foreach (object? _ in seq) return true;
            return false;
        }
        return true;
    }

    private static object? EvalMatches(
        MatchesExpression m,
        Binding binding,
        IReadOnlyDictionary<string, object?> parameters)
    {
        object? subject = EvalExpr(m.Subject, binding, parameters);
        if (subject is null) return null;
        foreach (Expression cand in m.Values)
        {
            object? c = EvalExpr(cand, binding, parameters);
            if (c is null) continue;
            if (AreEqual(subject, c)) return true;
        }
        return false;
    }

    private static object? EvalFunction(
        FunctionCallExpression fc,
        Binding binding,
        IReadOnlyDictionary<string, object?> parameters)
    {
        string name = fc.Name.ToLowerInvariant();
        switch (name)
        {
            case "count":
            {
                if (fc.Arguments.Count == 0) return 0L;
                object? v = EvalExpr(fc.Arguments[0], binding, parameters);
                if (v is null) return 0L;
                if (v is string) return 1L;
                if (v is IEnumerable seq)
                {
                    long n = 0;
                    foreach (object? _ in seq) n++;
                    return n;
                }
                return 1L;
            }
            case "length":
            {
                if (fc.Arguments.Count == 0) return 0L;
                object? v = EvalExpr(fc.Arguments[0], binding, parameters);
                if (v is null) return null;
                string s = v.ToString() ?? string.Empty;
                return (long)s.Length;
            }
            case "upper":
            {
                if (fc.Arguments.Count == 0) return null;
                object? v = EvalExpr(fc.Arguments[0], binding, parameters);
                return v?.ToString()?.ToUpperInvariant();
            }
            case "lower":
            {
                if (fc.Arguments.Count == 0) return null;
                object? v = EvalExpr(fc.Arguments[0], binding, parameters);
                return v?.ToString()?.ToLowerInvariant();
            }
            case "now":
                return DateTime.UtcNow;
            default:
                throw new AqlEvaluationException($"Unsupported AQL function: {fc.Name}");
        }
    }

    // -----------------------------------------------------------------
    // Equality and comparison
    // -----------------------------------------------------------------

    private static bool AreEqual(object left, object right)
    {
        // Unwrap DV types for ergonomic equality vs strings/numbers.
        object l = Coerce(left);
        object r = Coerce(right);
        if (l is double ld && r is double rd) return ld.Equals(rd);
        if (l is double || r is double)
        {
            return ToDouble(l).Equals(ToDouble(r));
        }
        if (l is long ll && r is long rr) return ll == rr;
        if ((l is long || l is int) && (r is long || r is int))
        {
            return Convert.ToInt64(l, CultureInfo.InvariantCulture) == Convert.ToInt64(r, CultureInfo.InvariantCulture);
        }
        if (l is string ls && r is string rs) return string.Equals(ls, rs, StringComparison.Ordinal);
        if (l is bool lb && r is bool rb) return lb == rb;
        return l.Equals(r);
    }

    private static object Coerce(object v) => v switch
    {
        DvText t => t.Value,
        DvCount c => c.Magnitude,
        DvBoolean b => b.Value,
        DvUri u => u.Value,
        UidBasedId u => u.Value,
        ObjectId o => o.Value,
        IsoDate d => d.OriginalLexicalForm,
        IsoTime t => t.OriginalLexicalForm,
        IsoDateTime dt => dt.OriginalLexicalForm,
        IsoDuration du => du.OriginalLexicalForm,
        _ => v,
    };

    private static int? CompareValues(object? left, object? right)
    {
        if (left is null || right is null) return null;
        // DV_QUANTITY × DV_QUANTITY: unit-aware.
        if (left is DvQuantity lq && right is DvQuantity rq)
        {
            double? converted = ConvertMagnitude(rq.Magnitude, rq.Units, lq.Units);
            if (converted is null) return null;
            return lq.Magnitude.CompareTo(converted.Value);
        }
        // DV_QUANTITY × number: compare magnitudes directly.
        if (left is DvQuantity lq2 && IsNumeric(right))
        {
            return lq2.Magnitude.CompareTo(ToDouble(right));
        }
        if (right is DvQuantity rq2 && IsNumeric(left))
        {
            return ToDouble(left).CompareTo(rq2.Magnitude);
        }
        object l = Coerce(left);
        object r = Coerce(right);
        if (l is string ls && r is string rs)
        {
            return string.CompareOrdinal(ls, rs);
        }
        if (IsNumeric(l) && IsNumeric(r))
        {
            return ToDouble(l).CompareTo(ToDouble(r));
        }
        if (l is bool lb && r is bool rb)
        {
            return lb.CompareTo(rb);
        }
        if (l is IComparable lc && l.GetType() == r.GetType())
        {
            return lc.CompareTo(r);
        }
        return null;
    }

    private static bool IsNumeric(object? v)
        => v is double || v is float || v is long || v is int || v is short || v is byte
        || v is decimal || v is DvCount;

    private static double ToDouble(object? v) => v switch
    {
        null => double.NaN,
        double d => d,
        float f => f,
        long l => l,
        int i => i,
        short s => s,
        byte b => b,
        decimal dec => (double)dec,
        bool bo => bo ? 1.0 : 0.0,
        DvCount dc => dc.Magnitude,
        DvQuantity dq => dq.Magnitude,
        string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) => parsed,
        _ => double.NaN,
    };

    private static string ToConcatString(object v) => v switch
    {
        string s => s,
        DvText t => t.Value,
        _ => v.ToString() ?? string.Empty,
    };

    // -----------------------------------------------------------------
    // Unit-aware conversion (small built-in table).
    // -----------------------------------------------------------------

    private static double? ConvertMagnitude(double magnitude, string fromUnits, string toUnits)
    {
        if (string.Equals(fromUnits, toUnits, StringComparison.Ordinal)) return magnitude;
        // Both units must be on the same base. Convert source → base,
        // then base → target. If either lookup fails, units are
        // incompatible and the caller treats the comparison as null.
        if (!UnitFactors.TryGetValue(fromUnits, out (string Base, double ToBase) from)) return null;
        if (!UnitFactors.TryGetValue(toUnits, out (string Base, double ToBase) to)) return null;
        if (!string.Equals(from.Base, to.Base, StringComparison.Ordinal)) return null;
        double inBase = magnitude * from.ToBase;
        return inBase / to.ToBase;
    }

    // unit → (canonical base unit, factor: 1 unit = factor * base).
    private static readonly Dictionary<string, (string Base, double ToBase)> UnitFactors
        = new(StringComparer.Ordinal)
        {
            // Pressure (base: mm[Hg]).
            ["mm[Hg]"] = ("mm[Hg]", 1.0),
            ["mmHg"] = ("mm[Hg]", 1.0),
            ["kPa"] = ("mm[Hg]", 7.50061682704),
            // Length (base: m).
            ["m"] = ("m", 1.0),
            ["cm"] = ("m", 0.01),
            ["mm"] = ("m", 0.001),
            // Mass (base: g).
            ["g"] = ("g", 1.0),
            ["kg"] = ("g", 1000.0),
            ["mg"] = ("g", 0.001),
            // Time (base: s).
            ["s"] = ("s", 1.0),
            ["min"] = ("s", 60.0),
            ["h"] = ("s", 3600.0),
            // Temperature (base: Cel). Note: Cel and [degF] would
            // require offsets, not factors; kept out for now.
            ["Cel"] = ("Cel", 1.0),
        };

    // -----------------------------------------------------------------
    // LIKE pattern → Regex
    // -----------------------------------------------------------------

    private static bool LikeMatch(string input, string pattern)
    {
        System.Text.StringBuilder rx = new();
        rx.Append('^');
        foreach (char ch in pattern)
        {
            if (ch == '%') rx.Append(".*");
            else if (ch == '_') rx.Append('.');
            else rx.Append(Regex.Escape(ch.ToString()));
        }
        rx.Append('$');
        return Regex.IsMatch(input, rx.ToString());
    }

    // -----------------------------------------------------------------
    // DISTINCT row keys (structural element-wise equality, null-safe).
    // -----------------------------------------------------------------

    private readonly struct RowKey : IEquatable<RowKey>
    {
        private readonly object?[] _values;

        public RowKey(object?[] values) { _values = values; }

        public bool Equals(RowKey other)
        {
            if (_values.Length != other._values.Length) return false;
            for (int i = 0; i < _values.Length; i++)
            {
                object? a = _values[i];
                object? b = other._values[i];
                if (a is null && b is null) continue;
                if (a is null || b is null) return false;
                if (!AreEqual(a, b)) return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => obj is RowKey k && Equals(k);

        public override int GetHashCode()
        {
            HashCode hc = new();
            foreach (object? v in _values)
            {
                hc.Add(v is null ? 0 : Coerce(v).GetHashCode());
            }
            return hc.ToHashCode();
        }
    }
}
