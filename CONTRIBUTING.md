# Contributing to DotnetOpenEhr

Thanks for your interest. This repo is in active early development;
contributions, issues, and design feedback are all welcome.

## Local build / test

Requires the **.NET 10 SDK**. `global.json` pins `10.0.108` with
`latestMajor` roll-forward, so any 10.x SDK works.

```pwsh
dotnet --info                                         # expects a 10.x SDK
dotnet build dotnet-openehr-sdk.slnx -c Release
dotnet test  dotnet-openehr-sdk.slnx -c Release --no-build
```

## AOT / trim gate

Every shipping package is trim-safe and Native-AOT-safe. CI publishes
the smoke executable with `PublishAot=true` and treats trim/AOT
warnings as build errors. Run the same publish locally before
submitting changes that touch shipping code:

```pwsh
dotnet publish tests/DotnetOpenEhr.AotSmoke -c Release -r linux-x64 -p:PublishAot=true
# then run the produced binary; it must exit 0 and print "smoke ok".
```

If your change introduces any of `IL2026 / IL2046 / IL2057 / IL3050 /
IL3056` etc., the build fails. Use source generators rather than
runtime reflection / runtime codegen wherever possible.

## Coding conventions

- **C# 14 / .NET 10** only.
- **Explicit types**: use the concrete type, never `var` — enforced by
  `.editorconfig` as a build error.
- **Empty collection initializers** use the collection expression form
  `[]` — also enforced as a build error.
- **File-scoped namespaces**, nullable enabled, warnings-as-errors.
- **`System.Text.Json` source generators** for any serialization in
  shipping code; no `Newtonsoft.Json`.
- **Hand-written parsers** (recursive descent over `ReadOnlySpan<char>`)
  for openEHR text grammars (ADL2, AOM2, OPT2, ODIN, AQL).

## Commit style

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>
```

Common types: `feat`, `fix`, `refactor`, `test`, `chore`, `docs`,
`build`, `ci`, `perf`. Subject is imperative, ≤ 72 chars. Scope is
optional but encouraged (the package name is a good default).

All commits should include the standard co-author trailer:

```
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

## License notes

The repository uses a **hybrid license stack**; please match the rule
that applies to where you are working:

- **`src/` source code** is **MIT-only**. Any new code you write in
  `src/` must be MIT-licensable and contributed under MIT.
- **Bundled openEHR specification artefacts** live under
  `src/DotnetOpenEhr.Bmm.Rm/Resources/` and ship under the
  [Apache License 2.0](LICENSE-Apache-2.0) as redistributions of
  upstream openEHR specification material. The containing package,
  `DotnetOpenEhr.Bmm.Rm`, declares the SPDX expression
  `MIT AND Apache-2.0` and ships
  `src/DotnetOpenEhr.Bmm.Rm/THIRD_PARTY_NOTICES.md` plus the top-level
  [`NOTICE`](NOTICE) file. **If you update the bundled BMM files**,
  bump the pinned upstream commit SHA in both the `NOTICE` and the
  package's `THIRD_PARTY_NOTICES.md`, and keep the SPDX expression and
  Apache-2.0 attribution intact.
- **New bundled spec artefacts** (e.g. additional openEHR schemas
  redistributed verbatim) must follow the same pattern: SPDX-declared
  dual license on the containing package, a `THIRD_PARTY_NOTICES.md`
  alongside the resources, and an entry in the top-level `NOTICE`.
- **Test fixtures** under `tests/**/Fixtures/` may be **CC-BY-SA 3.0**
  (openEHR CKM artefacts and openEHR JSON samples copied from
  upstream); when they are, the fixture directory carries an
  `ATTRIBUTION.md` listing the source URL, identifier/version, and
  license. **Do not copy fixture material into `src/`.**
