# OPT 1.4 XML Fixture Attribution

The `.opt` files in this directory are upstream openEHR Operational
Template (OPT 1.4) XML documents sourced from the
[openFHIR](https://github.com/openFHIR/openfhir) project's test corpus
(Apache-2.0). They are embedded unmodified as test fixtures for
`Opt14XmlParserTests`.

| Local file             | Upstream path inside `openFHIR/openfhir`                              | Commit SHA                                 | License    |
| ---------------------- | --------------------------------------------------------------------- | ------------------------------------------ | ---------- |
| `KDS_Vitalstatus.opt`  | `core/src/test/resources/kds/vitalstatus/KDS_Vitalstatus.opt`         | `1623d6fe71bbed94af52f6a8625a13ce60711fba` | Apache-2.0 |
| `KDS_Diagnose.opt`     | `core/src/test/resources/kds/diagnose/KDS_Diagnose.opt`               | `1623d6fe71bbed94af52f6a8625a13ce60711fba` | Apache-2.0 |
| `KDS_Person.opt`       | `core/src/test/resources/kds/person/KDS_Person.opt`                   | `1623d6fe71bbed94af52f6a8625a13ce60711fba` | Apache-2.0 |
| `Blood Pressure.opt`   | `core/src/test/resources/blood_pressure/Blood Pressure.opt`           | `1623d6fe71bbed94af52f6a8625a13ce60711fba` | Apache-2.0 |

The upstream `LICENSE` is the Apache License, Version 2.0; the
repository-root `LICENSE-Apache-2.0` is the corresponding copy carried
by this SDK.

The three `KDS_*` templates are part of the German
**Medizininformatik-Initiative Kerndatensatz** (KDS) and were authored
by Medizinische Hochschule Hannover (MHH); they are redistributed
through openFHIR under the same Apache-2.0 terms. The `Blood Pressure`
template is the canonical openEHR blood-pressure example, authored via
Better Studio.
