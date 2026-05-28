# DotnetOpenEhr.Serialization.Json.Flat

**FLAT openEHR JSON** serializer for the DotnetOpenEhr SDK. Round-trips
the slash-delimited FLAT JSON dialect into and out of the
`DotnetOpenEhr.Rm` strongly-typed Reference Model.

## What this package gives you

- `OpenEhrFlatJson.ParseComposition(ReadOnlySpan<byte>)` — schemaless
  mode (resolves polymorphism via monomorphic-RM lookup + inline
  `_type` discriminators).
- `OpenEhrFlatJson.ParseComposition(ReadOnlySpan<byte>, ITemplateSchema)`
  — schema-driven mode for fully ambiguous FLAT payloads.
- `OpenEhrFlatJson.Serialize(Composition)` /
  `Serialize(Composition, ITemplateSchema)` — emit FLAT JSON.
- `FlatPath` — typed FLAT path parser and builder.
- `FlatSchemaRequiredException` — thrown when schemaless mode can't
  disambiguate.

## Example

```csharp
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Serialization.Json.Flat;

byte[] utf8 = File.ReadAllBytes("vitals.flat.json");
Composition? c = OpenEhrFlatJson.ParseComposition(utf8);

byte[] back = OpenEhrFlatJson.Serialize(c!);
```

When schemaless mode raises `FlatSchemaRequiredException`, pass an
`ITemplateSchema` (e.g. an `OperationalTemplate` from
`DotnetOpenEhr.Templates`).

## AOT / trim

Fully AOT- and trim-safe; no runtime reflection. Shares the
source-generated `OpenEhrJsonContext` from
`DotnetOpenEhr.Serialization.Json` for all leaf data-value reads/writes.

## See also

- `DotnetOpenEhr.Templates` — provides an `ITemplateSchema` from OPT2.
- [`docs/package-map.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/package-map.md)
- [`docs/getting-started.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/getting-started.md)
