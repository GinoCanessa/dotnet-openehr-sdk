# temporal_and_null fixture — attribution

This fixture is hand-authored for the M26 (0604-04) task: exercise the
`AssertDataValue` switch arms for `DvDate`, `DvTime`, `DvDuration`
that were added in 0604-03 Phase 11 (dead-weight without a fixture)
and pin the `Element.NullFlavour` comparison contract.

- **License:** MIT AND Apache-2.0 (matches the repository SPDX).
- **Spec basis:** openEHR Data Structures Information Model
  (Section 4.2.3, ELEMENT class) for `value` / `null_flavour`
  semantics, and Data Types (DV_DATE / DV_TIME / DV_DURATION) for the
  leaf types. Local copy:
  `c:/ai/support/openEHR/Data Structures Information Model.html`,
  `c:/ai/support/openEHR/Data Types Information Model.html`.
- **Null-flavour code:** `openehr::253` (`unknown`) per the openEHR
  Support Terminology specification's `null_flavours` group.
