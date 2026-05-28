# Getting started with DotnetOpenEhr

A 5-minute tour: install the SDK, parse a canonical openEHR JSON
Composition, validate it against an Operational Template, and run an
AQL query over an in-memory list. Every step is AOT- and trim-safe.

## Install

The fastest way to start is the **umbrella metapackage**:

```bash
dotnet add package DotnetOpenEhr
```

That pulls in every `DotnetOpenEhr.*` shipping package at matching
versions. If you'd rather keep the dependency graph minimal, install
only what you need — see
[`package-map.md`](package-map.md) for the per-use-case mapping.

Targets `net10.0`. Single TFM; C# 14.

## 1. Parse a canonical openEHR JSON Composition

`DotnetOpenEhr.Serialization.Json` provides `OpenEhrJson`, a static
façade over the source-generated `OpenEhrJsonContext`.

```csharp
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Serialization.Json;

string json = File.ReadAllText("vitals.json");
Composition? c = OpenEhrJson.ParseComposition(json);

// Serialize back to canonical openEHR JSON (UTF-8 bytes).
byte[] roundTripped = OpenEhrJson.Serialize(c!);
```

The UTF-8 byte overload (`ParseComposition(ReadOnlySpan<byte>)`) skips
the string allocation and is what you should use on hot paths.

For the FLAT JSON dialect, use `OpenEhrFlatJson` from
`DotnetOpenEhr.Serialization.Json.Flat`:

```csharp
using DotnetOpenEhr.Serialization.Json.Flat;

Composition? flat = OpenEhrFlatJson.ParseComposition(File.ReadAllBytes("vitals.flat.json"));
```

## 2. Validate against an Operational Template (OPT2)

`DotnetOpenEhr.Templates` parses OPT2 sources into an
`OperationalTemplate`, and `OperationalTemplateValidator` walks an RM
`Composition` against it.

```csharp
using DotnetOpenEhr.Templates;
using DotnetOpenEhr.Templates.Validation;

OperationalTemplate opt = Opt2Parser.Parse(File.ReadAllText("vitals.opt2"));

OperationalTemplateValidator validator = new();
IReadOnlyList<ValidationIssue> issues = validator.Validate(c!, opt);

foreach (ValidationIssue issue in issues)
{
    Console.WriteLine($"{issue.Severity} {issue.Path}: {issue.Message}");
}
```

`ValidationIssue` covers structural, cardinality, occurrences, and
data-type-constraint findings emitted by the template walker.

## 3. Run an AQL query over in-memory Compositions

`DotnetOpenEhr.Aql` provides the `AqlParser` (source → AST) and
`AqlEvaluator` (AST → rows over an `IEnumerable<Composition>`).

```csharp
using DotnetOpenEhr.Aql;
using DotnetOpenEhr.Aql.Evaluation;

AqlQuery query = AqlParser.Parse(
    "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c " +
    "WHERE c/name/value = 'Vital signs' " +
    "ORDER BY c/uid/value LIMIT 50");

IEnumerable<Composition> source = new[] { c! };
AqlEvaluator evaluator = new();
IReadOnlyList<object?[]> rows = evaluator.Evaluate(query, source);

foreach (object?[] row in rows)
{
    Console.WriteLine(string.Join(" | ", row));
}
```

For streaming sources use `EvaluateAsync(query, IAsyncEnumerable<Composition>)`.

## Per-package READMEs

Each shipping package has its own README that lists the public surface
and shows a tiny snippet:

- [Foundation](https://www.nuget.org/packages/DotnetOpenEhr.Foundation)
- [Terminology](https://www.nuget.org/packages/DotnetOpenEhr.Terminology)
- [Odin](https://www.nuget.org/packages/DotnetOpenEhr.Odin)
- [Bmm](https://www.nuget.org/packages/DotnetOpenEhr.Bmm)
- [Bmm.Rm](https://www.nuget.org/packages/DotnetOpenEhr.Bmm.Rm)
- [Rm](https://www.nuget.org/packages/DotnetOpenEhr.Rm)
- [Serialization.Json](https://www.nuget.org/packages/DotnetOpenEhr.Serialization.Json)
- [Serialization.Json.Flat](https://www.nuget.org/packages/DotnetOpenEhr.Serialization.Json.Flat)
- [Archetypes](https://www.nuget.org/packages/DotnetOpenEhr.Archetypes)
- [Templates.Abstractions](https://www.nuget.org/packages/DotnetOpenEhr.Templates.Abstractions)
- [Templates](https://www.nuget.org/packages/DotnetOpenEhr.Templates)
- [Aql](https://www.nuget.org/packages/DotnetOpenEhr.Aql)

See also [`aot.md`](aot.md) for the AOT/trim posture and the smoke gate
the CI enforces on every PR.
