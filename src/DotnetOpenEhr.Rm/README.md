# DotnetOpenEhr.Rm

openEHR **Reference Model (RM)** and **Data Types** for .NET —
hand-authored, polymorphic, `System.Text.Json`-attributed classes.

## What this package gives you

- Strongly-typed classes for the full RM: `Composition`, `Section`,
  `Observation`, `Evaluation`, `Instruction`, `Action`, `ItemTree`,
  `Element`, `Cluster`, `EhrStatus`, `Folder`, `Party*`, etc.
- All openEHR Data Types: `DvText`, `DvCodedText`, `DvQuantity`,
  `DvDateTime`, `DvCount`, `DvBoolean`, `DvIdentifier`, `DvUri`,
  `DvOrdinal`, `DvProportion`, `DvMultimedia`, …
- `[JsonPolymorphic]` + `[JsonDerivedType]` attributes wired across
  every polymorphic base so the source-generated
  `OpenEhrJsonContext` in `DotnetOpenEhr.Serialization.Json` can
  round-trip canonical openEHR JSON with no runtime reflection.

## Example

```csharp
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Common;

Composition c = new()
{
    Name = new DvText { Value = "Vital signs" },
    ArchetypeNodeId = "openEHR-EHR-COMPOSITION.encounter.v1",
    Language = new CodePhrase("ISO_639-1", "en"),
    Territory = new CodePhrase("ISO_3166-1", "AU"),
};
```

## AOT / trim

Fully AOT- and trim-safe; no runtime reflection. Polymorphic
discrimination is declared via attributes that the STJ source generator
materialises at compile time.

## See also

- `DotnetOpenEhr.Serialization.Json` — canonical JSON round-trip.
- `DotnetOpenEhr.Serialization.Json.Flat` — FLAT JSON round-trip.
- [`docs/package-map.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/package-map.md)
- [`docs/getting-started.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/getting-started.md)
