# DotnetOpenEhr — a modern .NET SDK for openEHR

[![Tests](https://github.com/GinoCanessa/dotnet-openehr-sdk/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/GinoCanessa/dotnet-openehr-sdk/actions/workflows/build-and-test.yml)
[![Publish dotnet tool](https://github.com/GinoCanessa/dotnet-openehr-sdk/actions/workflows/nuget-tool.yml/badge.svg)](https://github.com/GinoCanessa/dotnet-openehr-sdk/actions/workflows/nuget-tool.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A first-class .NET 10 / C# 14 SDK for working with **openEHR** artefacts
in-process: parse and serialize Compositions in canonical and FLAT JSON,
work with the strongly-typed Reference Model and Data Types, validate
Compositions against operational templates, and evaluate AQL queries
over in-memory data — all trim-safe and Native-AOT-safe.

> **Status:** beta. APIs may still change before 1.0.
>
> **Versioning:** packages use a date-based scheme,
> `yyyy.MMdd.HHmm-beta.0` (e.g. `2026.0612.1428-beta.0`), emitted by
> `src/Directory.Build.props`. The `-beta.0` suffix marks the current
> pre-1.0 train.

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
| `DotnetOpenEhr.Bmm.Rm` | Canonical openEHR RM BMM schemas (embedded resources, dual-licensed). |
| `DotnetOpenEhr.Archetypes` | ADL2 / AOM2 parser + object model. |
| `DotnetOpenEhr.Templates` | OPT2 and OPT 1.4 XML parser + template-driven validation. |
| `DotnetOpenEhr.Aql` | AQL parser, AST, in-memory tree-walking evaluator. |

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

This repository ships a hybrid license stack:

- **Source code** (everything under `src/`, `tests/`, and the build
  scripts) — [MIT](LICENSE). New contributions to source code must be
  MIT-only.
- **Bundled openEHR specification artefacts** — the canonical BMM
  schema files embedded by `DotnetOpenEhr.Bmm.Rm` (under
  `src/DotnetOpenEhr.Bmm.Rm/Resources/`) are copies of upstream openEHR
  specification material redistributed under the
  [Apache License 2.0](LICENSE-Apache-2.0). See [`NOTICE`](NOTICE) and
  [`src/DotnetOpenEhr.Bmm.Rm/THIRD_PARTY_NOTICES.md`](src/DotnetOpenEhr.Bmm.Rm/THIRD_PARTY_NOTICES.md)
  for attribution and the pinned upstream commit SHA.
- **Test fixtures** under `tests/**/Fixtures/` may carry their own
  licenses (commonly CC-BY-SA 3.0); see each fixture directory's
  `ATTRIBUTION.md`.

The shipping NuGet packages are MIT-only, with the single exception of
`DotnetOpenEhr.Bmm.Rm`, which is published under the SPDX expression
`MIT AND Apache-2.0` to honour the upstream openEHR license on the
embedded BMM resources.

## Contributing & security

See [`CONTRIBUTING.md`](CONTRIBUTING.md) and [`SECURITY.md`](SECURITY.md).
