# DotnetOpenEhr.Templates.Abstractions

Seam abstractions used by openEHR template-aware components in the
DotnetOpenEhr SDK. Lives in its own tiny package so the FLAT JSON
serializer and the OPT-backed template model can interoperate without a
circular reference.

## What this package gives you

- `ITemplateSchema` — the abstract contract a template-aware consumer
  needs: resolve an RM type at a path, look up cardinality / occurrences
  constraints, enumerate allowed children.
- `TemplatePathInfo` and supporting DTOs returned by the schema.

## Example

```csharp
using DotnetOpenEhr.Templates.Abstractions;

void ReadWithSchema(ITemplateSchema schema)
{
    if (schema.TryGetPath("/content[openEHR-EHR-OBSERVATION.bp.v2]", out var info))
    {
        Console.WriteLine($"RM type at path: {info.RmTypeName}");
    }
}
```

A concrete implementation backed by an OPT2 ships in
`DotnetOpenEhr.Templates`. The FLAT JSON serializer
(`DotnetOpenEhr.Serialization.Json.Flat`) accepts any `ITemplateSchema`.

## AOT / trim

Fully AOT- and trim-safe; no runtime reflection. Pure interface +
record/DTO surface.

## See also

- `DotnetOpenEhr.Templates` — concrete OPT2-backed schema.
- `DotnetOpenEhr.Serialization.Json.Flat` — the primary consumer.
- [`docs/package-map.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/package-map.md)
- [`docs/getting-started.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/getting-started.md)
