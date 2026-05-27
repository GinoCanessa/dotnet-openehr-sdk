// DotnetOpenEhr AOT/trim smoke test.
//
// This executable is published with PublishAot=true in CI. Its only
// job is to exercise the publishable SDK surface end-to-end so that
// any new trim/AOT warning fails the build. Phases 1..9 each extend
// this file as their packages come online; Phase 0 ships only the
// console banner so the gate is alive from day one.

using System.IO;
using System.Reflection;
using System.Text.Json;
using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Support;
using DotnetOpenEhr.Serialization.Json;

IsoDate date = IsoDate.Parse("2024-05-27");
IsoDateTime dt = IsoDateTime.Parse("2024-05-27T10:25:03Z");
IsoDuration dur = IsoDuration.Parse("P1Y2M3DT4H5M6.789S");

System.Console.WriteLine($"foundation: {date} / {dt} / {dur}");

DvQuantity sbp = new(120, "mm[Hg]");
Element systolic = new()
{
    Name = new DvText("Systolic"),
    ArchetypeNodeId = "at0004",
    Value = sbp,
};

ItemTree tree = new()
{
    Name = new DvText("blood_pressure_data"),
    ArchetypeNodeId = "at0003",
    Items = [systolic],
};

PointEvent point = new()
{
    Name = new DvText("any event"),
    ArchetypeNodeId = "at0006",
    Time = new DvDateTime(dt),
    Data = tree,
};

History history = new()
{
    Name = new DvText("history"),
    ArchetypeNodeId = "at0002",
    Origin = new DvDateTime(dt),
    Events = [point],
};

Observation obs = new()
{
    Name = new DvText("Blood pressure"),
    ArchetypeNodeId = "openEHR-EHR-OBSERVATION.blood_pressure.v2",
    Language = new CodePhrase(new TerminologyId { Value = "ISO_639-1" }, "en"),
    Encoding = new CodePhrase(new TerminologyId { Value = "IANA_character-sets" }, "UTF-8"),
    Data = history,
};

System.Console.WriteLine($"obs: {obs.ArchetypeNodeId}");
System.Console.WriteLine($"quantity: {sbp}");
System.Console.WriteLine($"registry entries: {RmTypeName.AllRmNames.Count}");

// Phase 3: parse a canonical fixture and re-serialize, fully AOT-safe
// via the STJ source generator (no runtime reflection).
Assembly asm = typeof(Program).Assembly;
using Stream fixtureStream = asm.GetManifestResourceStream("kds_procedure_bundle.json")
    ?? throw new System.InvalidOperationException("Embedded fixture missing.");
using MemoryStream ms = new();
fixtureStream.CopyTo(ms);
Composition? roundTripped = OpenEhrJson.ParseComposition(ms.ToArray());
if (roundTripped is null) throw new System.InvalidOperationException("Fixture parse returned null.");
byte[] reEmitted = OpenEhrJson.Serialize(roundTripped);
System.Console.WriteLine($"json round-trip: archetype={roundTripped.ArchetypeNodeId} bytes={reEmitted.Length}");

// Phase 4: parse a FLAT JSON fixture, re-emit as FLAT, and re-parse to
// confirm the schemaless façade is AOT-safe (no reflection, all STJ
// source-gen).
using Stream flatStream = asm.GetManifestResourceStream("minimal_metadata_flat.json")
    ?? throw new System.InvalidOperationException("Embedded FLAT fixture missing.");
using MemoryStream flatMs = new();
flatStream.CopyTo(flatMs);
byte[] flatBytes = flatMs.ToArray();
Composition? flatParsed = DotnetOpenEhr.Serialization.Json.Flat.OpenEhrFlatJson.ParseComposition(flatBytes);
if (flatParsed is null) throw new System.InvalidOperationException("FLAT parse returned null.");
byte[] flatReEmitted = DotnetOpenEhr.Serialization.Json.Flat.OpenEhrFlatJson.Serialize(flatParsed, "minimal");
Composition? flatRoundTripped = DotnetOpenEhr.Serialization.Json.Flat.OpenEhrFlatJson.ParseComposition(flatReEmitted);
if (flatRoundTripped is null) throw new System.InvalidOperationException("FLAT re-parse returned null.");
using JsonDocument flatDoc = JsonDocument.Parse(flatReEmitted);
int flatKeyCount = 0;
foreach (JsonProperty _ in flatDoc.RootElement.EnumerateObject()) flatKeyCount++;
System.Console.WriteLine($"flat round-trip: keys={flatKeyCount}");

// Phase 5: parse and re-serialize an ODIN snippet end-to-end (lexer,
// parser, writer all hand-written and AOT-safe).
const string odinSrc = "<[\"en\"] = (RESOURCE_DESCRIPTION_ITEM) <language = <[ISO_639-1::en]> purpose = <\"demo\">>>";
DotnetOpenEhr.Odin.OdinValue odinParsed = DotnetOpenEhr.Odin.OdinParser.Parse(odinSrc);
string odinCompact = DotnetOpenEhr.Odin.OdinWriter.Write(odinParsed, DotnetOpenEhr.Odin.OdinWriteOptions.Compact);
DotnetOpenEhr.Odin.OdinValue odinReparsed = DotnetOpenEhr.Odin.OdinParser.Parse(odinCompact);
DotnetOpenEhr.Odin.Values.OdinHash odinHash = odinReparsed.AsHash();
System.Console.WriteLine($"odin parse ok: keys={odinHash.Entries.Count}");

// Phase 6: terminology lookup via the embedded resource + STJ source-gen
// pipeline, and a small BMM fragment parse end-to-end.
bool nullFlavour253 = DotnetOpenEhr.Terminology.OpenEhrTerminology.IsValidCode("null_flavours", "253");
System.Console.WriteLine($"terminology: null_flavours[253]={nullFlavour253.ToString().ToLowerInvariant()}");

const string bmmFragment = """
    bmm_version = <"2.1">
    model_name = <"smoke">
    class_definitions = <
        ["LOCATABLE"] = <
            name = <"LOCATABLE">
            is_abstract = <True>
        >
        ["PARTY"] = <
            name = <"PARTY">
            ancestors = <"LOCATABLE">
            properties = <
                ["name"] = <
                    name = <"name">
                    type = <"String">
                >
            >
        >
    >
    """;
DotnetOpenEhr.Bmm.BmmModel bmm = DotnetOpenEhr.Bmm.BmmParser.Parse(bmmFragment);
System.Console.WriteLine($"bmm parse ok: classes={bmm.ClassDefinitions.Count}");

// Phase 7a: load the bundled canonical openEHR RM BMM and report the
// merged concrete-class count. Exercises the embedded-resource loader
// plus the BMM parser's container/generic type_def support.
DotnetOpenEhr.Bmm.BmmModel rmBmm = DotnetOpenEhr.Bmm.Rm.OpenEhrRmBmm.LoadDefault();
System.Console.WriteLine($"rm-bmm: classes={rmBmm.ClassDefinitions.Count}");

System.Console.WriteLine("smoke ok");
return 0;


