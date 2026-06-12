# DotnetOpenEhr.Terminology

openEHR **Support Terminology** for .NET — the openEHR-internal
vocabulary groups (`null_flavours`, `audit_change_type`,
`composition_category`, …) shipped as embedded JSON resources with an
AOT-safe lookup API.

## What this package gives you

- `OpenEhrTerminology.GroupIds` — every shipped vocabulary group id.
- `OpenEhrTerminology.GetGroup(string groupId)` — frozen dictionary of
  `code → TerminologyEntry` (with rubric and description).
- `OpenEhrTerminology.TryGetGroup(...)` — non-throwing lookup.
- `OpenEhrTerminology.IsValidCode(groupId, code)` — predicate used by
  RM validation and the template walker.

## Example

```csharp
using DotnetOpenEhr.Terminology;

if (OpenEhrTerminology.TryGetGroup("null_flavours", out var nulls))
{
    foreach ((string code, TerminologyEntry entry) in nulls)
    {
        Console.WriteLine($"{code}: {entry.Rubric}");
    }
}

bool ok = OpenEhrTerminology.IsValidCode("composition_category", "433");
```

## AOT / trim

Fully AOT- and trim-safe; no runtime reflection. JSON groups are parsed
once via a `System.Text.Json` source-generated context and exposed as
`FrozenDictionary` instances.

## See also

- [`docs/package-map.md`](https://github.com/GinoCanessa/dotnet-openehr-sdk/blob/main/docs/package-map.md)
- [`docs/getting-started.md`](https://github.com/GinoCanessa/dotnet-openehr-sdk/blob/main/docs/getting-started.md)
