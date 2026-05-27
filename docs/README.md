# DotnetOpenEhr documentation

This folder will collect end-user and contributor documentation as the
SDK comes online phase by phase.

Planned docs (landing alongside the phases that produce them):

- `getting-started.md` — install, parse a Composition, validate against
  an OPT, run an AQL query (Phase 11).
- `package-map.md` — which package to install when (Phase 11; the
  short table is also in the top-level `README.md`).
- `aot.md` — AOT/trim posture and supported scenarios (Phase 11).
- `canonical-json-ordering.md` — the documented sibling-key ordering
  used for byte-equivalent canonical round-trip (Phase 3).

## openEHR specs (source of truth)

The pinned openEHR specs live under `support/` in this repo (gitignored,
local-only). They are the immovable reference for the SDK's behaviour.
A non-exhaustive list:

- Architecture Overview
- Foundation Types
- Data Types Information Model
- Resource Model (RM)
- Archetype Definition Language 2 (ADL2)
- Archetype Object Model 2 (AOM2)
- Operational Template 2 (OPT2)
- Basic Meta-Model (BMM)
- Object Data Instance Notation (ODIN)
- Support Terminology specification
- Archetype Identification
- Archetype Technology Overview
- Archetype Query Language (AQL)
