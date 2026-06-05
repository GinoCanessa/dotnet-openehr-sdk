# Third-party attribution

The JSON files under [`Groups/`](Groups/) are derived from the openEHR
Support Terminology specification.

- **Source:** openEHR Support Terminology specification, TERM
  Release 3.0.0 (STABLE).
- **Spec URL:** <https://specifications.openehr.org/releases/TERM/latest/SupportTerminology.html>
- **Local copy used:** `c:/ai/support/openEHR/Support Terminology specification.html`
  (read on 2026-06-05).
- **License of the source:** Creative Commons Attribution-NoDerivs 3.0
  Unported (<https://creativecommons.org/licenses/by-nd/3.0/>).
- **License of this package:** SPDX `MIT AND Apache-2.0`. The
  spec-derived content is embedded under the same dual-license model as
  the rest of `DotnetOpenEhr.Terminology`, consistent with the
  `DotnetOpenEhr.Bmm.Rm` precedent for openEHR-derived data.

## Per-group provenance

Each JSON file under `Groups/` mirrors one vocabulary table from
section 3.2 of the spec. The `code` and `rubric` columns are copied
verbatim from the spec table; the `description` field carries the same
text the spec lists under its "Description" column (which is the
human-readable rubric for that code).

| Group file                           | Spec section |
|--------------------------------------|--------------|
| `attestation_reason.json`            | 3.2.x (Attestation Reason) |
| `audit_change_type.json`             | 3.2.x (Audit Change Type) |
| `composition_category.json`          | 3.2.x (Composition Category) |
| `event_math_function.json`           | 3.2.13 Event Math Function |
| `instruction_states.json`            | 3.2.9 Instruction States |
| `instruction_transitions.json`       | 3.2.10 Instruction Transitions |
| `null_flavours.json`                 | 3.2.7 Null Flavours |
| `participation_function.json`        | 3.2.x (Participation Function) |
| `participation_mode.json`            | 3.2.8 Participation Mode |
| `property.json`                      | 3.2.x (Property) |
| `setting.json`                       | 3.2.14 Setting |
| `subject_relationship.json`          | 3.2.11 Subject Relationship |
| `term_mapping_purpose.json`          | 3.2.12 Term Mapping Purpose |
| `version_lifecycle_state.json`       | 3.2.x (Version Lifecycle State) |

If the spec is republished with revised rubrics, regenerate these
files in lock-step and update the "read on" date above.
