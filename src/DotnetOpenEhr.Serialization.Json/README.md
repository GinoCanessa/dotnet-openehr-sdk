# DotnetOpenEhr.Serialization.Json

**Canonical / STRUCTURED openEHR JSON** serializer for the DotnetOpenEhr
SDK. Round-trips canonical openEHR JSON into and out of the
`DotnetOpenEhr.Rm` strongly-typed Reference Model via
`System.Text.Json` source generation only — zero runtime reflection.

## What this package gives you

- `OpenEhrJson.ParseComposition(string)` /
  `ParseComposition(ReadOnlySpan<byte>)` — UTF-8 byte path uses the STJ
  source-generated reader directly.
- `OpenEhrJson.ParseCompositionAsync(Stream, CancellationToken)` — async
  stream variant.
- `OpenEhrJson.Serialize(Composition)` — UTF-8 `byte[]` in canonical
  form with the root `_type` discriminator emitted.
- `OpenEhrJson.Serialize(Stream, Composition)` — stream sink.
- `OpenEhrJsonContext` — the source-generated `JsonSerializerContext`,
  exposed for advanced scenarios.

## Example

```csharp
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Serialization.Json;

string json = File.ReadAllText("composition.json");
Composition? c = OpenEhrJson.ParseComposition(json);

byte[] roundTripped = OpenEhrJson.Serialize(c!);
```

## AOT / trim

Fully AOT- and trim-safe; no runtime reflection. Every payload is
routed through the polymorphic `Locatable` type info so canonical
`_type` discrimination is preserved at the document root.

## See also

- `DotnetOpenEhr.Serialization.Json.Flat` — FLAT-dialect round-trip.
- [`docs/canonical-json-ordering.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/canonical-json-ordering.md)
  — the documented sibling-key ordering used for byte-equivalent
  round-trip.
- [`docs/package-map.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/package-map.md)
- [`docs/getting-started.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/getting-started.md)
