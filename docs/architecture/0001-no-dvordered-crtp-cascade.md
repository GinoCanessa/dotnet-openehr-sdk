# ADR 0001 — Do not pursue a `DvOrdered<T>` CRTP cascade

- **Status:** Accepted (2026-06-05)
- **Deciders:** SDK maintainers
- **Tags:** rm, foundation, json, types

## Context

`DvOrdered` is the openEHR Reference Model (RM) base type for ordered
data values: `DvQuantity`, `DvCount`, `DvOrdinal`, `DvScale`,
`DvDateTime`, `DvDate`, `DvTime`, `DvDuration`, `DvProportion`, and
their kin. `ReferenceRange.Range` is the canonical container that
references a `DvOrdered` instance through a typed interval — modelled
today as `Interval<DvOrdered>?`.

Polymorphic serialization of `Interval<DvOrdered>` already works under
runtime reflection and trim-safe paths via
`IntervalJsonConverter<T>` plus a `_type` discriminator on the
inner `DvOrdered` payload (see
`src/DotnetOpenEhr.Foundation/IntervalJsonConverter.cs` and
`src/DotnetOpenEhr.Rm/Json/DvOrderedJsonConverter.cs`). The AOT smoke
test (`tests/DotnetOpenEhr.AotSmoke`) confirms the round-trip survives
`PublishAot=true`.

Two earlier sweeps (`scratch/0604-03/plan.md` Phase 3 deviation and
the `scratch/0604-04/featurerequest.md` Theme F first bullet) raised
the question of whether `ReferenceRange.Range` should be re-typed as
`ReferenceRange<T>` where `T : DvOrdered<T>` — a CRTP cascade — so the
range's element type becomes statically known at every call site.
That refactor would touch roughly twelve RM types (every `DvOrdered`
subtype plus their consumers, `ReferenceRange`, the BMM/OPT/FLAT
serializers that name the type, and the AQL evaluator's order-aware
operators). It would deliver zero behaviour change, zero new
diagnostic value, and zero serialization difference: the wire format
is fixed by the openEHR specification and is already correct.

## Decision

We will **not** pursue the `DvOrdered<T>` / `ReferenceRange<T>` CRTP
cascade.

The decision is **permanent** in the sense that it should not be
revisited without a fundamentally new motivating force — for example,
a new RM language feature, a new openEHR-mandated serialization that
requires statically-typed ranges, or a measured downstream performance
problem that this refactor would actually solve.

## Consequences

### Positive

- `ReferenceRange.Range` stays `Interval<DvOrdered>?`. Consumers
  continue to pattern-match or cast against `DvOrdered` subtypes the
  same way the rest of the SDK already does for `Element.Value`,
  `Composition.Content`, etc.
- The closed JSON-serializer story for `Interval<T>` over RM types
  remains unchanged: `IntervalJsonConverterFactory` keeps a
  reflection-fallback path for `T`s defined outside
  `DotnetOpenEhr.Foundation` (concretely: `DvDateTime`, `DvOrdered`).
  That fallback is documented in
  `src/DotnetOpenEhr.Foundation/IntervalJsonConverter.cs` with a
  narrowly-scoped `[UnconditionalSuppressMessage]` and is exercised
  by the AOT smoke test.
- The public surface of `DotnetOpenEhr.Rm` does not gain a sea of
  generic parameters that every consumer must thread through.

### Negative

- `ReferenceRange.Range`'s element type is not statically expressible
  at the API. Consumers that want, say, `Interval<DvQuantity>` must
  cast or pattern-match. This matches the situation for every other
  RM polymorphic container today, so the asymmetry cost is zero, but
  the API does not get *stronger* either.
- The `Interval<T>` AOT story keeps one reflection site
  (`IntervalJsonConverterFactory.CreateConverter` fall-through for
  RM-side `T`s). Phase 11 of `scratch/0604-04/plan.md` narrows the
  suppression scope to that fallback only and pins the contract with
  unit tests; the reflection site cannot be removed without a
  separate architectural change to either (a) split the factory
  across Foundation and RM or (b) drop the `[JsonConverter]`
  attribute on `Foundation.Interval<T>` and require every consumer
  to register both factories. Both options were considered and
  rejected as too large for a hardening sweep.

## Alternatives Considered

- **Full CRTP cascade.** Rewrite `DvOrdered` as
  `DvOrdered<TSelf> where TSelf : DvOrdered<TSelf>` and re-type
  `ReferenceRange` as `ReferenceRange<T>`. Rejected for the reasons
  in **Context**: zero behaviour change at twelve-type churn cost.
- **Partial cascade — type only `ReferenceRange<T>`.** Equally
  invasive at the consumer level (every callsite must name `T`)
  without the CRTP self-bound's only theoretical benefit (statically
  enforcing that mixed-type ranges are impossible).
- **Split `IntervalJsonConverterFactory` so the closed set covers
  RM types too.** This is the AOT-purist alternative discussed under
  Phase 11's "Option C" in `scratch/0604-04/plan.md`. Rejected for
  this sweep because it removes the `[JsonConverter]` attribute from
  `Foundation.Interval<T>` and forces every consuming
  `JsonSerializerOptions` to register both factories — a public
  surface change. Filed as an Open Question for a future slot.

## References

- `scratch/0604-03/plan.md` — Phase 3 deviation (first raised the
  CRTP question).
- `scratch/0604-04/featurerequest.md` — Theme F first bullet
  ("`DvOrdered<T>` permanent deferral, with ADR").
- `scratch/0604-04/plan.md` — Phase 1 (this ADR) and Phase 11
  (`IntervalJsonConverter` hybrid dispatch).
- `src/DotnetOpenEhr.Foundation/IntervalJsonConverter.cs` —
  factory + converter + suppression attributes.
- `src/DotnetOpenEhr.Rm/Json/DvOrderedJsonConverter.cs` — `_type`
  discriminator for the inner payload.
- `tests/DotnetOpenEhr.AotSmoke/` — the cross-assembly AOT gate.
