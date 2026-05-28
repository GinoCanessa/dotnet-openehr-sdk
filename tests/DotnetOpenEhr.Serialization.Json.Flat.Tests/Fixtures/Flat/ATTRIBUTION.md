# FLAT openEHR JSON fixtures — attribution

The JSON FLAT-format Composition fixtures in this directory are
reproduced verbatim from the upstream
[openFHIR](https://github.com/openFHIR/openfhir) project (Apache
License, Version 2.0), where they are used as round-trip test data for
the openFHIR ↔ openEHR mapping engine.

They are used here unmodified as **input only** for the DotnetOpenEhr
FLAT JSON round-trip tests
(`tests/DotnetOpenEhr.Serialization.Json.Flat.Tests/`). The
DotnetOpenEhr SDK does not redistribute or re-license these fixtures;
copies are embedded so the test suite has authentic, third-party-produced
FLAT openEHR JSON to parse.

| Local file                                  | Upstream path inside `openfhir`                                                            |
|---------------------------------------------|--------------------------------------------------------------------------------------------|
| `stu3_blood_pressure_flat.json`             | `core/src/test/resources/stu3_blood_pressure/stu3-blood-pressure_flat.json`                |
| `blood_pressure_flat.json`                  | `core/src/test/resources/blood_pressure/blood-pressure_flat.json`                          |
| `news2_encounter_parent_flat.json`          | `core/src/test/resources/news2/news2_encounter_parent_FLAT.json`                           |
| `medication_order_flat.json`                | `core/src/test/resources/medication_order/medication_order_flat.json`                      |
| `growth_chart_flat.json`                    | `core/src/test/resources/growth_chart/growth_chart_flat.json`                              |
| `kds_prozedur_flat.json`                    | `core/src/test/resources/kds/procedure/KDS_Prozedur.flat.json`                             |
| `kds_person_flat.json`                      | `core/src/test/resources/kds/person/KDS_Person.flat.json`                                  |
| `kds_diagnose_composition_flat.json`        | `core/src/test/resources/kds/diagnose/KDS_Diagnose_Composition.flat.json`                  |
| `kds_fall_einfach_flat.json`                | `core/src/test/resources/kds/fall/KDS_Fall_einfach.flat.json`                              |
| `kds_laborbericht_flat.json`                | `core/src/test/resources/kds/laborbericht/KDS_Laborbericht.flat.json`                      |
| `kds_medikationseintrag_flat.json`          | `core/src/test/resources/kds/medikationseintrag/KDS_Medikationseintrag.flat.json`          |
| `kds_medikamentenverabreichungen_flat.json` | `core/src/test/resources/kds/medikationsverabreichung/KDS_Medikamentenverabreichungen.flat.json` |
| `studienteilnahme_flat.json`                | `core/src/test/resources/kds/studienteilnahme/studienteilnahme.flat.json`                  |

In addition, the hand-authored fixture below is original work shipped
under the DotnetOpenEhr SDK MIT license:

| Local file                                | Origin                                                                                       |
|-------------------------------------------|----------------------------------------------------------------------------------------------|
| `minimal_metadata_flat.json`              | Authored to exercise the schemaless round-trip path (no clinical content).        |

License notice from the openFHIR repository root: `Apache License,
Version 2.0` (`http://www.apache.org/licenses/LICENSE-2.0`).
