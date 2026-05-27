using System.IO;
using System.Linq;
using System.Reflection;
using DotnetOpenEhr.Archetypes.Adl2;
using DotnetOpenEhr.Archetypes.Aom2;
using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using DotnetOpenEhr.Archetypes.Identification;
using DotnetOpenEhr.Foundation;
using Xunit;

namespace DotnetOpenEhr.Archetypes.Tests.Adl2;

/// <summary>
/// Coverage of the ADL2 recursive-descent parser <see cref="Adl2Parser"/>.
/// Each top-level production and cADL constraint variant gets a positive
/// + negative test, plus three round-trip tests over real-world archie
/// fixtures embedded as resources.
/// </summary>
public class Adl2ParserTests
{
    private const string MinimalHeader = "archetype (adl_version=2.0.6; rm_release=1.1.0)\n\topenEHR-EHR-OBSERVATION.minimal.v1.0.0\n";
    private const string MinimalLanguage = "\nlanguage\n\toriginal_language = <[ISO_639-1::en]>\n";
    private const string MinimalDescription = "\ndescription\n\tlifecycle_state = <\"unmanaged\">\n";
    private const string MinimalTerminology = "\nterminology\n\tterm_definitions = <[\"en\"] = <[\"id1\"] = <text = <\"x\"> description = <\"y\">>>>\n";

    private static string Wrap(string definition)
        => MinimalHeader + MinimalLanguage + MinimalDescription + definition + MinimalTerminology;

    // -- Header ---------------------------------------------------------

    [Fact]
    public void Parses_authored_archetype_header()
    {
        Archetype a = Adl2Parser.Parse(Wrap("\ndefinition\n\tOBSERVATION[id1] matches { }\n"));
        Assert.IsType<AuthoredArchetype>(a);
        Assert.Equal("openEHR-EHR-OBSERVATION", a.ArchetypeId.QualifiedRmEntity.ToString());
        Assert.Equal("minimal", a.ArchetypeId.ConceptId);
        Assert.Equal("1.0.0", a.ArchetypeId.VersionId.ToString());
    }

    [Fact]
    public void Parses_template_header()
    {
        string src = "template (adl_version=2.0.6)\n\topenEHR-EHR-COMPOSITION.foo.v1.0.0\n"
                     + MinimalLanguage + MinimalDescription
                     + "\ndefinition\n\tCOMPOSITION[id1] matches { }\n"
                     + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        Assert.IsType<Template>(a);
        Assert.True(a.IsTemplate);
    }

    [Fact]
    public void Parses_template_overlay_header()
    {
        string src = "template_overlay (adl_version=2.0.6)\n\topenEHR-EHR-CLUSTER.bar.v1.0.0\n"
                     + MinimalLanguage + MinimalDescription
                     + "\ndefinition\n\tCLUSTER[id1] matches { }\n"
                     + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        Assert.IsType<TemplateOverlay>(a);
    }

    [Fact]
    public void Parses_operational_template_header()
    {
        string src = "operational_template (adl_version=2.0.6)\n\topenEHR-EHR-COMPOSITION.opt.v1.0.0\n"
                     + MinimalLanguage + MinimalDescription
                     + "\ndefinition\n\tCOMPOSITION[id1] matches { }\n"
                     + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        Assert.IsType<OperationalTemplate>(a);
    }

    [Fact]
    public void Parses_differential_modifier()
    {
        string src = "archetype differential (adl_version=2.0.6)\n\topenEHR-EHR-OBSERVATION.diff.v1.0.0\n"
                     + MinimalLanguage + MinimalDescription
                     + "\ndefinition\n\tOBSERVATION[id1] matches { }\n"
                     + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        Assert.True(a.IsDifferential);
    }

    [Fact]
    public void Header_captures_metadata_pairs()
    {
        Archetype a = Adl2Parser.Parse(Wrap("\ndefinition\n\tOBSERVATION[id1] matches { }\n"));
        Assert.Equal("2.0.6", a.HeaderMetadata["adl_version"]);
        Assert.Equal("1.1.0", a.HeaderMetadata["rm_release"]);
    }

    [Fact]
    public void Header_captures_flag_keys_with_empty_value()
    {
        string src = "archetype (adl_version=2.0.6; rm_release=1.1.0; generated)\n\topenEHR-EHR-OBSERVATION.flag.v1.0.0\n"
                     + MinimalLanguage + MinimalDescription
                     + "\ndefinition\n\tOBSERVATION[id1] matches { }\n"
                     + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        Assert.True(a.HeaderMetadata.ContainsKey("generated"));
        Assert.Equal(string.Empty, a.HeaderMetadata["generated"]);
    }

    [Fact]
    public void Throws_on_missing_archetype_keyword()
    {
        Assert.Throws<Adl2ParseException>(() => Adl2Parser.Parse("not_a_keyword openEHR-EHR-OBSERVATION.x.v1.0.0\n"));
    }

    [Fact]
    public void Throws_on_missing_hrid()
    {
        Assert.Throws<Adl2ParseException>(() => Adl2Parser.Parse("archetype (adl_version=2.0.6)\n\n"));
    }

    [Fact]
    public void Throws_on_invalid_hrid()
    {
        Assert.Throws<Adl2ParseException>(() =>
            Adl2Parser.Parse("archetype (adl_version=2.0.6)\n\tnot.a.valid.hrid\n"
                + MinimalLanguage + MinimalDescription
                + "\ndefinition\n\tOBSERVATION[id1] matches {}\n"
                + MinimalTerminology));
    }

    // -- Specialize -----------------------------------------------------

    [Fact]
    public void Parses_specialize_clause()
    {
        string src = MinimalHeader
            + "specialize\n\topenEHR-EHR-OBSERVATION.parent.v1.0.0\n"
            + MinimalLanguage + MinimalDescription
            + "\ndefinition\n\tOBSERVATION[id1] matches {}\n"
            + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        Assert.NotNull(a.ParentArchetypeId);
        Assert.Equal("parent", a.ParentArchetypeId!.ConceptId);
    }

    [Fact]
    public void Throws_on_specialize_without_hrid()
    {
        string src = MinimalHeader
            + "specialize\nlanguage\n\toriginal_language = <[ISO_639-1::en]>\n"
            + MinimalDescription
            + "\ndefinition\n\tOBSERVATION[id1] matches {}\n"
            + MinimalTerminology;
        Assert.Throws<Adl2ParseException>(() => Adl2Parser.Parse(src));
    }

    // -- Language section -----------------------------------------------

    [Fact]
    public void Parses_language_with_original_language_and_translations()
    {
        string src = MinimalHeader
            + "\nlanguage\n\toriginal_language = <[ISO_639-1::en]>\n"
            + "\ttranslations = <[\"de\"] = <language = <[ISO_639-1::de]> author = <[\"name\"] = <\"X Y\">>>>\n"
            + MinimalDescription
            + "\ndefinition\n\tOBSERVATION[id1] matches {}\n"
            + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        Assert.Equal("en", a.OriginalLanguage);
        Assert.NotNull(a.Translations);
        Assert.True(a.Translations!.ContainsKey("de"));
        Assert.Equal("de", a.Translations["de"].Language);
        Assert.Equal("X Y", a.Translations["de"].Author["name"]);
    }

    [Fact]
    public void Throws_on_language_missing_odin_block()
    {
        string src = MinimalHeader
            + "\nlanguage\n\toriginal_language = en\n"
            + MinimalDescription
            + "\ndefinition\n\tOBSERVATION[id1] matches {}\n"
            + MinimalTerminology;
        Assert.Throws<Adl2ParseException>(() => Adl2Parser.Parse(src));
    }

    // -- Description section --------------------------------------------

    [Fact]
    public void Parses_description_section()
    {
        string src = MinimalHeader + MinimalLanguage
            + "\ndescription\n"
            + "\tlifecycle_state = <\"published\">\n"
            + "\toriginal_author = <[\"name\"] = <\"A B\"> [\"email\"] = <\"x@y.z\">>\n"
            + "\tdetails = <[\"en\"] = <language = <[ISO_639-1::en]> purpose = <\"For testing.\"> keywords = <\"t1\", \"t2\">>>\n"
            + "\ndefinition\n\tOBSERVATION[id1] matches {}\n"
            + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        Assert.Equal("published", a.Description.LifecycleState);
        Assert.Equal("A B", a.Description.OriginalAuthor["name"]);
        Assert.Equal("For testing.", a.Description.Details["en"].Purpose);
        Assert.Equal(2, a.Description.Details["en"].Keywords!.Count);
    }

    [Fact]
    public void Throws_on_description_with_bad_odin()
    {
        string src = MinimalHeader + MinimalLanguage
            + "\ndescription\n\tlifecycle_state = <not_a_valid_odin>\n"
            + "\ndefinition\n\tOBSERVATION[id1] matches {}\n"
            + MinimalTerminology;
        Assert.Throws<Adl2ParseException>(() => Adl2Parser.Parse(src));
    }

    // -- Definition / cADL ---------------------------------------------

    [Fact]
    public void Parses_complex_object_with_node_id_and_occurrences()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tOBSERVATION[id1] occurrences matches {0..1} matches {\n"
            + "\t\tdata matches { HISTORY[id2] matches {} }\n"
            + "\t}\n"));
        Assert.Equal("OBSERVATION", a.Definition.RmTypeName);
        Assert.Equal("id1", a.Definition.NodeId);
        Assert.NotNull(a.Definition.Occurrences);
        CAttribute attr = Assert.Single(a.Definition.Attributes);
        Assert.Equal("data", attr.RmAttributeName);
        Assert.IsType<CSingleAttribute>(attr);
    }

    [Fact]
    public void Throws_on_missing_definition_section()
    {
        string src = MinimalHeader + MinimalLanguage + MinimalDescription + MinimalTerminology;
        Assert.Throws<Adl2ParseException>(() => Adl2Parser.Parse(src));
    }

    [Fact]
    public void Throws_on_unmatched_open_brace_in_definition()
    {
        string src = MinimalHeader + MinimalLanguage + MinimalDescription
            + "\ndefinition\n\tOBSERVATION[id1] matches {\n"
            + MinimalTerminology;
        Assert.Throws<Adl2ParseException>(() => Adl2Parser.Parse(src));
    }

    // -- CSingleAttribute / CMultipleAttribute --------------------------

    [Fact]
    public void Multiple_attribute_parsed_with_cardinality_keyword()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tOBSERVATION[id1] matches {\n"
            + "\t\tdata matches { HISTORY[id2] matches { events cardinality matches {1..*; unordered} matches { EVENT[id3] matches {} } } }\n"
            + "\t}\n"));
        CAttribute data = Assert.Single(a.Definition.Attributes);
        CComplexObject history = Assert.IsType<CComplexObject>(Assert.Single(data.Children));
        CAttribute events = Assert.Single(history.Attributes);
        CMultipleAttribute multi = Assert.IsType<CMultipleAttribute>(events);
        Assert.NotNull(multi.Cardinality);
        Assert.True(multi.Cardinality!.Interval.HasLower);
        Assert.False(multi.Cardinality.Interval.HasUpper);
    }

    [Fact]
    public void Single_attribute_parsed_without_cardinality_keyword()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tOBSERVATION[id1] matches {\n"
            + "\t\tdata matches { HISTORY[id2] matches {} }\n"
            + "\t}\n"));
        CAttribute data = Assert.Single(a.Definition.Attributes);
        Assert.IsType<CSingleAttribute>(data);
    }

    [Fact]
    public void Attribute_existence_parsed_as_interval()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tOBSERVATION[id1] matches {\n"
            + "\t\tdata existence matches {1..1} matches { HISTORY[id2] matches {} }\n"
            + "\t}\n"));
        CAttribute data = Assert.Single(a.Definition.Attributes);
        Assert.NotNull(data.Existence);
        Assert.True(data.Existence!.Contains(1));
    }

    // -- c_primitive_object variants ------------------------------------

    private static CObject FirstLeaf(Archetype a)
    {
        CComplexObject root = a.Definition;
        CObject cur = root;
        while (true)
        {
            if (cur is CComplexObject ccx && ccx.Attributes.Count > 0
                && ccx.Attributes[0].Children.Count > 0)
            {
                cur = ccx.Attributes[0].Children[0];
                continue;
            }
            break;
        }
        return cur;
    }

    [Fact]
    public void Parses_cstring_enumerated_values()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tELEMENT[id1] matches {\n"
            + "\t\tvalue matches { DV_TEXT[id2] matches { value matches { \"a\", \"b\", \"c\" } } }\n"
            + "\t}\n"));
        CObject leaf = FirstLeaf(a);
        CString cs = Assert.IsType<CString>(leaf);
        Assert.NotNull(cs.EnumeratedValues);
        Assert.Equal(3, cs.EnumeratedValues!.Count);
    }

    [Fact]
    public void Parses_cstring_regex_pattern()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tELEMENT[id1] matches {\n"
            + "\t\tvalue matches { DV_TEXT[id2] matches { value matches { /[a-z]+/ } } }\n"
            + "\t}\n"));
        CObject leaf = FirstLeaf(a);
        CString cs = Assert.IsType<CString>(leaf);
        Assert.Equal("[a-z]+", cs.Pattern);
    }

    [Fact]
    public void Parses_cinteger_interval()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tELEMENT[id1] matches {\n"
            + "\t\tvalue matches { DV_COUNT[id2] matches { magnitude matches { |0..100| } } }\n"
            + "\t}\n"));
        CObject leaf = FirstLeaf(a);
        CInteger ci = Assert.IsType<CInteger>(leaf);
        Assert.NotNull(ci.Range);
        Assert.True(ci.Range!.Contains(50));
    }

    [Fact]
    public void Parses_creal_interval_with_upper_open()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tELEMENT[id1] matches {\n"
            + "\t\tvalue matches { DV_QUANTITY[id2] matches { magnitude matches { |0.0..<1000.0| } } }\n"
            + "\t}\n"));
        CObject leaf = FirstLeaf(a);
        CReal cr = Assert.IsType<CReal>(leaf);
        Assert.NotNull(cr.Range);
        Assert.True(cr.Range!.HasLower);
        Assert.True(cr.Range.HasUpper);
        Assert.False(cr.Range.UpperIncluded);
    }

    [Fact]
    public void Parses_cboolean_true_only()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tELEMENT[id1] matches {\n"
            + "\t\tvalue matches { DV_BOOLEAN[id2] matches { value matches { true } } }\n"
            + "\t}\n"));
        CObject leaf = FirstLeaf(a);
        CBoolean cb = Assert.IsType<CBoolean>(leaf);
        Assert.True(cb.TrueValid);
        Assert.False(cb.FalseValid);
    }

    [Fact]
    public void Parses_terminology_code_valuesetref()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tELEMENT[id1] matches {\n"
            + "\t\tvalue matches { DV_CODED_TEXT[id2] matches { defining_code matches {[ac1]} } }\n"
            + "\t}\n"));
        CObject leaf = FirstLeaf(a);
        CTerminologyCode tc = Assert.IsType<CTerminologyCode>(leaf);
        Assert.Equal("ac1", tc.ValueSetRef);
    }

    [Fact]
    public void Parses_terminology_code_with_assumed_value()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tELEMENT[id1] matches {\n"
            + "\t\tvalue matches { DV_CODED_TEXT[id2] matches { defining_code matches {[ac1; at0002]} } }\n"
            + "\t}\n"));
        CObject leaf = FirstLeaf(a);
        CTerminologyCode tc = Assert.IsType<CTerminologyCode>(leaf);
        Assert.Equal("ac1", tc.ValueSetRef);
        Assert.Equal("at0002", ((CPrimitiveObject<string>)tc).DefaultValue);
    }

    [Fact]
    public void Parses_terminology_code_enumerated_values()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tELEMENT[id1] matches {\n"
            + "\t\tvalue matches { DV_CODED_TEXT[id2] matches { defining_code matches {[local::at0001, at0002, at0003]} } }\n"
            + "\t}\n"));
        CObject leaf = FirstLeaf(a);
        CTerminologyCode tc = Assert.IsType<CTerminologyCode>(leaf);
        Assert.NotNull(tc.EnumeratedValues);
        Assert.Equal(3, tc.EnumeratedValues!.Count);
        Assert.Equal("local", tc.TerminologyId);
    }

    [Fact]
    public void Throws_on_unterminated_terminology_code_constraint()
    {
        string body = "\ndefinition\n\tELEMENT[id1] matches {\n"
            + "\t\tvalue matches { DV_CODED_TEXT[id2] matches { defining_code matches {[ac1; at0002 } } }\n"
            + "\t}\n";
        Assert.Throws<Adl2ParseException>(() => Adl2Parser.Parse(Wrap(body)));
    }

    [Fact]
    public void Throws_on_invalid_interval_value()
    {
        string body = "\ndefinition\n\tELEMENT[id1] matches {\n"
            + "\t\tvalue matches { DV_COUNT[id2] matches { magnitude matches { |0..abc| } } }\n"
            + "\t}\n";
        Assert.Throws<Adl2ParseException>(() => Adl2Parser.Parse(Wrap(body)));
    }

    // -- Archetype slot --------------------------------------------------

    [Fact]
    public void Parses_archetype_slot_with_include()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tOBSERVATION[id1] matches {\n"
            + "\t\tdata matches { allow_archetype CLUSTER[id2] occurrences matches {0..1} matches {\n"
            + "\t\t\tinclude\n"
            + "\t\t\t\tarchetype_id/value matches {/openEHR-EHR-CLUSTER\\.device\\.v1/}\n"
            + "\t\t} }\n"
            + "\t}\n"));
        CAttribute data = Assert.Single(a.Definition.Attributes);
        ArchetypeSlot slot = Assert.IsType<ArchetypeSlot>(Assert.Single(data.Children));
        Assert.Equal("CLUSTER", slot.RmTypeName);
        Assert.Equal("id2", slot.NodeId);
        Assert.Single(slot.Includes);
    }

    [Fact]
    public void Throws_on_slot_missing_rm_type()
    {
        string body = "\ndefinition\n\tOBSERVATION[id1] matches {\n"
            + "\t\tdata matches { allow_archetype matches {} }\n"
            + "\t}\n";
        Assert.Throws<Adl2ParseException>(() => Adl2Parser.Parse(Wrap(body)));
    }

    // -- Archetype internal ref -----------------------------------------

    [Fact]
    public void Parses_use_node()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tOBSERVATION[id1] matches {\n"
            + "\t\tdata matches { HISTORY[id2] matches { events matches { use_node EVENT[id3] /data/items[id4] } } }\n"
            + "\t}\n"));
        CObject leaf = FirstLeaf(a);
        ArchetypeInternalRef r = Assert.IsType<ArchetypeInternalRef>(leaf);
        Assert.Equal("EVENT", r.RmTypeName);
        Assert.Equal("id3", r.NodeId);
        Assert.Contains("data", r.TargetPath);
    }

    // -- c_archetype_root -----------------------------------------------

    [Fact]
    public void Parses_archetype_root_via_ac_code()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tCOMPOSITION[id1] matches {\n"
            + "\t\tcontent matches { OBSERVATION[ac1] occurrences matches {0..*} matches {} }\n"
            + "\t}\n"));
        CAttribute content = Assert.Single(a.Definition.Attributes);
        CArchetypeRoot root = Assert.IsType<CArchetypeRoot>(Assert.Single(content.Children));
        Assert.Equal("OBSERVATION", root.RmTypeName);
        Assert.Equal("ac1", root.ArchetypeRef);
    }

    // -- Rules section --------------------------------------------------

    [Fact]
    public void Parses_rules_section_as_raw_text()
    {
        string src = MinimalHeader + MinimalLanguage + MinimalDescription
            + "\ndefinition\n\tOBSERVATION[id1] matches {}\n"
            + "\nrules\n\t/data/items[id5]/value/magnitude > 0\n"
            + MinimalTerminology;
        Archetype a = Adl2Parser.Parse(src);
        Assert.NotNull(a.Rules);
        Assert.Contains("magnitude > 0", a.Rules!.RawText);
    }

    [Fact]
    public void Rules_section_optional_when_absent()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tOBSERVATION[id1] matches {}\n"));
        Assert.Null(a.Rules);
    }

    // -- Terminology section --------------------------------------------

    [Fact]
    public void Parses_terminology_term_definitions()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tOBSERVATION[id1] matches {}\n"));
        Assert.True(a.Terminology.TermDefinitions.ContainsKey("en"));
        Assert.True(a.Terminology.TermDefinitions["en"].ContainsKey("id1"));
        Assert.Equal("x", a.Terminology.TermDefinitions["en"]["id1"].Text);
    }

    [Fact]
    public void Parses_terminology_value_sets()
    {
        string src = MinimalHeader + MinimalLanguage + MinimalDescription
            + "\ndefinition\n\tOBSERVATION[id1] matches {}\n"
            + "\nterminology\n"
            + "\tterm_definitions = <[\"en\"] = <[\"id1\"] = <text = <\"x\"> description = <\"y\">>>>\n"
            + "\tvalue_sets = <[\"ac1\"] = <id = <\"ac1\"> members = <\"at0001\", \"at0002\">>>\n";
        Archetype a = Adl2Parser.Parse(src);
        Assert.True(a.Terminology.ValueSets.ContainsKey("ac1"));
        Assert.Equal(2, a.Terminology.ValueSets["ac1"].Members.Count);
    }

    [Fact]
    public void Throws_on_terminology_without_odin_block()
    {
        string src = MinimalHeader + MinimalLanguage + MinimalDescription
            + "\ndefinition\n\tOBSERVATION[id1] matches {}\n"
            + "\nterminology\n\tterm_definitions = bare_identifier\n";
        Assert.Throws<Adl2ParseException>(() => Adl2Parser.Parse(src));
    }

    // -- Annotations section --------------------------------------------

    [Fact]
    public void Parses_annotations_section_when_present()
    {
        string src = MinimalHeader + MinimalLanguage + MinimalDescription
            + "\ndefinition\n\tOBSERVATION[id1] matches {}\n"
            + MinimalTerminology
            + "\nannotations\n\tdocumentation = <[\"en\"] = <[\"/data\"] = <\"Note.\">>>\n";
        Archetype a = Adl2Parser.Parse(src);
        Assert.NotNull(a.Annotations);
    }

    [Fact]
    public void Annotations_section_optional()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tOBSERVATION[id1] matches {}\n"));
        Assert.Null(a.Annotations);
    }

    // -- Source position tracking ---------------------------------------

    [Fact]
    public void Source_position_populated_on_definition_root()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tOBSERVATION[id1] matches {\n\t\tdata matches { HISTORY[id2] matches {} }\n\t}\n"));
        Assert.True(a.Definition.SourceLine > 0);
        Assert.True(a.Definition.SourceColumn > 0);
    }

    [Fact]
    public void Source_position_on_attribute_matches_token()
    {
        // 'data' identifier sits at line 12, column 3 (after \t\t) within Wrap().
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tOBSERVATION[id1] matches {\n\t\tdata matches { HISTORY[id2] matches {} }\n\t}\n"));
        CAttribute data = Assert.Single(a.Definition.Attributes);
        Assert.True(data.SourceLine > 0);
        Assert.True(data.SourceColumn > 0);
    }

    [Fact]
    public void Parse_error_reports_line_and_column()
    {
        // Inject an illegal token on a known line in the body.
        string src = MinimalHeader + MinimalLanguage + MinimalDescription
            + "\ndefinition\n\tOBSERVATION[id1] matches {\n"
            + "\t\t!!! }\n"
            + MinimalTerminology;
        Adl2ParseException ex = Assert.Throws<Adl2ParseException>(() => Adl2Parser.Parse(src));
        Assert.True(ex.Line > 0);
        Assert.True(ex.Column > 0);
    }

    // -- Fixture round-trips --------------------------------------------

    private static string ReadFixture(string name)
    {
        Assembly asm = typeof(Adl2ParserTests).Assembly;
        string? resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith(name, System.StringComparison.Ordinal));
        Assert.NotNull(resourceName);
        using Stream stream = asm.GetManifestResourceStream(resourceName!)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Fixture_blood_pressure_parses()
    {
        string text = ReadFixture("openEHR-EHR-OBSERVATION.blood_pressure.v2.0.0.adls");
        Archetype a = Adl2Parser.Parse(text);
        Assert.IsType<AuthoredArchetype>(a);
        Assert.Equal("blood_pressure", a.ArchetypeId.ConceptId);
        Assert.Equal("OBSERVATION", a.Definition.RmTypeName);
        Assert.NotEmpty(a.Definition.Attributes);
        Assert.True(a.Terminology.TermDefinitions.ContainsKey("en"));
    }

    [Fact]
    public void Fixture_body_weight_parses()
    {
        string text = ReadFixture("openEHR-EHR-OBSERVATION.body_weight.v1.0.0.adls");
        Archetype a = Adl2Parser.Parse(text);
        Assert.IsType<AuthoredArchetype>(a);
        Assert.Equal("body_weight", a.ArchetypeId.ConceptId);
        Assert.Equal("OBSERVATION", a.Definition.RmTypeName);
        Assert.NotEmpty(a.Definition.Attributes);
        Assert.NotNull(a.Translations);
        Assert.True(a.Translations!.Count > 1);
    }

    [Fact]
    public void Fixture_internal_value_set_parses()
    {
        string text = ReadFixture("openEHR-EHR-OBSERVATION.internal_value_set.v1.0.0.adls");
        Archetype a = Adl2Parser.Parse(text);
        Assert.IsType<AuthoredArchetype>(a);
        Assert.Equal("internal_value_set", a.ArchetypeId.ConceptId);
        Assert.Equal("OBSERVATION", a.Definition.RmTypeName);
        Assert.NotEmpty(a.Terminology.ValueSets);
        Assert.True(a.Terminology.ValueSets.ContainsKey("ac1"));
    }
}
