# Canonical openEHR JSON fixtures — attribution

The JSON Composition fixtures in this directory are reproduced verbatim
from the upstream [openFHIR](https://github.com/openFHIR/openfhir)
project (Apache License, Version 2.0), where they are used as
round-trip test data for the openFHIR ↔ openEHR mapping engine.

They are used here unmodified as **input only** for the DotnetOpenEhr
canonical / STRUCTURED JSON round-trip integration tests
(`tests/DotnetOpenEhr.IntegrationTests/`). The DotnetOpenEhr SDK does
not redistribute or re-license these fixtures; copies are embedded so
the integration test suite has authentic, third-party-produced
canonical openEHR JSON to parse and serialise.

| Local file                                        | Upstream path inside `openfhir`                                                                                          |
|---------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------|
| `growth_chart.json`                               | `core/src/test/resources/growth_chart/growth_chart_composition.json`                                                     |
| `kds_diagnose_patient1.json`                      | `core/src/test/resources/kds/diagnose/toOpenEHR/output/Composition-mii-exa-test-data-patient-1-diagnose-1.json`          |
| `kds_fall_patient1.json`                          | `core/src/test/resources/kds/fall/toOpenEHR/output/Composition-mii-exa-test-data-patient-1-encounter-1.json`             |
| `kds_laborbericht_patient1.json`                  | `core/src/test/resources/kds/laborbericht/toOpenEHR/output/Composition-mii-exa-test-data-patient-1-labreport-1.json`     |
| `kds_medikationseintrag_patient1.json`            | `core/src/test/resources/kds/medikationseintrag/toOpenEHR/output/Composition-mii-exa-test-data-patient-1-medstatement-1.json` |
| `kds_medikationsverabreichung_patient1.json`      | `core/src/test/resources/kds/medikationsverabreichung/toOpenEHR/output/Composition-mii-exa-test-data-patient-1-medadmin-1.json` |
| `kds_person_patient1.json`                        | `core/src/test/resources/kds/person/toOpenEHR/output/Composition-mii-exa-test-data-patient-1.json`                       |
| `kds_procedure_bundle.json`                       | `core/src/test/resources/kds/procedure/toOpenEHR/output/Composition-KDS_Prozedur_bundle.json`                            |

License notice from the openFHIR repository root: `Apache License,
Version 2.0` (`http://www.apache.org/licenses/LICENSE-2.0`).
