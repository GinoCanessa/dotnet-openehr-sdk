# DotnetOpenEhr.Archetypes

openEHR **ADL2 / AOM2** parser and object model for .NET — the
Archetype Object Model classes and a hand-written ADL2 parser.

## What this package gives you

- `Adl2Parser.Parse(string)` / `Parse(ReadOnlySpan<char>)` — reads an
  ADL2 source document into a strongly-typed `Archetype`.
- AOM2 object model: `Archetype`, `Template`, `OperationalTemplate`,
  `CComplexObject`, `CAttribute`, `CObject`, `CPrimitiveObject`,
  `ArchetypeTerminology`, `ArchetypeHRID`, …
- Identification helpers: `ArchetypeHRID.Parse`, `VersionId.Parse`.

## Example

```csharp
using DotnetOpenEhr.Archetypes.Adl2;
using DotnetOpenEhr.Archetypes.Aom2;

string source = File.ReadAllText("openEHR-EHR-OBSERVATION.blood_pressure.v2.adls");
Archetype a = Adl2Parser.Parse(source);

Console.WriteLine($"{a.ArchetypeId} concept={a.Definition.NodeId}");
```

## AOT / trim

Fully AOT- and trim-safe; no runtime reflection. The parser is
hand-written recursive-descent on the shared ODIN tokenizer. The AOM2
object graph is plain mutable C# classes with no reflection-based
serialization.

## See also

- `DotnetOpenEhr.Templates` — OPT2 parser that extends this AOM2 model.
- `DotnetOpenEhr.Bmm.Rm` — RM BMM schemas used during archetype slot
  resolution.
- [`docs/package-map.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/package-map.md)
- [`docs/getting-started.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/getting-started.md)
