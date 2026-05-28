# DotnetOpenEhr.Foundation

openEHR **Foundation Types** for .NET — the value types every other
`DotnetOpenEhr.*` package builds on.

## What this package gives you

- `IsoDate`, `IsoTime`, `IsoDateTime`, `IsoDuration`, `IsoTimeZone` —
  ISO 8601 value types that preserve the original lexical form so
  canonical round-trip is byte-equivalent.
- `Interval<T>` — generic open/closed interval with `Contains`,
  `Intersect`, and equality.
- `Cardinality` — list cardinality (lower/upper, ordered, unique).
- `TerminologyCode` — `(terminology_id, code_string)` carrier used
  across the RM and templates.

## Example

```csharp
using DotnetOpenEhr.Foundation;
using DotnetOpenEhr.Foundation.Iso;

IsoDateTime when = IsoDateTime.Parse("2026-02-14T09:30:00+10:00");
Interval<int> range = Interval<int>.Closed(1, 10);
bool hit = range.Contains(7);     // true

TerminologyCode code = new("local", "at0001");
```

## AOT / trim

Fully AOT- and trim-safe; no runtime reflection. All ISO parsing is
hand-written and allocates only the resulting value object.

## See also

- [`docs/package-map.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/package-map.md)
  — pick the right package for the job.
- [`docs/getting-started.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/getting-started.md)
  — install, parse, validate, query.
