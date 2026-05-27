// DotnetOpenEhr AOT/trim smoke test.
//
// This executable is published with PublishAot=true in CI. Its only
// job is to exercise the publishable SDK surface end-to-end so that
// any new trim/AOT warning fails the build. Phases 1..9 each extend
// this file as their packages come online; Phase 0 ships only the
// console banner so the gate is alive from day one.

using System.IO;
using System.Reflection;
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

System.Console.WriteLine("smoke ok");
return 0;


