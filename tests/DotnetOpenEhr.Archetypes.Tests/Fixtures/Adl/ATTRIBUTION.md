# ADL2 Test Fixtures — Attribution

The `*.adls` files in this directory are sourced from the openEHR
[`archie`](https://github.com/openEHR/archie) reference implementation
test corpus, pinned at commit
[`45861a6e038fd831fecd6faaff25f4f663bc9170`](https://github.com/openEHR/archie/tree/45861a6e038fd831fecd6faaff25f4f663bc9170).

These archetypes are licensed under the
[Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0)
as part of the archie project. They are used here **for testing only**
and are not redistributed as part of any shipping NuGet package
produced by this SDK.

| File | Source path in archie |
| --- | --- |
| `openEHR-EHR-OBSERVATION.blood_pressure.v2.0.0.adls` | `tools/src/test/resources/com/nedap/archie/flattener/openEHR-EHR-OBSERVATION.blood_pressure.v2.0.0.adls` |
| `openEHR-EHR-OBSERVATION.body_weight.v1.0.0.adls` | `tools/src/test/resources/ckm-mirror/local/archetypes/entry/observation/openEHR-EHR-OBSERVATION.body_weight.v1.0.0.adls` |
| `openEHR-EHR-OBSERVATION.internal_value_set.v1.0.0.adls` | `tools/src/test/resources/adl2-tests/features/terminology/value_sets/openEHR-EHR-OBSERVATION.internal_value_set.v1.0.0.adls` |

To refresh against a newer upstream snapshot, update the commit SHA and
re-download each file via raw GitHub URLs of the form:

```
https://raw.githubusercontent.com/openEHR/archie/<sha>/<source-path>
```
