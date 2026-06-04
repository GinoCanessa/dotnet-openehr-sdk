# Changelog

All notable changes to the **DotnetOpenEhr SDK** are documented in this
file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

While the SDK is in the `0.1.0-alpha.*` line, **public APIs are
unstable** and may change between alphas.

## [Unreleased]

### Added

- **`IsoParseMode` enum** in `DotnetOpenEhr.Foundation.Iso` with
  `Strict`, `Ostrich`, and `FixAsPossible` modes; all
  `Iso{Date,Time,DateTime,Duration,TimeZone}` parsers gained
  `(ReadOnlySpan<char>, IsoParseMode)` overloads. Existing paramless
  overloads default to `FixAsPossible` for backward compatibility.
- **`IsoDuration` now accepts the zero-only forms** `PT0S`, `PT0H`,
  `PT0M` per the ISO 8601 spec; `P1YT` and bare `PT` still reject.
- **`OperationalTemplateValidatorOptions.RegexMatchTimeout`** — opt-in
  per-pattern timeout for the validator's regex evaluator. Defaults to
  100 ms; a `RegexMatchTimeoutException` surfaces as a
  `NotValidated` issue rather than crashing the validator.
- **Per-pattern compiled-regex cache** in the validator, keyed by
  pattern string, so repeat constraints in a wide OPT share a single
  compiled state machine.
- **AOT smoke test** (`tests/DotnetOpenEhr.AotSmoke`) now part of the
  pre-merge gate; the publish must print `smoke ok` against a
  `PublishAot=true` build.

### Changed (BREAKING)

- **RM type narrowing for spec fidelity.** Several RM properties have
  been narrowed to spec-correct concrete bounds. Affected properties:
  - `Composition.Composer` default is now `null` (was
    `new PartyIdentified()`); explicit construction is required.
  - `DvMultimedia.Size` is now `long` (was `int`) so payloads above
    2 GiB round-trip without truncation.
  - Several `Interval<DvOrdered>` properties on `PartyProxy`,
    `Demographic`, `Quantity`, and `History` are now typed with
    spec-correct concrete bounds.
- **`IsoTimeZone` parser range tightened.** Hours outside `[-12, +14]`
  and minutes outside `{0, 15, 30, 45}` now reject under `Strict` and
  clamp/normalise under `FixAsPossible`; equality is now compared
  through `ToTimeSpan` so `+00:00` and `Z` compare equal.
- **`IsoTime.CompareTo` / `IsoDateTime.CompareTo` mixed-zone policy
  unified.** Comparing two operands where one carries a timezone and
  the other does not now throws `InvalidOperationException` instead of
  silently coercing the zoneless operand to local time.

### Fixed

- **B1 — AQL `DISTINCT` row-key canonicalisation.** The `RowKey`
  helper that backs `DISTINCT` now produces a hash and an equality
  contract that agree across mixed `DvText`/`DvCodedText` columns;
  previously the hash and equality could disagree and emit duplicate
  rows.
- **B2 — validator `OverflowException` on legitimate input.**
  `OperationalTemplateValidator.ValidateLong` no longer casts a
  `long` magnitude to `int` with `checked`; values above `int.MaxValue`
  emit a `NumericOutOfRange` issue rather than throwing.
- **B3 — `BmmParseException` line/column.** Every non-ODIN throw site
  in `BmmParser` now threads the offending ODIN token's `Line`/`Column`
  through `OdinValue.Line`/`Column` rather than emitting `0, 0`.
- **B4 — FLAT archive coverage.** The 13 archived
  `openfhir-archive/*_flat.json` fixtures are now driven by the
  `schema-required` bucket of the lossless catalogue, asserting they
  surface a `SchemaRequired` exception with a path-naming message.
- **H1 — AOT trim posture.** Removed the
  `Activator.CreateInstance(..., NonPublic)` site in
  `Adl2Parser.PostProcessForOpt2`; the concrete `OperationalTemplate`
  is now constructed directly via `new()`.
- **H5/H6 — FLAT silent failures.** `FlatJsonContentParser.ReadDouble`/
  `ReadInt`/`ReadInt64` throw `JsonException` with path context on
  malformed numeric input; `InstantiateContentItem` throws on unknown
  `rmType` instead of silently downgrading to `Section`.
- **H7 — `IsoDuration` accepts `PT0S`.** See *Added* above.
- **H8 — validator regex hardening.** The regex evaluator now uses a
  per-pattern compiled-regex cache, a configurable timeout, and emits
  `NotValidated` (not a crash) on parse / timeout.
- **H9 — schema-required exception message format pinned**
  (`FlatSchemaRequiredException.BuildMessage`).
- **H13 — canonical JSON byte snapshot.** Eight canonical-wire
  fixtures now have checked-in `*.expected.json` byte snapshots
  regenerable via `OPENEHR_REGENERATE_CANONICAL_WIRE_SNAPSHOTS=1`.
  Deliberate-mutation smoke confirms drift detection.
- **M1/M2/M4/M5/M7/M11/M13/M19/M21/M22/M23/M25.** Various
  smaller correctness and diagnostic fixes — see commit log.

### Performance

- **H3 — `OperationalTemplate.TryResolveType` is now zero-alloc.** The
  FLAT-path index is exposed through a `FrozenDictionary.AlternateLookup<ReadOnlySpan<char>>`
  so callers no longer incur a per-call `string` allocation.
- **H4 — `AqlEvaluator.Binding` rewritten as a parent-pointer linked
  list.** Each `With(alias, value)` allocates a single node instead of
  cloning a backing dictionary; per-row evaluation under CONTAINS-heavy
  queries is now O(depth) allocations, not O(depth × aliases).
  Equivalence pinned by the `BindingRefactorEquivalenceTests` fixture.
- **M3 — `OperationalTemplate.HasSubtypes` precomputed once per
  `BmmModel`** via `ConditionalWeakTable<BmmModel, FrozenSet<string>>`.
- **M9 — `AqlLexer.MatchKeyword` and `AqlEvaluator.EvalFunction` now
  dispatch through `OrdinalIgnoreCase` `FrozenDictionary` tables**,
  eliminating per-call `ToUpperInvariant`/`ToLowerInvariant`
  allocations.
- **M8 / L7 — assorted hot-path cleanups** in `PathNavigator` and
  `Opt2Parser`.

### Removed

- **MinVer.** Reverts the `MinVer` integration introduced in
  `0.1.0-alpha.1`; package versions are no longer derived from git
  tags. `Microsoft.SourceLink.GitHub` is unaffected and still
  ships.
- **L1/L2/L3 dead code.** Identical-arm ternary in
  `AqlEvaluator.EvalUnary`; per-character loop in `OdinLexer.Advance`;
  unread `AqlLexer._lastKind` field.

### Versioning

- **Versioning is now date-based and deterministic per build.** Every
  shipping `src/` package emits a NuGet version of
  `yyyy.MMdd.HHmm-beta.0` and an `AssemblyVersion` /
  `FileVersion` of `0.yyyy.MMdd.HHmm`, derived from a single
  `UtcNow` read in `src/Directory.Build.props`. NuGet/SemVer 2.0.0
  strips leading zeros from the version segments, so the on-disk
  filename for a build at 2026-06-03 08:12 UTC is
  `<Package>.2026.603.812-beta.0.nupkg`.

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
