# DotnetOpenEhr (umbrella metapackage)

`DotnetOpenEhr` is a **convenience metapackage** that depends on every
`DotnetOpenEhr.*` shipping package at the same version. Installing it
gives you the whole SDK in one `dotnet add package` call.

```bash
dotnet add package DotnetOpenEhr
```

That brings in, at the same version:

- `DotnetOpenEhr.Foundation`
- `DotnetOpenEhr.Terminology`
- `DotnetOpenEhr.Odin`
- `DotnetOpenEhr.Bmm`
- `DotnetOpenEhr.Bmm.Rm`
- `DotnetOpenEhr.Rm`
- `DotnetOpenEhr.Serialization.Json`
- `DotnetOpenEhr.Serialization.Json.Flat`
- `DotnetOpenEhr.Archetypes`
- `DotnetOpenEhr.Templates.Abstractions`
- `DotnetOpenEhr.Templates`
- `DotnetOpenEhr.Aql`

## Should I install the umbrella or pick packages?

If you are **exploring** the SDK or writing a sample, install the
umbrella. If you are **shipping an app** and care about trim size /
NuGet graph cleanliness, install only the packages you actually use —
the per-package READMEs and
[`docs/package-map.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/package-map.md)
explain which one solves which problem.

## AOT / trim

This metapackage contains no compiled output of its own. Every
referenced package is fully AOT- and trim-safe; see
[`docs/aot.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/aot.md).

## Licensing

This metapackage is MIT-licensed. The pulled-in `DotnetOpenEhr.Bmm.Rm`
ships under the SPDX expression `MIT AND Apache-2.0` because it embeds
the canonical openEHR Reference Model BMM schemas; see that package's
[`THIRD_PARTY_NOTICES.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/src/DotnetOpenEhr.Bmm.Rm/THIRD_PARTY_NOTICES.md).
All other shipping packages are MIT-only.

## See also

- [`docs/getting-started.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/getting-started.md)
- [`docs/package-map.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/package-map.md)
- [`docs/aot.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/aot.md)
