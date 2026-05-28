# Package map

The DotnetOpenEhr SDK ships as **twelve granular** `DotnetOpenEhr.*`
packages plus one **umbrella metapackage** (`DotnetOpenEhr`). Pick the
granular packages for production apps that care about trim size and a
tight dependency graph; pick the umbrella for samples, prototypes, or
test harnesses where you want everything at once.

## I want to …

| Use case | Install |
|---|---|
| **Everything, fastest setup** | `DotnetOpenEhr` (umbrella) |
| Work with ISO 8601 dates / `Interval<T>` / terminology codes | `DotnetOpenEhr.Foundation` |
| Look up openEHR support terminology (null flavours, etc.) | `DotnetOpenEhr.Terminology` |
| Parse ODIN documents | `DotnetOpenEhr.Odin` |
| Parse arbitrary BMM schema files | `DotnetOpenEhr.Bmm` |
| Load the canonical openEHR RM BMM | `DotnetOpenEhr.Bmm.Rm` (transitively pulls `DotnetOpenEhr.Bmm`) |
| Hold an openEHR Composition / RM graph in memory | `DotnetOpenEhr.Rm` |
| Parse / write **canonical** openEHR JSON | `DotnetOpenEhr.Serialization.Json` |
| Parse / write **FLAT** openEHR JSON | `DotnetOpenEhr.Serialization.Json.Flat` |
| Parse ADL2 archetypes | `DotnetOpenEhr.Archetypes` |
| Parse OPT2 templates + validate Compositions | `DotnetOpenEhr.Templates` |
| Hand a template to the FLAT serializer without taking `Templates` as a dep | `DotnetOpenEhr.Templates.Abstractions` |
| Parse and run AQL over in-memory Compositions | `DotnetOpenEhr.Aql` |

## Granular vs umbrella

The granular packages let you ship apps that only carry the parsers
they actually exercise. A FLAT-JSON-only ingestion service, for
example, can install `DotnetOpenEhr.Serialization.Json.Flat` and pick up
only `Rm`, `Foundation`, `Serialization.Json`, and
`Templates.Abstractions` transitively — no BMM schemas, no ADL2 parser,
no AQL evaluator.

The umbrella exists purely for convenience. Because it installs every
package at the same `^x.y.z` floor, it guarantees an internally
consistent SDK without you having to fan out twelve
`<PackageReference>` lines. There is no functional difference between
"installed the umbrella" and "installed all twelve packages by hand at
the same version".

## Dependency layering

```
Foundation
 ├── Terminology
 ├── Odin
 │    └── Bmm
 │         └── Bmm.Rm
 ├── Rm
 │    ├── Serialization.Json
 │    │    └── Serialization.Json.Flat (also depends on Templates.Abstractions)
 │    └── Aql
 ├── Templates.Abstractions
 └── Archetypes (uses Odin + Bmm + Bmm.Rm + Terminology)
       └── Templates (also uses Rm + Bmm.Rm + Templates.Abstractions)
```

Every edge is a `ProjectReference` at build time and becomes a
matching-version `PackageReference` at pack time.

## See also

- [`getting-started.md`](getting-started.md)
- [`aot.md`](aot.md)
- [`canonical-json-ordering.md`](canonical-json-ordering.md)
