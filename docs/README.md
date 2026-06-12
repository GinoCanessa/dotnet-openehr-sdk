# DotnetOpenEhr documentation

This folder contains end-user and contributor documentation for the SDK.

Docs:

- [`getting-started.md`](getting-started.md) — install, parse a
  Composition, validate against an OPT, run an AQL query.
- [`package-map.md`](package-map.md) — which package to install when
  (the short table is also in the top-level `README.md`).
- [`aot.md`](aot.md) — AOT/trim posture and supported scenarios.
- [`canonical-json-ordering.md`](canonical-json-ordering.md) — the
  documented sibling-key ordering used for byte-equivalent canonical
  round-trip.

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

## Architecture decisions

ADRs live under [`architecture/`](architecture/) and record
non-trivial, durable design decisions.

- [`0001-no-dvordered-crtp-cascade.md`](architecture/0001-no-dvordered-crtp-cascade.md)
  — `ReferenceRange.Range` stays `Interval<DvOrdered>?`; the
  `DvOrdered<T>` CRTP cascade is permanently deferred.
