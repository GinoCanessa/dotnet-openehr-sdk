# Third-Party Notices for `DotnetOpenEhr.Bmm.Rm`

This NuGet package bundles the canonical openEHR Reference Model BMM
schema files, which are governed by their own license. The package's
own code (the embedded-resource loader and public API) is licensed under
MIT (see `LICENSE`). The bundled `.bmm` files are licensed under the
Apache License, Version 2.0 (see `LICENSE-Apache-2.0`). The package's
NuGet `licenseExpression` is the SPDX expression `MIT AND Apache-2.0`.

## Bundled artefacts

Source: <https://github.com/openEHR/specifications-ITS-BMM>
Commit: `0003c592b2a74d6b3af21b0b43b3cc0e5e0ddd3c`
License: Apache License, Version 2.0
SPDX:    `Apache-2.0`

| Embedded resource (under `Resources/`)   | Upstream path                                              |
|------------------------------------------|------------------------------------------------------------|
| `openehr_base_120.bmm`                   | `components/BASE/Release-1.2.0/openehr_base_120.bmm`                   |
| `openehr_base_base_types_120.bmm`        | `components/BASE/Release-1.2.0/openehr_base_base_types_120.bmm`        |
| `openehr_base_foundation_types_120.bmm`  | `components/BASE/Release-1.2.0/openehr_base_foundation_types_120.bmm`  |
| `openehr_base_resource_120.bmm`          | `components/BASE/Release-1.2.0/openehr_base_resource_120.bmm`          |
| `openehr_rm_110.bmm`                     | `components/RM/Release-1.1.0/openehr_rm_110.bmm`                       |
| `openehr_rm_data_types_110.bmm`          | `components/RM/Release-1.1.0/openehr_rm_data_types_110.bmm`            |
| `openehr_rm_demographic_110.bmm`         | `components/RM/Release-1.1.0/openehr_rm_demographic_110.bmm`           |
| `openehr_rm_ehr_110.bmm`                 | `components/RM/Release-1.1.0/openehr_rm_ehr_110.bmm`                   |
| `openehr_rm_ehr_extract_110.bmm`         | `components/RM/Release-1.1.0/openehr_rm_ehr_extract_110.bmm`           |
| `openehr_rm_structures_110.bmm`          | `components/RM/Release-1.1.0/openehr_rm_structures_110.bmm`            |

Each file's original copyright header (referencing openEHR International,
its authors, and the Apache 2.0 license URL) is preserved verbatim in
the embedded resource.

## How this material is used

`OpenEhrRmBmm.LoadDefault()` parses every embedded BMM file into a
`DotnetOpenEhr.Bmm.BmmModel` instance and merges their package and
class definitions into a single resolved model. No upstream source is
modified at build time; the files are shipped byte-for-byte as
distributed by openEHR.

## Reporting upstream issues

Issues or fixes that pertain to the bundled BMM source itself should be
reported to the upstream project at the URL above. Issues that pertain
to how this package loads or exposes the model should be reported in
this repository.
