# OPT2 Fixture Attribution

The `.opt2` files in this directory are hand-authored by the
`dotnet-openehr-sdk` project for use as parser-test fixtures only.
They do not derive from any third-party source.

| File | Origin | Notes |
| --- | --- | --- |
| `minimal_vitals.opt2` | hand-authored | Minimal OPT2 with one OBSERVATION → HISTORY → POINT_EVENT → ITEM_TREE → ELEMENT → DV_QUANTITY, plus one `component_terminologies` entry. |
| `report_composition.opt2` | hand-authored | OPT2 wrapping a COMPOSITION with one OBSERVATION (notes) and one SECTION, plus two `component_terminologies` entries. |

If real-world OPT2 fixtures from openEHR / archie are added later, list
their upstream source, commit SHA, and license here.
