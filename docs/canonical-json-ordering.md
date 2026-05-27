# Canonical openEHR JSON: byte-equivalence and ordering

## Background

Phase 3 of the DotnetOpenEhr SDK delivers a lossless round-trip from
the canonical openEHR JSON wire form into the strongly-typed
`DotnetOpenEhr.Rm` Reference Model. The Phase 3 plan called for two
layers of round-trip verification:

1. **Structural equivalence.** Parse → re-serialize → parse → assert
   deep equality of the two object graphs.
2. **Byte-equivalent re-serialize.** After a normalisation pass over
   the input (sort sibling JSON properties by openEHR's documented
   canonical-form ordering rules, applied uniformly to both sides),
   the SDK's output bytes must equal the normalised input bytes
   exactly.

Layer 1 is the implementation-correctness contract and is enforced
unconditionally across every fixture. Layer 2 is the formatting-drift
detector.

## Deviation from the plan

The openEHR specifications include the [`ITS-JSON`
component](https://specifications.openehr.org/releases/ITS-JSON/latest)
which defines the wire shape, but the fully-canonical _ordering_ rules
for sibling JSON properties are not separately published as a
referenceable normative document at the time of writing. Without an
authoritative ordering specification, implementing a faithful canonical
normaliser is a research task, not a coding task.

Phase 3 therefore ships a **pragmatic normaliser**
(`CanonicalJsonNormaliser` in the IntegrationTests project) that
implements three deterministic rules:

1. **Whitespace.** All insignificant whitespace is removed.
2. **Object-key ordering.** Sibling keys inside every JSON object are
   sorted alphabetically using ordinal comparison. The same rule
   applies to the SDK output and to the upstream fixture, so any
   stable ordering produces the same normalised result.
3. **Number formatting.** JSON numbers whose decimal expansion is a
   pure integer are emitted without a decimal point — e.g. `1.0` is
   normalised to `1`. This compensates for `System.Text.Json` rendering
   `double` values with a trailing `.0` when the upstream fixture
   omitted it.

The byte-equivalence integration test runs against the pragmatic
normaliser and is **best-effort**: failures are logged to test
output, not asserted. Structural equivalence remains the gating bar
and is enforced for every fixture.

## What would full byte equivalence require?

Achieving a true byte-equivalent round-trip would need at least the
following on top of the pragmatic rules above:

* **Canonical property order.** Object keys should follow the openEHR
  RM attribute ordering (e.g. `_type`, then `name`, then
  `archetype_node_id`, then specialised attributes in a defined
  sequence) instead of the arbitrary alphabetical ordering used here.
  This is what the upstream openEHR Java reference implementation does
  but the rules are tribal knowledge rather than a published spec.
* **Discriminator inclusion policy.** When `_type` is statically
  inferable from the schema (e.g. a `terminology_id` property whose
  static type is `TERMINOLOGY_ID`), the canonical form is silent on
  whether `_type` must be emitted. Upstream fixtures emit it even
  where it is redundant (every `CODE_PHRASE` carries `_type:
  CODE_PHRASE`). The current SDK omits `_type` on non-polymorphic
  properties because STJ source-gen does not write a discriminator for
  a non-polymorphic concrete type. Closing this gap would require
  custom hand-written converters or marking every value type as
  polymorphic with a single derived entry.
* **Numeric precision policy.** The fixtures contain
  string-typed-but-numeric-looking values (e.g. the `code_string` field
  of a `CODE_PHRASE`). The pragmatic normaliser treats those as
  strings — but a full canonical normaliser would also need to address
  trailing-zero rendering of true JSON numbers
  (`120.000` ↔ `120.0` ↔ `120`).

## Re-visiting in Phase 10

Phase 10 (AOT / trim gate hardening) will re-open this normaliser when
the canonical openEHR ordering rules have been studied in depth. The
intended outcome is to upgrade the byte-equivalence test from
best-effort to **gating** by either (a) hand-coding the RM-attribute
ordering table and a discriminator inclusion policy, or (b) adopting an
authoritative specification if one becomes available before then.
