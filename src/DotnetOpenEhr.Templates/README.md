# DotnetOpenEhr.Templates

openEHR **Operational Template (OPT2)** parser and template-driven
validation for .NET. Reuses the ADL2 parser from
`DotnetOpenEhr.Archetypes` and adds OPT2-specific extensions
(`component_terminologies`) plus a concrete `ITemplateSchema`
implementation suitable for driving the template-aware FLAT serializer.

## What this package gives you

- `Opt2Parser.Parse(string)` / `Parse(ReadOnlySpan<char>)` /
  `Parse(string, BmmModel)` — reads an OPT2 source into a strongly-typed
  `OperationalTemplate`.
- `OperationalTemplate` — concrete model exposing the merged AOM2 tree,
  template id, component terminologies, and `ITemplateSchema`.
- `OperationalTemplateValidator.Validate(Composition, OperationalTemplate)`
  — walks an RM `Composition` in lock-step with the template and emits
  `ValidationIssue` findings (structural, cardinality, occurrences,
  data-type constraints).

## Example

```csharp
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Serialization.Json;
using DotnetOpenEhr.Templates;
using DotnetOpenEhr.Templates.Validation;

OperationalTemplate opt = Opt2Parser.Parse(File.ReadAllText("vitals.opt2"));
Composition c = OpenEhrJson.ParseComposition(File.ReadAllText("vitals.json"))!;

OperationalTemplateValidator v = new();
foreach (ValidationIssue issue in v.Validate(c, opt))
{
    Console.WriteLine($"{issue.Severity} {issue.Path}: {issue.Message}");
}
```

## AOT / trim

Fully AOT- and trim-safe; no runtime reflection. The validator uses a
closed switch over the supported RM types instead of reflection.

## See also

- `DotnetOpenEhr.Serialization.Json.Flat` — consume
  `OperationalTemplate` as an `ITemplateSchema` for FLAT round-trip.
- [`docs/package-map.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/package-map.md)
- [`docs/getting-started.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/getting-started.md)
