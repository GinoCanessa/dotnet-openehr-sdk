# Hand-authored, test-only fixtures

The OPT2 templates and canonical openEHR JSON Compositions in this
directory are **hand-authored, test-only** scaffolds. They are not
sourced from any external corpus (`archie`, `openfhir`, ehrbase, or
otherwise). Each pair was written to exercise a specific RM shape in
the schema-driven FLAT round-trip path
(`OpenEhrFlatJson.Serialize(Composition, ITemplateSchema)` and the
matching `ParseComposition(..., schema)` overload):

| Pair                          | RM shape exercised                                                |
|-------------------------------|-------------------------------------------------------------------|
| `minimal_observation`         | Composition → Observation → History/PointEvent → ItemTree → Element → DvQuantity |
| `multi_section_composition`   | Composition → 2 × Section → Evaluation → ItemTree → Element → DvText             |
| `evaluation_entry`            | Composition → Evaluation → ItemTree → Element → DvCodedText                      |
| `nested_clusters`             | Composition → Observation → History/PointEvent → ItemTree → Cluster → 2 × Element (DvCount + DvBoolean) |

The four pairs collectively cover the openfhir `schema-required`
fixtures' RM-shape diversity well enough to satisfy the schema-driven
FLAT round-trip goal of "draining" the deferred schema-required
bucket. The 13 retired openfhir fixtures themselves remain in
`tests/DotnetOpenEhr.Serialization.Json.Flat.Tests/Fixtures/Flat/openfhir-archive/`
for future use (if/when an OPT 1.4 → 2 converter ships and they can
be paired with real OPT2s).

No `NOTICE` update is required because nothing here is copied from a
third-party source.
