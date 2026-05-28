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

## AOT / trim

Fully AOT- and trim-safe; no runtime reflection. The evaluator uses
closed switches over the supported RM / DataType shapes — no
`Reflection.Emit`, no `Expression.Compile`, no runtime code generation.

## See also

- `DotnetOpenEhr.Rm` — the typed Composition tree the evaluator walks.
- [`docs/package-map.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/package-map.md)
- [`docs/getting-started.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/getting-started.md)
