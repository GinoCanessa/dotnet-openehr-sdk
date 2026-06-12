# AOT / trim posture

DotnetOpenEhr is designed **AOT- and trim-safe from day one**. This
isn't an afterthought: every shipping project enables the trim analyzer
with warnings escalated to errors, so a change that introduces a
reflection-based code path or a non-trim-friendly dependency fails the
ordinary `dotnet build` the CI workflow already runs on every PR. A
fuller Native AOT smoke binary (which also exercises the AOT analyzer's
`IL3xxx` diagnostics) is published and executed as a **local/manual
gate** via the command below; a CI job for it is staged in
`.github/workflows/build-and-test.yml` but is currently disabled.

## What "AOT- and trim-safe" means here

Every shipping `DotnetOpenEhr.*` package sets, via the central
`src/Directory.Build.props`:

- `IsAotCompatible=true`
- `IsTrimmable=true`
- `EnableTrimAnalyzer=true`

…and a `Directory.Build.targets` guard (`_RequireAotFlagsOnShippingProjects`)
fails the build if any shipping project clears those flags.

Concretely that means:

- **No `Reflection.Emit`**, no `Expression.Compile`, no runtime code
  generation anywhere on a hot path.
- **No `System.Reflection`-driven serialization.** Every JSON read/write
  goes through the source-generated `OpenEhrJsonContext` in
  `DotnetOpenEhr.Serialization.Json` (the FLAT serializer shares it).
- **Hand-written parsers** for ADL2, AOM2, OPT2, ODIN, BMM, and AQL.
  No ANTLR runtime, no scaffolding that needs reflection to walk an
  AST.
- **Closed switches over RM types** for AQL path navigation and
  template-driven validation, in place of `Type.GetProperty(...)`
  lookups.
- **`Regex` usage** is limited to the parser pre-passes (ADL2 / OPT2
  identifier scanners, ODIN comment stripping). `System.Text.RegularExpressions`
  is trim-safe under .NET 10, and we only construct compile-time-string
  patterns so the regex source generator is happy.

## The smoke gate

`tests/DotnetOpenEhr.AotSmoke` is a tiny console app referencing every
shipping package and exercising the primary public surface (parse a
Composition, validate against an OPT, run an AQL query, round-trip
canonical and FLAT JSON, load the RM BMM, etc.).

Run it locally (this is the command the staged-but-disabled CI job uses,
for both **linux-x64** and **win-x64**):

```bash
dotnet publish tests/DotnetOpenEhr.AotSmoke \
    -c Release -r linux-x64 -p:PublishAot=true -p:TreatWarningsAsErrors=true
```

This publishes with the trimmer's `IL2xxx` and the AOT analyzer's
`IL3xxx` diagnostics escalated to errors. The resulting binary is then
executed; a non-zero exit code or a missing `smoke ok` line indicates a
regression.

## Coverage gate

The smoke binary also enumerates `AppDomain.CurrentDomain.GetAssemblies()`
at the end of its run and prints a `coverage:` line listing every
loaded `DotnetOpenEhr.*` assembly. If any shipping assembly fails to
load it fails the run. That keeps the smoke from quietly stagnating as
the SDK grows — you can't add a new shipping project without wiring it
in.

## Supported scenarios under AOT

The smoke explicitly exercises these:

- **Parse** a canonical openEHR JSON Composition (`OpenEhrJson.ParseComposition`).
- **Serialize** the same Composition back out (`OpenEhrJson.Serialize`).
- **Parse** a FLAT openEHR JSON Composition (`OpenEhrFlatJson.ParseComposition`).
- **Validate** a Composition against an OPT2
  (`OperationalTemplateValidator.Validate`).
- **Query** an in-memory list of Compositions with an AQL statement
  (`AqlEvaluator.Evaluate`).
- **Load** the canonical RM BMM (`OpenEhrRmBmm.LoadDefault`).

All of these run from the AOT-published binary with zero trimmer /
analyzer warnings.

## Caveats and deferred work

- **`System.Text.RegularExpressions`** is pulled in by the ADL2 / OPT2
  identifier scanners. It's trim-safe under .NET 10 but is the heaviest
  dependency in the trim graph. If a future profile pass shows it
  dominating the trimmed footprint, the scanners can be hand-rolled and
  the `using` removed.
- **FLAT JSON schemaless mode** uses inline `_type` discriminators and
  the monomorphic-RM lookup table; both are static data. Schema-driven
  mode (`ITemplateSchema`) is also static once the OPT is parsed.
- **No `JsonSerializerOptions` mutation at runtime.** Every JSON entry
  point routes through the source-generated context. If you need to
  customize behaviour, fork the context or extend the typed surface —
  don't reach for `JsonSerializerOptions.AddConverter(...)`.

## See also

- [`getting-started.md`](getting-started.md)
- [`package-map.md`](package-map.md)
- The smoke test project: `tests/DotnetOpenEhr.AotSmoke/`
