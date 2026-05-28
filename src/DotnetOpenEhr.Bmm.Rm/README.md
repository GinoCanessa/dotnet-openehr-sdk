# DotnetOpenEhr.Bmm.Rm

Bundled canonical **openEHR Reference Model BMM schemas** (BASE 1.2.0 +
RM 1.1.0), packaged as embedded resources with a typed loader on top of
`DotnetOpenEhr.Bmm`.

## What this package gives you

- `OpenEhrRmBmm.LoadDefault()` — returns a fully merged `BmmModel`
  containing all BASE + RM schemas, parsed once and cached.
- `OpenEhrRmBmm.EmbeddedFileNames` — enumerate the bundled `.bmm`
  resource names.

## Example

```csharp
using DotnetOpenEhr.Bmm.Rm;
using DotnetOpenEhr.Bmm.Schema;

BmmModel rm = OpenEhrRmBmm.LoadDefault();
BmmClass obs = rm.ClassDefinitions["OBSERVATION"];
Console.WriteLine($"OBSERVATION has {obs.Properties.Count} properties");
```

## AOT / trim

Fully AOT- and trim-safe; no runtime reflection. The schemas are
embedded with `EmbeddedResource` and loaded lazily through
`DotnetOpenEhr.Bmm`'s hand-written parser.

## Licensing

This package's **code** is MIT; the embedded `.bmm` files are
redistributed verbatim under **Apache-2.0** from the openEHR
`specifications-ITS-BMM` repository. The NuGet package therefore
publishes under the SPDX expression `MIT AND Apache-2.0`. See
[`THIRD_PARTY_NOTICES.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/src/DotnetOpenEhr.Bmm.Rm/THIRD_PARTY_NOTICES.md)
for upstream attribution and the pinned commit SHA.

## See also

- [`docs/package-map.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/package-map.md)
- [`docs/getting-started.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/getting-started.md)
