# DotnetOpenEhr.Aql

openEHR **AQL (Archetype Query Language)** parser, AST, and tree-walking
**in-memory evaluator** for the DotnetOpenEhr SDK.

## What this package gives you

- `AqlParser.Parse(string)` / `Parse(ReadOnlySpan<char>)` — produces a
  strongly-typed `AqlQuery` AST.
- `AqlQuery` AST — `SelectClause`, `FromClause`, `WhereClause`,
  `OrderByClause`, `Limit` / `Offset`, parameter references.
- `AqlEvaluator` — tree-walking interpreter that evaluates an
  `AqlQuery` against an `IEnumerable<Composition>` (or
  `IAsyncEnumerable<Composition>` for streaming sources). Supports
  FROM / CONTAINS, three-valued WHERE, SELECT projection, DISTINCT,
  multi-column ORDER BY, LIMIT / OFFSET, and parameter binding.

## Example

```csharp
using DotnetOpenEhr.Aql;
using DotnetOpenEhr.Aql.Evaluation;
using DotnetOpenEhr.Rm.Composition;

AqlQuery query = AqlParser.Parse(
    "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c " +
    "WHERE c/name/value = 'Vital signs' ORDER BY c/uid/value LIMIT 50");

IEnumerable<Composition> source = LoadCompositions();
AqlEvaluator evaluator = new();
IReadOnlyList<object?[]> rows = evaluator.Evaluate(query, source);
```

## Resolving archetype paths

For the everyday "give me the value(s) at this archetype path against
this `Pathable` root" operation — mapping pipelines, validators,
sample code — use `ArchetypePathResolver` (one-shot) or `ArchetypePath`
(pre-compiled). Both surfaces share the same RM attribute switch as
`AqlEvaluator`, so they cannot drift on which RM attributes are
walkable or how predicates filter.

```csharp
using DotnetOpenEhr.Aql;
using DotnetOpenEhr.Rm.Common;

// One-shot resolution. Parses on every call; cheap for ad-hoc use.
double systolic = ArchetypePathResolver.Resolve<double>(
    observation,
    "/data/events[at0006]/data[at0003]/items[at0004]/value/magnitude");

// All matches (no event predicate → one entry per event in
// RM-collection order).
IEnumerable<object?> magnitudes = ArchetypePathResolver.ResolveAll(
    observation,
    "/data/events/data/items[at0004]/value/magnitude");

// Pre-compiled path — parse once, re-resolve against many roots.
ArchetypePath path = ArchetypePath.Parse(
    "/data/events/data/items[at0004]/value/magnitude");
foreach (Pathable root in observations)
{
    double value = path.Resolve<double>(root);
    // ...
}
```

`Resolve` returns `null` when the path does not resolve and throws
`InvalidOperationException` if the path matches more than one node
(use `ResolveAll`). The generic overloads return `default(T)` for
unresolved paths and throw `InvalidCastException` on type mismatch
(`ResolveAll<T>` throws on the first offending element). Path
predicates support node ids (`[at0006]`, `[idN]`, `[acN]`, archetype
HRIDs), name predicates (`['Systolic']`), and the combined
`[at0004, 'Systolic']` form. String literals follow the same
backslash-escape set as AQL (`\\`, `\'`, `\n`, etc.) — not SQL-style
`''` doubling.

Both surfaces are trim- and Native-AOT-safe (no reflection, no
`Expression.Compile`, no runtime code generation).

## AOT / trim

Fully AOT- and trim-safe; no runtime reflection. The evaluator uses
closed switches over the supported RM / DataType shapes — no
`Reflection.Emit`, no `Expression.Compile`, no runtime code generation.

## See also

- `DotnetOpenEhr.Rm` — the typed Composition tree the evaluator walks.
- [`docs/package-map.md`](https://github.com/GinoCanessa/dotnet-openehr-sdk/blob/main/docs/package-map.md)
- [`docs/getting-started.md`](https://github.com/GinoCanessa/dotnet-openehr-sdk/blob/main/docs/getting-started.md)
