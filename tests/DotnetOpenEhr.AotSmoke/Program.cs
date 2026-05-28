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

// Phase 7f: parse a tiny inline ADL2 archetype and cross-validate against
// the canonical RM BMM. Confirms the validator + the rest of the pipeline
// publish cleanly under PublishAot.
const string adlSrc = """
    archetype (adl_version=2.0.6; rm_release=1.1.0)
        openEHR-EHR-OBSERVATION.minimal.v1.0.0

    language
        original_language = <[ISO_639-1::en]>

    description
        lifecycle_state = <"unmanaged">

    definition
        OBSERVATION[id1] matches { }

    terminology
        term_definitions = <
            ["en"] = <
                ["id1"] = <
                    text = <"Minimal observation">
                    description = <"Minimal observation">
                >
            >
        >
    """;
DotnetOpenEhr.Archetypes.Aom2.Archetype parsedArchetype =
    DotnetOpenEhr.Archetypes.Adl2.Adl2Parser.Parse(adlSrc);
DotnetOpenEhr.Archetypes.Validation.ArchetypeBmmValidator archetypeValidator = new();
System.Collections.Generic.IReadOnlyList<DotnetOpenEhr.Archetypes.Validation.ArchetypeIssue> archetypeIssues =
    archetypeValidator.Validate(parsedArchetype, rmBmm);
int archetypeErrors = 0;
foreach (DotnetOpenEhr.Archetypes.Validation.ArchetypeIssue issue in archetypeIssues)
{
    if (issue.Severity == DotnetOpenEhr.Archetypes.Validation.ArchetypeIssueSeverity.Error)
    {
        archetypeErrors++;
    }
}
System.Console.WriteLine($"archetype: {parsedArchetype.ArchetypeId} issues={archetypeErrors}");

// Phase 8a: parse a tiny inline OPT2 and report its node count. Confirms
// the Opt2Parser + concrete OperationalTemplate publish cleanly under
// PublishAot (component_terminologies extraction is pre-pass + ODIN).
const string opt2Src = """
    operational_template (adl_version=2.0.6; rm_release=1.1.0; generated)
        openEHR-EHR-OBSERVATION.aot_smoke.v1.0.0

    language
        original_language = <[ISO_639-1::en]>

    description
        lifecycle_state = <"unmanaged">

    definition
        OBSERVATION[id1] matches {
            data matches {
                HISTORY[id2] matches {
                    events matches {
                        POINT_EVENT[id3] matches {
                            data matches {
                                ITEM_TREE[id4] matches {
                                    items matches {
                                        ELEMENT[id5] matches {
                                            value matches {
                                                DV_TEXT[id6]
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

    terminology
        term_definitions = <
            ["en"] = <
                ["id1"] = <text = <"AOT smoke"> description = <"AOT smoke">>
            >
        >

    component_terminologies
        component_terminologies = <
            ["openEHR-EHR-OBSERVATION.aot_smoke.v1.0.0"] = <
                term_definitions = <
                    ["en"] = <
                        ["id1"] = <text = <"AOT smoke"> description = <"AOT smoke">>
                    >
                >
            >
        >
    """;
DotnetOpenEhr.Templates.OperationalTemplate opt2 =
    DotnetOpenEhr.Templates.Opt2Parser.Parse(opt2Src);
System.Console.WriteLine($"opt2: {opt2.ArchetypeId} nodes={opt2.Nodes.Count}");

// Phase 8d: schema-driven FLAT round-trip. Build a tiny COMPOSITION OPT2
// + matching Composition, FLAT-serialise using the template as schema,
// re-parse, and report the FLAT key count + a structural sanity check.
const string flatSchemaSrc = """
    operational_template (adl_version=2.0.6; rm_release=1.1.0; generated)
        openEHR-EHR-COMPOSITION.aot_flat_smoke.v1.0.0

    language
        original_language = <[ISO_639-1::en]>

    description
        lifecycle_state = <"unmanaged">

    definition
        COMPOSITION[id1] matches {
            content matches {
                OBSERVATION[id2] occurrences matches {0..1} matches {
                    data matches {
                        HISTORY[id3] matches {
                            events matches {
                                POINT_EVENT[id4] occurrences matches {0..1} matches {
                                    data matches {
                                        ITEM_TREE[id5] matches {
                                            items matches {
                                                ELEMENT[id6] matches {
                                                    value matches {
                                                        DV_TEXT[id7]
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

    terminology
        term_definitions = <
            ["en"] = <
                ["id1"] = <text = <"AOT flat smoke"> description = <"AOT flat smoke">>
                ["id2"] = <text = <"observation"> description = <"observation">>
                ["id3"] = <text = <"history"> description = <"history">>
                ["id4"] = <text = <"event"> description = <"event">>
                ["id5"] = <text = <"tree"> description = <"tree">>
                ["id6"] = <text = <"element"> description = <"element">>
                ["id7"] = <text = <"value"> description = <"value">>
            >
        >

    component_terminologies
        component_terminologies = <
            ["openEHR-EHR-COMPOSITION.aot_flat_smoke.v1.0.0"] = <
                term_definitions = <
                    ["en"] = <
                        ["id1"] = <text = <"AOT flat smoke"> description = <"AOT flat smoke">>
                    >
                >
            >
        >
    """;
DotnetOpenEhr.Templates.OperationalTemplate flatTemplate =
    DotnetOpenEhr.Templates.Opt2Parser.Parse(flatSchemaSrc);

DotnetOpenEhr.Rm.Composition.Composition flatComp = new()
{
    Name = new DotnetOpenEhr.Rm.DataTypes.Text.DvText(flatTemplate.TemplateId),
    ArchetypeNodeId = "openEHR-EHR-COMPOSITION.aot_flat_smoke.v1.0.0",
    Language = new DotnetOpenEhr.Rm.DataTypes.Text.CodePhrase
    {
        TerminologyId = new DotnetOpenEhr.Rm.Support.TerminologyId { Value = "ISO_639-1" },
        CodeString = "en",
    },
    Territory = new DotnetOpenEhr.Rm.DataTypes.Text.CodePhrase
    {
        TerminologyId = new DotnetOpenEhr.Rm.Support.TerminologyId { Value = "ISO_3166-1" },
        CodeString = "GB",
    },
    Category = new DotnetOpenEhr.Rm.DataTypes.Text.DvCodedText
    {
        Value = "event",
        DefiningCode = new DotnetOpenEhr.Rm.DataTypes.Text.CodePhrase
        {
            TerminologyId = new DotnetOpenEhr.Rm.Support.TerminologyId { Value = "openehr" },
            CodeString = "433",
        },
    },
    Composer = new DotnetOpenEhr.Rm.Common.PartyIdentified { Name = "AOT" },
    Content =
    [
        new DotnetOpenEhr.Rm.Composition.Observation
        {
            Name = new DotnetOpenEhr.Rm.DataTypes.Text.DvText("observation"),
            ArchetypeNodeId = "id2",
            Subject = new DotnetOpenEhr.Rm.Common.PartySelf(),
            Data = new DotnetOpenEhr.Rm.DataStructures.History
            {
                Name = new DotnetOpenEhr.Rm.DataTypes.Text.DvText("history"),
                ArchetypeNodeId = "id3",
                Origin = new DotnetOpenEhr.Rm.DataTypes.DateTime.DvDateTime(
                    DotnetOpenEhr.Foundation.Iso.IsoDateTime.Parse("2024-08-23T08:15:00Z")),
                Events =
                [
                    new DotnetOpenEhr.Rm.DataStructures.PointEvent
                    {
                        Name = new DotnetOpenEhr.Rm.DataTypes.Text.DvText("event"),
                        ArchetypeNodeId = "id4",
                        Time = new DotnetOpenEhr.Rm.DataTypes.DateTime.DvDateTime(
                            DotnetOpenEhr.Foundation.Iso.IsoDateTime.Parse("2024-08-23T08:15:00Z")),
                        Data = new DotnetOpenEhr.Rm.DataStructures.ItemTree
                        {
                            Name = new DotnetOpenEhr.Rm.DataTypes.Text.DvText("tree"),
                            ArchetypeNodeId = "id5",
                            Items =
                            [
                                new DotnetOpenEhr.Rm.DataStructures.Element
                                {
                                    Name = new DotnetOpenEhr.Rm.DataTypes.Text.DvText("element"),
                                    ArchetypeNodeId = "id6",
                                    Value = new DotnetOpenEhr.Rm.DataTypes.Text.DvText("hello aot"),
                                },
                            ],
                        },
                    },
                ],
            },
        },
    ],
};

byte[] flatSchemaBytes = DotnetOpenEhr.Serialization.Json.Flat.OpenEhrFlatJson.Serialize(flatComp, flatTemplate);
System.Collections.Generic.IReadOnlyList<
    System.Collections.Generic.KeyValuePair<
        DotnetOpenEhr.Serialization.Json.Flat.FlatPath,
        System.Text.Json.JsonElement>> flatSchemaPairs =
    DotnetOpenEhr.Serialization.Json.Flat.FlatJsonReader.Read(flatSchemaBytes);
DotnetOpenEhr.Rm.Composition.Composition? flatSchemaRoundTripped =
    DotnetOpenEhr.Serialization.Json.Flat.OpenEhrFlatJson.ParseComposition(flatSchemaBytes, flatTemplate);
int flatSchemaContentCount = flatSchemaRoundTripped?.Content?.Count ?? 0;
System.Console.WriteLine($"flat schema-driven: keys={flatSchemaPairs.Count} content={flatSchemaContentCount}");

// Phase 9c: parse a one-line AQL query and evaluate it against an
// in-memory list of Compositions. Confirms the parser + tree-walking
// evaluator publish cleanly under PublishAot (no Expression.Compile,
// no reflection-based traversal).
System.Collections.Generic.List<DotnetOpenEhr.Rm.Composition.Composition> aqlSource =
[
    new DotnetOpenEhr.Rm.Composition.Composition
    {
        Name = new DotnetOpenEhr.Rm.DataTypes.Text.DvText("Alpha"),
        ArchetypeNodeId = "openEHR-EHR-COMPOSITION.encounter.v1",
        Uid = new DotnetOpenEhr.Rm.Support.HierObjectId { Value = "aql-1" },
        Language = new DotnetOpenEhr.Rm.DataTypes.Text.CodePhrase
        {
            TerminologyId = new DotnetOpenEhr.Rm.Support.TerminologyId { Value = "ISO_639-1" },
            CodeString = "en",
        },
        Territory = new DotnetOpenEhr.Rm.DataTypes.Text.CodePhrase
        {
            TerminologyId = new DotnetOpenEhr.Rm.Support.TerminologyId { Value = "ISO_3166-1" },
            CodeString = "GB",
        },
        Category = new DotnetOpenEhr.Rm.DataTypes.Text.DvCodedText
        {
            Value = "event",
            DefiningCode = new DotnetOpenEhr.Rm.DataTypes.Text.CodePhrase
            {
                TerminologyId = new DotnetOpenEhr.Rm.Support.TerminologyId { Value = "openehr" },
                CodeString = "433",
            },
        },
        Composer = new DotnetOpenEhr.Rm.Common.PartyIdentified { Name = "AOT" },
    },
    new DotnetOpenEhr.Rm.Composition.Composition
    {
        Name = new DotnetOpenEhr.Rm.DataTypes.Text.DvText("Bravo"),
        ArchetypeNodeId = "openEHR-EHR-COMPOSITION.encounter.v1",
        Uid = new DotnetOpenEhr.Rm.Support.HierObjectId { Value = "aql-2" },
        Language = new DotnetOpenEhr.Rm.DataTypes.Text.CodePhrase
        {
            TerminologyId = new DotnetOpenEhr.Rm.Support.TerminologyId { Value = "ISO_639-1" },
            CodeString = "en",
        },
        Territory = new DotnetOpenEhr.Rm.DataTypes.Text.CodePhrase
        {
            TerminologyId = new DotnetOpenEhr.Rm.Support.TerminologyId { Value = "ISO_3166-1" },
            CodeString = "GB",
        },
        Category = new DotnetOpenEhr.Rm.DataTypes.Text.DvCodedText
        {
            Value = "event",
            DefiningCode = new DotnetOpenEhr.Rm.DataTypes.Text.CodePhrase
            {
                TerminologyId = new DotnetOpenEhr.Rm.Support.TerminologyId { Value = "openehr" },
                CodeString = "433",
            },
        },
        Composer = new DotnetOpenEhr.Rm.Common.PartyIdentified { Name = "AOT" },
    },
    new DotnetOpenEhr.Rm.Composition.Composition
    {
        Name = new DotnetOpenEhr.Rm.DataTypes.Text.DvText("Charlie"),
        ArchetypeNodeId = "openEHR-EHR-COMPOSITION.encounter.v1",
        Uid = new DotnetOpenEhr.Rm.Support.HierObjectId { Value = "aql-3" },
        Language = new DotnetOpenEhr.Rm.DataTypes.Text.CodePhrase
        {
            TerminologyId = new DotnetOpenEhr.Rm.Support.TerminologyId { Value = "ISO_639-1" },
            CodeString = "en",
        },
        Territory = new DotnetOpenEhr.Rm.DataTypes.Text.CodePhrase
        {
            TerminologyId = new DotnetOpenEhr.Rm.Support.TerminologyId { Value = "ISO_3166-1" },
            CodeString = "GB",
        },
        Category = new DotnetOpenEhr.Rm.DataTypes.Text.DvCodedText
        {
            Value = "event",
            DefiningCode = new DotnetOpenEhr.Rm.DataTypes.Text.CodePhrase
            {
                TerminologyId = new DotnetOpenEhr.Rm.Support.TerminologyId { Value = "openehr" },
                CodeString = "433",
            },
        },
        Composer = new DotnetOpenEhr.Rm.Common.PartyIdentified { Name = "AOT" },
    },
];
DotnetOpenEhr.Aql.Ast.AqlQuery aqlQuery =
    DotnetOpenEhr.Aql.AqlParser.Parse("SELECT c FROM EHR e CONTAINS COMPOSITION c");
DotnetOpenEhr.Aql.Evaluation.AqlEvaluator aqlEvaluator = new();
System.Collections.Generic.IReadOnlyList<object?[]> aqlRows = aqlEvaluator.Evaluate(aqlQuery, aqlSource);
System.Console.WriteLine($"aql: rows={aqlRows.Count}");

// Phase 10: coverage gate. Every shipping NuGet package the SDK
// publishes must be transitively loaded by the smoke run; otherwise
// PublishAot would silently skip its IL2*/IL3* analysis. We use
// AppDomain.GetAssemblies() rather than enumerating ProjectReference
// metadata because the former is AOT-safe (no reflection over loaded
// types, just assembly names) and naturally only reports what the
// preceding smoke blocks actually touched.
string[] shippingAssemblies =
[
    "DotnetOpenEhr.Foundation",
    "DotnetOpenEhr.Terminology",
    "DotnetOpenEhr.Bmm",
    "DotnetOpenEhr.Bmm.Rm",
    "DotnetOpenEhr.Odin",
    "DotnetOpenEhr.Rm",
    "DotnetOpenEhr.Serialization.Json",
    "DotnetOpenEhr.Serialization.Json.Flat",
    "DotnetOpenEhr.Archetypes",
    "DotnetOpenEhr.Templates.Abstractions",
    "DotnetOpenEhr.Templates",
    "DotnetOpenEhr.Aql",
];

System.Reflection.Assembly[] loaded = System.AppDomain.CurrentDomain.GetAssemblies();
System.Collections.Generic.HashSet<string> loadedNames = [];
foreach (System.Reflection.Assembly assembly in loaded)
{
    string? assemblyName = assembly.GetName().Name;
    if (!string.IsNullOrEmpty(assemblyName))
    {
        loadedNames.Add(assemblyName);
    }
}

System.Collections.Generic.List<string> missingAssemblies = [];
foreach (string expected in shippingAssemblies)
{
    if (!loadedNames.Contains(expected))
    {
        missingAssemblies.Add(expected);
    }
}

if (missingAssemblies.Count > 0)
{
    System.Console.Error.WriteLine(
        $"coverage gate FAILED: missing assemblies {string.Join(", ", missingAssemblies)}");
    return 2;
}

System.Console.WriteLine($"coverage: {shippingAssemblies.Length} shipping assemblies loaded");

System.Console.WriteLine("smoke ok");
return 0;


