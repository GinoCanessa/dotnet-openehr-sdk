# DotnetOpenEhr.Bmm

openEHR **Basic Meta-Model (BMM)** object model and parser for .NET.

## What this package gives you

- `BmmParser.Parse(string)` / `Parse(ReadOnlySpan<char>)` — reads a BMM
  schema (ODIN dialect) into a strongly-typed `BmmModel`.
- `BmmModel` — packages, classes, properties, generic parameters, type
  references, ancestor walks.
- `BmmTypeStringParser` — parses a BMM type string (e.g.
  `List<Hash<String,String>>`) into a `BmmType`.

## Example

```csharp
using DotnetOpenEhr.Bmm;
using DotnetOpenEhr.Bmm.Schema;

string source = File.ReadAllText("openehr_rm_110.bmm");
BmmModel model = BmmParser.Parse(source);

BmmClass observation = model.ClassDefinitions["OBSERVATION"];
foreach (BmmProperty p in observation.Properties.Values)
{
    Console.WriteLine($"{p.Name}: {p.TypeRef}");
}
```

## AOT / trim

Fully AOT- and trim-safe; no runtime reflection. The parser is
hand-written recursive-descent on the ODIN tokenizer from
`DotnetOpenEhr.Odin`.

## See also

- `DotnetOpenEhr.Bmm.Rm` — canonical openEHR RM BMM schemas embedded
  and ready to load.
- [`docs/package-map.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/package-map.md)
- [`docs/getting-started.md`](https://github.com/ginoc/dotnet-openehr-sdk/blob/main/docs/getting-started.md)
