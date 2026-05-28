# Changelog

All notable changes to the **DotnetOpenEhr SDK** are documented in this
file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

While the SDK is in the `0.1.0-alpha.*` line, **public APIs are
unstable** and may change between alphas.

## [Unreleased]

## [0.1.0-alpha.1] - 2026-02-14

First public alpha. Installs as a granular set of twelve
`DotnetOpenEhr.*` packages, or via the new umbrella metapackage
`DotnetOpenEhr`.

### Added

- **Umbrella metapackage `DotnetOpenEhr`** — single `dotnet add package`
  pulls in every shipping component at the same version. See
  [`docs/package-map.md`](docs/package-map.md) for the granular option.
- **Per-package READMEs** — every shipping package now publishes a
  `README.md` showing its primary surface and a small snippet, packed
  via `PackageReadmeFile`.
- **Top-level documentation set** under `docs/`:
  [`getting-started.md`](docs/getting-started.md),
  [`package-map.md`](docs/package-map.md),
  [`aot.md`](docs/aot.md).
- **Repo & build foundation:** .NET 10 / C# 14 single-TFM
  solution, central package management, MinVer + SourceLink, shared
  AOT/trim posture for every `src/` project, `_IsShippingProject` guard
  in `Directory.Build.targets`.
- **`DotnetOpenEhr.Foundation`:** ISO 8601 date/time/duration/
  timezone value types preserving original lexical form,
  `Interval<T>`, `Cardinality`, `TerminologyCode`.
- **`DotnetOpenEhr.Rm`:** hand-authored, polymorphic,
  source-generator-friendly Reference Model and Data Types (Common,
  Data Types, Data Structures, EHR, Demographic, Identification).
- **`DotnetOpenEhr.Serialization.Json`:** canonical /
  STRUCTURED openEHR JSON round-trip via STJ source generation only.
  Includes documented sibling-key ordering for byte-equivalent
  round-trip ([`docs/canonical-json-ordering.md`](docs/canonical-json-ordering.md)).
- **`DotnetOpenEhr.Templates.Abstractions` +
  `DotnetOpenEhr.Serialization.Json.Flat`:** FLAT openEHR JSON
  serializer with schemaless + schema-driven modes, sharing the
  source-generated context with the canonical serializer.
- **`DotnetOpenEhr.Terminology`:** openEHR Support
  Terminology groups embedded as JSON resources with an AOT-safe
  lookup API.
- **`DotnetOpenEhr.Odin`:** standalone hand-written ODIN
  parser, AST, and writer.
- **`DotnetOpenEhr.Bmm` + `DotnetOpenEhr.Bmm.Rm`:** BMM
  object model and parser plus the canonical openEHR RM BMM schemas
  (BASE 1.2.0, RM 1.1.0) bundled as embedded resources with a typed
  loader. `DotnetOpenEhr.Bmm.Rm` ships under SPDX `MIT AND Apache-2.0`
  to honour the upstream openEHR license.
- **`DotnetOpenEhr.Archetypes`:** ADL2 / AOM2 parser and
  object model.
- **`DotnetOpenEhr.Templates`:** OPT2 parser, concrete
  `OperationalTemplate` model, `ITemplateSchema` implementation, and
  `OperationalTemplateValidator` for template-driven Composition
  validation.
- **`DotnetOpenEhr.Aql`:** hand-written AQL lexer, parser,
  AST, and tree-walking in-memory evaluator (FROM / CONTAINS,
  three-valued WHERE, SELECT / DISTINCT, multi-column ORDER BY, LIMIT /
  OFFSET, parameter binding, sync + streaming `IAsyncEnumerable`
  evaluation).
- **AOT / trim smoke (`tests/DotnetOpenEhr.AotSmoke`)** publishing
  with `PublishAot=true` on linux-x64 and win-x64 in CI; treats trimmer
  and AOT warnings as errors and gates merges to `main`.

### Notes

- Single TFM `net10.0`. C# 14, `Nullable` and `ImplicitUsings` enabled.
- All shipping packages are MIT-licensed except `DotnetOpenEhr.Bmm.Rm`
  (SPDX `MIT AND Apache-2.0`).
- This is the first public alpha — the APIs are unstable and may break
  between alphas without notice.

[Unreleased]: https://github.com/ginoc/dotnet-openehr-sdk/compare/v0.1.0-alpha.1...HEAD
[0.1.0-alpha.1]: https://github.com/ginoc/dotnet-openehr-sdk/releases/tag/v0.1.0-alpha.1
