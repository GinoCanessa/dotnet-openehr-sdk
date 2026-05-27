using System.IO;
using System.Linq;
using System.Reflection;
using DotnetOpenEhr.Archetypes.Adl2;
using DotnetOpenEhr.Archetypes.Aom2;
using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using DotnetOpenEhr.Archetypes.Aom2.Resource;
using DotnetOpenEhr.Archetypes.Aom2.Terminology;
using DotnetOpenEhr.Archetypes.Identification;
using Xunit;

namespace DotnetOpenEhr.Archetypes.Tests.Adl2;

/// <summary>
/// Round-trip and per-section coverage for <see cref="Adl2Writer"/>.
/// </summary>
public class Adl2WriterTests
{
    private const string MinimalHeader = "archetype (adl_version=2.0.6; rm_release=1.1.0)\n\topenEHR-EHR-OBSERVATION.minimal.v1.0.0\n";
    private const string MinimalLanguage = "\nlanguage\n\toriginal_language = <[ISO_639-1::en]>\n";
    private const string MinimalDescription = "\ndescription\n\tlifecycle_state = <\"unmanaged\">\n";
    private const string MinimalTerminology = "\nterminology\n\tterm_definitions = <[\"en\"] = <[\"id1\"] = <text = <\"x\"> description = <\"y\">>>>\n";

    private static string Wrap(string definition)
        => MinimalHeader + MinimalLanguage + MinimalDescription + definition + MinimalTerminology;

    private static string ReadFixture(string name)
    {
        Assembly asm = typeof(Adl2WriterTests).Assembly;
        string? resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith(name, System.StringComparison.Ordinal));
        Assert.NotNull(resourceName);
        using Stream stream = asm.GetManifestResourceStream(resourceName!)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    // ------------------------------------------------------------------
    // Fixture round-trips: re-parse + deep-equal
    // ------------------------------------------------------------------

    public static TheoryData<string> Fixtures =>
    [
        "openEHR-EHR-OBSERVATION.blood_pressure.v2.0.0.adls",
        "openEHR-EHR-OBSERVATION.body_weight.v1.0.0.adls",
        "openEHR-EHR-OBSERVATION.internal_value_set.v1.0.0.adls",
    ];

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixture_roundtrips_in_original_mode(string fixtureName)
    {
        string text = ReadFixture(fixtureName);
        Archetype original = Adl2Parser.Parse(text);
        string written = Adl2Writer.Write(original, Adl2WriteMode.Original);
        Archetype roundtripped = Adl2Parser.Parse(written);
        Assert.True(
            ArchetypeEquality.ArchetypeDeepEquals(original, roundtripped),
            $"Original-mode round-trip diverged for {fixtureName}.");
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixture_roundtrips_in_canonical_mode(string fixtureName)
    {
        string text = ReadFixture(fixtureName);
        Archetype original = Adl2Parser.Parse(text);
        string written = Adl2Writer.Write(original, Adl2WriteMode.Canonical);
        Archetype roundtripped = Adl2Parser.Parse(written);
        Assert.True(
            ArchetypeEquality.ArchetypeDeepEquals(original, roundtripped),
            $"Canonical-mode round-trip diverged for {fixtureName}.");
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Canonical_output_is_byte_identical_on_second_pass(string fixtureName)
    {
        string text = ReadFixture(fixtureName);
        Archetype parsed = Adl2Parser.Parse(text);
        string written1 = Adl2Writer.Write(parsed, Adl2WriteMode.Canonical);
        Archetype reparsed = Adl2Parser.Parse(written1);
        string written2 = Adl2Writer.Write(reparsed, Adl2WriteMode.Canonical);
        Assert.Equal(written1, written2);
    }

    // ------------------------------------------------------------------
    // Programmatic tree round-trip
    // ------------------------------------------------------------------

    [Fact]
    public void Programmatic_tree_canonical_roundtrips()
    {
        AuthoredArchetype a = new()
        {
            ArchetypeId = ArchetypeHRID.Parse("openEHR-EHR-OBSERVATION.scratch.v1.0.0"),
            OriginalLanguage = "en",
            HeaderMetadata = new Dictionary<string, string>
            {
                ["adl_version"] = "2.0.6",
                ["rm_release"] = "1.1.0",
            },
        };
        a.Description.LifecycleState = "unmanaged";
        a.Terminology.OriginalLanguage = "en";
        a.Terminology.TermDefinitions["en"] = new Dictionary<string, ArchetypeTerm>
        {
            ["id1"] = new ArchetypeTerm { Text = "root", Description = "root node" },
            ["id2"] = new ArchetypeTerm { Text = "data", Description = "data tree" },
        };

        CComplexObject root = new()
        {
            RmTypeName = "OBSERVATION",
            NodeId = "id1",
        };
        CSingleAttribute dataAttr = new() { RmAttributeName = "data" };
        CComplexObject dataTree = new()
        {
            RmTypeName = "ITEM_TREE",
            NodeId = "id2",
        };
        dataAttr.Children.Add(dataTree);
        root.Attributes.Add(dataAttr);
        a.Definition = root;

        string written = Adl2Writer.Write(a, Adl2WriteMode.Canonical);
        Archetype roundtripped = Adl2Parser.Parse(written);
        Assert.True(ArchetypeEquality.ArchetypeDeepEquals(a, roundtripped));
    }

    // ------------------------------------------------------------------
    // Per-section emission positives
    // ------------------------------------------------------------------

    [Fact]
    public void Output_begins_with_archetype_keyword_for_authored()
    {
        Archetype a = Adl2Parser.Parse(Wrap("\ndefinition\n\tOBSERVATION[id1] matches { }\n"));
        string written = Adl2Writer.Write(a, Adl2WriteMode.Canonical);
        Assert.StartsWith("archetype", written, System.StringComparison.Ordinal);
        Assert.EndsWith("\n", written, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Output_begins_with_template_keyword_for_template()
    {
        string src = "template (adl_version=2.0.6)\n\topenEHR-EHR-COMPOSITION.foo.v1.0.0\n"
            + MinimalLanguage + MinimalDescription
            + "\ndefinition\n\tCOMPOSITION[id1] matches { }\n"
            + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        string written = Adl2Writer.Write(a, Adl2WriteMode.Canonical);
        Assert.StartsWith("template", written, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Output_begins_with_template_overlay_keyword()
    {
        string src = "template_overlay (adl_version=2.0.6)\n\topenEHR-EHR-CLUSTER.bar.v1.0.0\n"
            + MinimalLanguage + MinimalDescription
            + "\ndefinition\n\tCLUSTER[id1] matches { }\n"
            + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        string written = Adl2Writer.Write(a, Adl2WriteMode.Canonical);
        Assert.StartsWith("template_overlay", written, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Output_begins_with_operational_template_keyword()
    {
        string src = "operational_template (adl_version=2.0.6)\n\topenEHR-EHR-COMPOSITION.opt.v1.0.0\n"
            + MinimalLanguage + MinimalDescription
            + "\ndefinition\n\tCOMPOSITION[id1] matches { }\n"
            + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        string written = Adl2Writer.Write(a, Adl2WriteMode.Canonical);
        Assert.StartsWith("operational_template", written, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Header_emits_differential_modifier_when_set()
    {
        string src = "archetype differential (adl_version=2.0.6)\n\topenEHR-EHR-OBSERVATION.diff.v1.0.0\n"
            + MinimalLanguage + MinimalDescription
            + "\ndefinition\n\tOBSERVATION[id1] matches { }\n"
            + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        string written = Adl2Writer.Write(a, Adl2WriteMode.Canonical);
        Assert.Contains("differential", written);
    }

    [Fact]
    public void Header_preserves_metadata_pairs_and_flag_keys()
    {
        string src = "archetype (adl_version=2.0.6; rm_release=1.1.0; generated)\n\topenEHR-EHR-OBSERVATION.flag.v1.0.0\n"
            + MinimalLanguage + MinimalDescription
            + "\ndefinition\n\tOBSERVATION[id1] matches { }\n"
            + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        string written = Adl2Writer.Write(a, Adl2WriteMode.Canonical);
        Assert.Contains("adl_version=2.0.6", written);
        Assert.Contains("rm_release=1.1.0", written);
        Assert.Contains("generated", written);
        Archetype reparsed = Adl2Parser.Parse(written);
        Assert.True(reparsed.HeaderMetadata.ContainsKey("generated"));
    }

    [Fact]
    public void Specialize_clause_round_trips()
    {
        string src = MinimalHeader
            + "specialize\n\topenEHR-EHR-OBSERVATION.parent.v1.0.0\n"
            + MinimalLanguage + MinimalDescription
            + "\ndefinition\n\tOBSERVATION[id1] matches {}\n"
            + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        string written = Adl2Writer.Write(a, Adl2WriteMode.Canonical);
        Assert.Contains("specialize", written);
        Archetype reparsed = Adl2Parser.Parse(written);
        Assert.NotNull(reparsed.ParentArchetypeId);
        Assert.Equal("parent", reparsed.ParentArchetypeId!.ConceptId);
    }

    [Fact]
    public void Language_block_round_trips_with_translations()
    {
        string src = MinimalHeader
            + "\nlanguage\n\toriginal_language = <[ISO_639-1::en]>\n"
            + "\ttranslations = <[\"de\"] = <language = <[ISO_639-1::de]> author = <[\"name\"] = <\"X Y\">>>>\n"
            + MinimalDescription
            + "\ndefinition\n\tOBSERVATION[id1] matches {}\n"
            + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        string written = Adl2Writer.Write(a, Adl2WriteMode.Canonical);
        Assert.Contains("language", written);
        Assert.Contains("original_language", written);
        Assert.Contains("translations", written);
        Archetype reparsed = Adl2Parser.Parse(written);
        Assert.Equal("en", reparsed.OriginalLanguage);
        Assert.NotNull(reparsed.Translations);
        Assert.True(reparsed.Translations!.ContainsKey("de"));
    }

    [Fact]
    public void Description_block_round_trips()
    {
        string src = MinimalHeader + MinimalLanguage
            + "\ndescription\n"
            + "\tlifecycle_state = <\"published\">\n"
            + "\toriginal_author = <[\"name\"] = <\"A B\"> [\"email\"] = <\"x@y.z\">>\n"
            + "\tdetails = <[\"en\"] = <language = <[ISO_639-1::en]> purpose = <\"For testing.\"> keywords = <\"t1\", \"t2\">>>\n"
            + "\ndefinition\n\tOBSERVATION[id1] matches {}\n"
            + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        string written = Adl2Writer.Write(a, Adl2WriteMode.Canonical);
        Assert.Contains("description", written);
        Assert.Contains("lifecycle_state", written);
        Archetype reparsed = Adl2Parser.Parse(written);
        Assert.Equal("published", reparsed.Description.LifecycleState);
        Assert.Equal("For testing.", reparsed.Description.Details["en"].Purpose);
    }

    [Fact]
    public void Definition_block_emits_complex_object_tree()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tOBSERVATION[id1] occurrences matches {0..1} matches {\n"
            + "\t\tdata matches { HISTORY[id2] matches {} }\n"
            + "\t}\n"));
        string written = Adl2Writer.Write(a, Adl2WriteMode.Canonical);
        Assert.Contains("definition", written);
        Assert.Contains("OBSERVATION[id1]", written);
        Assert.Contains("HISTORY[id2]", written);
        Assert.Contains("occurrences matches {0..1}", written);
        Archetype reparsed = Adl2Parser.Parse(written);
        Assert.Equal("OBSERVATION", reparsed.Definition.RmTypeName);
        Assert.Equal("id1", reparsed.Definition.NodeId);
    }

    [Fact]
    public void Definition_emits_cardinality_clause()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tOBSERVATION[id1] matches {\n"
            + "\t\tdata matches { HISTORY[id2] matches { events cardinality matches {1..*; unordered} matches { EVENT[id3] matches {} } } }\n"
            + "\t}\n"));
        string written = Adl2Writer.Write(a, Adl2WriteMode.Canonical);
        Assert.Contains("cardinality matches {1..*", written);
        Assert.Contains("unordered", written);
        Archetype reparsed = Adl2Parser.Parse(written);
        CAttribute data = Assert.Single(reparsed.Definition.Attributes);
        CComplexObject history = Assert.IsType<CComplexObject>(Assert.Single(data.Children));
        CMultipleAttribute events = Assert.IsType<CMultipleAttribute>(Assert.Single(history.Attributes));
        Assert.NotNull(events.Cardinality);
        Assert.True(events.Cardinality!.Interval.HasLower);
        Assert.False(events.Cardinality.Interval.HasUpper);
    }

    [Fact]
    public void Terminology_block_emits_term_definitions_value_sets_and_bindings()
    {
        string src = MinimalHeader + MinimalLanguage + MinimalDescription
            + "\ndefinition\n\tOBSERVATION[id1] matches {}\n"
            + "\nterminology\n"
            + "\tterm_definitions = <[\"en\"] = <[\"id1\"] = <text = <\"x\"> description = <\"y\">>>>\n"
            + "\tvalue_sets = <[\"ac1\"] = <id = <\"ac1\"> members = <\"at0001\", \"at0002\">>>\n"
            + "\tterm_bindings = <[\"SNOMED-CT\"] = <[\"id1\"] = <\"http://snomed.info/id/1\">>>\n";
        Archetype a = Adl2Parser.Parse(src);
        string written = Adl2Writer.Write(a, Adl2WriteMode.Canonical);
        Assert.Contains("terminology", written);
        Assert.Contains("term_definitions", written);
        Assert.Contains("value_sets", written);
        Assert.Contains("term_bindings", written);
        Archetype reparsed = Adl2Parser.Parse(written);
        Assert.True(reparsed.Terminology.ValueSets.ContainsKey("ac1"));
        Assert.Equal(2, reparsed.Terminology.ValueSets["ac1"].Members.Count);
        Assert.True(reparsed.Terminology.TermBindings.ContainsKey("SNOMED-CT"));
    }

    // ------------------------------------------------------------------
    // Mode switch parity
    // ------------------------------------------------------------------

    [Fact]
    public void Canonical_and_original_modes_produce_parseable_output_for_minimal()
    {
        Archetype a = Adl2Parser.Parse(Wrap("\ndefinition\n\tOBSERVATION[id1] matches {}\n"));
        string canonical = Adl2Writer.Write(a, Adl2WriteMode.Canonical);
        string original = Adl2Writer.Write(a, Adl2WriteMode.Original);
        // Both must re-parse.
        Adl2Parser.Parse(canonical);
        Adl2Parser.Parse(original);
    }
}
