# DotnetOpenEhr — a modern .NET SDK for openEHR

[![CI](https://github.com/ginoc/dotnet-openehr-sdk/actions/workflows/ci.yml/badge.svg)](https://github.com/ginoc/dotnet-openehr-sdk/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A first-class .NET 10 / C# 14 SDK for working with **openEHR** artefacts
in-process: parse and serialize Compositions in canonical and FLAT JSON,
work with the strongly-typed Reference Model and Data Types, validate
Compositions against operational templates, and evaluate AQL queries
over in-memory data — all trim-safe and Native-AOT-safe.

> **Status:** pre-alpha. APIs are unstable. v1 will ship as
> `0.1.0-alpha.*` on NuGet once Phase 11 of the implementation plan
> lands.

## Package map

Ship as multiple NuGet packages under the **`DotnetOpenEhr.*`**
identifier so consumers only pay for what they use:

| Package | Purpose |
|---|---|
| `DotnetOpenEhr` | Umbrella metapackage — references every other package. |
| `DotnetOpenEhr.Foundation` | Foundation Types (`Interval<T>`, ISO date/time, etc.). |
| `DotnetOpenEhr.Rm` | Reference Model + Data Types (strongly-typed). |
| `DotnetOpenEhr.Serialization.Json` | Canonical/STRUCTURED openEHR JSON round-trip. |
| `DotnetOpenEhr.Serialization.Json.Flat` | FLAT openEHR JSON round-trip. |
| `DotnetOpenEhr.Templates.Abstractions` | `ITemplateSchema` seam shared by FLAT + Templates. |
| `DotnetOpenEhr.Odin` | ODIN parser/serializer. |
| `DotnetOpenEhr.Terminology` | openEHR Support Terminology (built-in groups). |
| `DotnetOpenEhr.Bmm` | Basic Meta-Model (BMM) object model + parser. |
| `DotnetOpenEhr.Archetypes` | ADL2 / AOM2 parser + object model. |
| `DotnetOpenEhr.Templates` | OPT2 parser + template-driven validation. |
| `DotnetOpenEhr.Aql` | AQL parser, AST, in-memory tree-walking evaluator. |

A fuller `docs/package-map.md` lands with Phase 11.

## Build / Test / AOT

```pwsh
dotnet --version                                   # expects 10.x
dotnet build dotnet-openehr-sdk.slnx -c Release
dotnet test  dotnet-openehr-sdk.slnx -c Release --no-build

# AOT/trim smoke (this is what CI gates on):
dotnet publish tests/DotnetOpenEhr.AotSmoke -c Release -r linux-x64 -p:PublishAot=true
./tests/DotnetOpenEhr.AotSmoke/bin/Release/net10.0/linux-x64/publish/DotnetOpenEhr.AotSmoke
```

## Design tenets

- **.NET 10 / C# 14**, single TFM `net10.0`.
- **`System.Text.Json` source-generated** everywhere — no
  `Newtonsoft.Json`, no reflection-based serialization in shipped APIs.
- **Hand-written tokenizer + recursive-descent parsers** for ADL2, AOM2,
  OPT2, ODIN, and AQL. No ANTLR runtime, no `Expression.Compile`, no
  `Emit`.
- **AOT-/trim-safe** is a hard requirement; CI publishes a `PublishAot`
  smoke app exercising every shipping assembly and treats trim/AOT
  warnings as errors.

## License

[MIT](LICENSE). Note: test fixtures under `tests/**/Fixtures/` may be
licensed CC-BY-SA 3.0 (see each `ATTRIBUTION.md`). Shipping `src/`
assemblies and NuGet packages are MIT-only.

## Contributing & security

See [`CONTRIBUTING.md`](CONTRIBUTING.md) and [`SECURITY.md`](SECURITY.md).
