# Changelog

All notable changes to the **DotnetOpenEhr SDK** are documented in this
file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

While the SDK is in the `0.1.0-alpha.*` line, **public APIs are
unstable** and may change between alphas.

## [Unreleased]

### Added

- **`Opt14XmlParser` — OPT 1.4 XML loader for the Templates package.**
  Reads the canonical openEHR OPT 1.4 XML serialisation produced by
  every authoring tool (CKM, Better Studio, EHRbase Template
  Repository, openFHIR KDS) and returns the same
  `OperationalTemplate` shape `Opt2Parser` returns from ADL2-text, so
  `ITemplateSchema`, the template-aware FLAT serializer, and the
  validator work unchanged across both source formats. Translates
  the full XML graph into AOM2 (xsi:type-dispatched, including the
  `C_PRIMITIVE_OBJECT` envelope unwrap), harvests per-archetype
  terminology from every `C_ARCHETYPE_ROOT` plus any top-level
  `<component_ontologies>` / `<component_terminologies>` block,
  and exposes a `ParseOptions { Lenient }` toggle for vendor /
  namespace drift. Strict-mode errors raise `Opt14ParseException`
  with `IXmlLineInfo` co-ordinates. AOT/trim-safe (uses
  `XDocument` / `XmlReader` only; no reflection or
  `XmlSerializer`). *(0605-01)*
- **ADR 0001 — `DvOrdered<T>` CRTP cascade permanently deferred.**
  `docs/architecture/0001-no-dvordered-crtp-cascade.md` records the
  2026-06-05 decision to keep `ReferenceRange.Range` as
  `Interval<DvOrdered>?` and not pursue a generic self-bound type
  hierarchy. Linked from `docs/README.md`. *(0604-04 Phase 1)*
- **`FlatJsonReader.Read(ReadOnlyMemory<byte>)` overload.** Skips the
  `ReadOnlySpan<byte>.ToArray()` copy that the span overload requires
  (`JsonDocument.Parse` has no span overload). The `Read(Stream)` /
  `ReadAsync(Stream, …)` paths switch from `GetBuffer().AsSpan(…).ToArray()`
  to `GetBuffer().AsMemory(…)`, removing one copy. *(0604-04 Phase 7, L6)*
- **Schema-driven FLAT fixture `temporal_and_null`.** Pins the
  `DvDate` arm of `AssertDataValue` + the `Element.NullFlavour`
  compare path added in 0604-03 Phase 11; registers the scenario in
  `lossless-catalogue.json`. *(0604-04 Phase 9, M26)*
- **Per-group `description` content for every embedded terminology
  entry.** All 14 group JSON files under
  `src/DotnetOpenEhr.Terminology/Groups/` now carry a `description`
  field on every entry, sourced from the openEHR Support
  Terminology spec (TERM Release 3.0.0). Provenance recorded in
  `src/DotnetOpenEhr.Terminology/THIRD_PARTY_NOTICES.md`.
  *(0604-04 Phase 10, L8)*
- **`IntervalJsonConverterFactory` unit tests.** Pin the new closed
  switch over `int` / `long` / `double` / `string` plus the
  reflection-fallback contract. *(0604-04 Phase 11)*
- **Two new `ExpectedIssuesManifest` fixtures** —
  `external_binding_external_takes_precedence` and
  `external_binding_with_no_value_set_skips_validation` — that pin
  the precedence fix landed in 0604-03 Phase 8 for the
  `HasExternalBinding` path. *(0604-04 Phase 5, M4)*
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
- **`OperationalTemplateValidatorOptions.RegexCache`** — optional
  `ConcurrentDictionary<(string Pattern, TimeSpan Timeout), Regex>`
  seam on the validator options. When supplied, the validator uses
  the caller's dictionary as its regex compile cache; when null
  (default), it uses the same process-global cache as before, with
  identical hit/miss behaviour. Primarily intended for tests that
  need a private, observable cache without reaching into validator
  internals. *(0610-01)*
- **Per-pattern compiled-regex cache** in the validator, keyed by
  pattern string, so repeat constraints in a wide OPT share a single
  compiled state machine.
- **AOT smoke test** (`tests/DotnetOpenEhr.AotSmoke`) now part of the
  pre-merge gate; the publish must print `smoke ok` against a
  `PublishAot=true` build.

### Changed

- **Validator regex compile cache is no longer exposed via
  `InternalsVisibleTo`** and the prior `[ThreadStatic]`-based timeout
  plumbing has been removed. The renamed default cache field
  (`s_defaultRegexCache`) is now `private`, and the configured
  `RegexMatchTimeout` is read per-instance from `_options` directly
  inside `ValidateString`. **No effect on public API or default
  behaviour** — default-options validators still share the same
  process-global cache with the same hit/miss profile. Callers that
  want a private cache can now supply
  `OperationalTemplateValidatorOptions.RegexCache`. *(0610-01)*
- **`OdinParseException` messages now include the `(near '…')`
  snippet at every throw site** in both `OdinParser` and `OdinLexer`.
  The exception's `Snippet` property is populated from
  `OdinLexer.Source` via a `BuildSnippet(source, line, column)` helper
  that walks newlines and CR/LF-escapes the slice so the message stays
  single-line. Message text changes; no API surface change.
  *(0604-04 Phase 2, H9)*
- **`Adl2Lexer` / `AqlLexer` HRID grammar tightened to the spec.**
  Both scanners now drive the HRID body through an explicit
  `IsHridTerminator(char)` helper anchored to openEHR Archetype
  Identification spec § 3.2.1 (Release 2.3.0). Behaviour identical to
  the prior implicit inclusion check; the new shape encodes the spec
  so a future maintainer doesn't have to re-derive the rule.
  *(0604-04 Phase 3, M12)*

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

- **M1 — `FlatJsonContentParser.MergeDataValue` no longer silently
  zeros scalars across DV-type transitions.** A new per-DV scalar
  copier (`DataValueScalarCopier`) preserves magnitude when
  `|magnitude` (integral, lands as `DvCount`) is followed by `|units`
  (forces re-instantiation as `DvQuantity`), preserves the text when
  `|value` is followed by `|code`+`|terminology` (`DvText` →
  `DvCodedText`), and explicitly documents the no-op transitions.
  `DvCodedText` is no longer downcast to `DvText` when a `|value`
  arrives after a `|code` pair. *(0604-04 Phase 6)*
- **`IntervalJsonConverterFactory` reflection scope narrowed.** The
  factory now uses a closed switch for Foundation-side `T`s
  (`int`, `long`, `double`, `string`) that returns the typed
  converter without `Activator.CreateInstance` /
  `MakeGenericType`. The reflection fallback (and its
  `[UnconditionalSuppressMessage]`) is retained only for RM-side
  `T`s (currently `DvDateTime`, `DvOrdered`) that Foundation cannot
  reference. Full elimination is deferred per the new ADR.
  *(0604-04 Phase 11)*
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

### Tests

- **H12 — validator `AssertSingleIssue` now pins `Path` alongside
  `RuleId`.** A misrouted issue surfaces as a test failure rather
  than passing silently. Helper extracted to a shared
  `ValidationAssertions.cs` consumed via `using static` from
  `DataTypeTests` (11 sites) and `StringPatternHardeningTests` (2
  sites). *(0604-04 Phase 4)*
- **M16 — DV_QUANTITY / DV_ORDINAL characterisation tests.** Six new
  tests in `Adl2ParserSecondOrderTests` pin that the existing parser
  produces `CComplexObject` + `CAttributeTuple` for both the basic
  and tuple constraint forms (the ADL 2 spec explicitly removed
  `C_DV_QUANTITY` / `C_DV_ORDINAL` in favour of generic tuple
  constraints). *(0604-04 Phase 8)*
- **L8 — terminology `description` pin test flipped.** The previous
  `Description_field_is_currently_unpopulated_for_all_entries` test is
  replaced by a positive `Description_is_populated_for_every_entry_in_every_group`
  fact plus a per-group `[Theory]` variant that names the responsible
  JSON file on failure. *(0604-04 Phase 10)*

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

[Unreleased]: https://github.com/GinoCanessa/dotnet-openehr-sdk/compare/v0.1.0-alpha.1...HEAD
[0.1.0-alpha.1]: https://github.com/GinoCanessa/dotnet-openehr-sdk/releases/tag/v0.1.0-alpha.1
