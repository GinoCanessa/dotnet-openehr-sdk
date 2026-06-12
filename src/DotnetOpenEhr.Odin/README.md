# DotnetOpenEhr.Odin

Standalone hand-written **ODIN (Object Data Instance Notation)**
parser, AST, and writer for the DotnetOpenEhr SDK. ODIN is the
attribute/object notation used inside ADL2 archetypes, OPT2 templates
and BMM schemas.

## What this package gives you

- `OdinParser.Parse(string)` / `Parse(ReadOnlySpan<char>)` — produces
  an `OdinValue` AST.
- `OdinValue` hierarchy — object, list, hash, primitive, type marker.
- `OdinWriter` — serializes an `OdinValue` back to canonical ODIN text.

## Example

```csharp
using DotnetOpenEhr.Odin;
using DotnetOpenEhr.Odin.Ast;

string text = "name = <\"vital signs\">; revision = <\"1.0.0\">";
OdinValue value = OdinParser.Parse(text);

if (value is OdinObject obj)
{
    string? name = (obj.Attributes["name"] as OdinPrimitive)?.AsString();
}
```

## AOT / trim

Fully AOT- and trim-safe; no runtime reflection. The tokenizer and
parser are hand-written recursive-descent and allocate only AST nodes
plus the small set of strings the document carries.

## See also

- [`docs/package-map.md`](https://github.com/GinoCanessa/dotnet-openehr-sdk/blob/main/docs/package-map.md)
- [`docs/getting-started.md`](https://github.com/GinoCanessa/dotnet-openehr-sdk/blob/main/docs/getting-started.md)
