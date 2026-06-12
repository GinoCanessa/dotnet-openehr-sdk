# DotnetOpenEhr.Templates

openEHR **Operational Template** parser and template-driven validation
for .NET. Reads both flavours of the OPT serialisation in active use:

- **OPT2** (ADL2-text) via `Opt2Parser`, by way of the ADL2 parser in
  `DotnetOpenEhr.Archetypes` plus OPT2-specific extensions
  (`component_terminologies`).
- **OPT1.4** (XML) via `Opt14XmlParser`, the format every authoring
  tool (CKM, Better Studio, EHRbase, openFHIR's KDS bundle) actually
  emits today.

Both parsers return the same concrete `OperationalTemplate`, so
downstream consumers (`ITemplateSchema`, the template-aware FLAT
serializer, the validator) work uniformly across the two source
formats.

## What this package gives you

- `Opt2Parser.Parse(string)` / `Parse(ReadOnlySpan<char>)` /
  `Parse(string, BmmModel)` — reads an OPT2 source into a strongly-typed
  `OperationalTemplate`.
- `Opt14XmlParser.Load(Stream)` / `Load(string filePath)` /
  `Parse(string xmlText)` (plus `BmmModel` overloads) — reads an
  OPT1.4 XML source into the same `OperationalTemplate`.
- `ParseOptions { Lenient = true }` — opt-in tolerance for vendor
  extensions and namespace drift in OPT1.4 documents; never silently
  drops terminology data.
- `OperationalTemplate` — concrete model exposing the merged AOM2 tree,
  template id, component terminologies, and `ITemplateSchema`.
- `OperationalTemplateValidator.Validate(Composition, OperationalTemplate)`
  — walks an RM `Composition` in lock-step with the template and emits
  `ValidationIssue` findings (structural, cardinality, occurrences,
  data-type constraints).

## When to use which parser

| You have…                                                | Use                  |
| -------------------------------------------------------- | -------------------- |
| Hand-authored ADL2-text template (`.opt2` / `.adls`)     | `Opt2Parser`         |
| Vendor or CKM export, openFHIR KDS bundle (`.opt` XML)   | `Opt14XmlParser`     |
| Don't know — strict-mode auto-detect needed              | inspect the first non-whitespace byte: `<` ⇒ OPT1.4, otherwise OPT2 |

## Example

```csharp
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Serialization.Json;
using DotnetOpenEhr.Templates;
using DotnetOpenEhr.Templates.Validation;

// OPT2 (ADL2-text):
OperationalTemplate opt2 = Opt2Parser.Parse(File.ReadAllText("vitals.opt2"));

// OPT1.4 (XML):
OperationalTemplate opt14 = Opt14XmlParser.Load("KDS_Vitalstatus.opt");

// Lenient mode (forgives missing openEHR xmlns + unknown xsi:type
// vendor extensions; still throws if terminology data would be lost):
OperationalTemplate vendor = Opt14XmlParser.Load(
    "vendor.opt",
    new ParseOptions { Lenient = true });

Composition c = OpenEhrJson.ParseComposition(File.ReadAllText("vitals.json"))!;
OperationalTemplateValidator v = new();
foreach (ValidationIssue issue in v.Validate(c, opt2))
{
    Console.WriteLine($"{issue.Severity} [{issue.RuleId}] {issue.Path}: {issue.Message}");
}
```

`Opt14XmlParser` throws `Opt14ParseException` (subclass of
`InvalidOperationException`, with `LineNumber` / `LinePosition`) on
strict-mode schema violations; lenient mode collects unknown
`xsi:type` discriminators and skips the offending `<children>` rather
than throwing.

## AOT / trim

Fully AOT- and trim-safe; no runtime reflection.
- `Opt2Parser` uses a closed switch over the supported RM types.
- `Opt14XmlParser` uses `XDocument` / `XmlReader` directly (no
  `XmlSerializer` / `DataContractSerializer` / reflection-driven XML
  binding).
- The validator uses a closed switch over the supported RM types
  instead of reflection.

## See also

- `DotnetOpenEhr.Serialization.Json.Flat` — consume
  `OperationalTemplate` as an `ITemplateSchema` for FLAT round-trip.
- [`docs/package-map.md`](https://github.com/GinoCanessa/dotnet-openehr-sdk/blob/main/docs/package-map.md)
- [`docs/getting-started.md`](https://github.com/GinoCanessa/dotnet-openehr-sdk/blob/main/docs/getting-started.md)
- openEHR ITS-XML specifications:
  <https://specifications.openehr.org/releases/ITS-XML/latest/>

