# openfhir FLAT archive

The 13 FLAT JSON fixtures in this directory were originally sourced
from the [openfhir](https://github.com/openehr/openfhir) test corpus
and lived directly under `Fixtures/Flat/` while the schemaless FLAT
parser was being developed. Each one carries clinical
content that the schemaless parser cannot resolve without an OPT —
parsing them raised `FlatSchemaRequiredException` with the unresolved
paths catalogued in `Fixtures/Flat/lossless-catalogue.json`.

The `schema-required` catalogue bucket has been retired: the
schema-driven FLAT round-trip is now exercised against hand-authored
OPT2 + Composition pairs under
`Fixtures/FlatSchemaDriven/` instead. openfhir ships only OPT 1.4 XML
templates, not OPT2, and this repo deliberately avoids depending on
an unverified OPT 1.4 → 2 converter.

These files are retained here for **provenance** so they can be
brought back into active testing if/when a vetted OPT 1.4 → 2
converter ships and the schemaless serializer can reactivate them.

No tests embed or load these archived files at runtime; only the
`FlatSchemaDrivenRoundTripTests` provenance check asserts they still
exist on disk.
