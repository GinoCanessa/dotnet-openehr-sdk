using System.IO;
using System.Linq;
using System.Reflection;
using DotnetOpenEhr.Archetypes.Aom2.Terminology;
using DotnetOpenEhr.Archetypes.Identification;
using DotnetOpenEhr.Bmm;
using DotnetOpenEhr.Bmm.Rm;
using DotnetOpenEhr.Templates.Abstractions;
using Xunit;

namespace DotnetOpenEhr.Templates.Tests;

/// <summary>
/// Coverage for <see cref="Opt2Parser"/> and the concrete
/// <see cref="OperationalTemplate"/>: fixture-driven happy paths plus
/// <see cref="ITemplateSchema"/> behaviour.
/// </summary>
public sealed class Opt2ParserTests
{
    private static readonly BmmModel s_rmBmm = OpenEhrRmBmm.LoadDefault();

    private static string LoadFixture(string name)
    {
        Assembly asm = typeof(Opt2ParserTests).Assembly;
        string? match = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(name, System.StringComparison.Ordinal));
        if (match is null)
        {
            throw new FileNotFoundException($"Embedded fixture '{name}' not found.");
        }
        using Stream s = asm.GetManifestResourceStream(match)!;
        using StreamReader r = new(s);
        return r.ReadToEnd();
    }

    // ---- minimal_vitals.opt2 ----------------------------------------

    [Fact]
    public void MinimalVitals_parses_to_OperationalTemplate_with_expected_hrid()
    {
        OperationalTemplate opt = Opt2Parser.Parse(LoadFixture("minimal_vitals.opt2"));
        Assert.NotNull(opt);
        Assert.NotNull(opt.ArchetypeId);
        Assert.Equal("openEHR-EHR-OBSERVATION.minimal_vitals.v1.0.0", opt.ArchetypeId.ToString());
        Assert.Equal("OBSERVATION", opt.Definition.RmTypeName);
    }

    [Fact]
    public void MinimalVitals_populates_component_terminologies()
    {
        OperationalTemplate opt = Opt2Parser.Parse(LoadFixture("minimal_vitals.opt2"));
        Assert.Single(opt.ComponentTerminologies);
        ArchetypeHRID key = opt.ComponentTerminologies.Keys.First();
        Assert.Equal("openEHR-EHR-OBSERVATION.minimal_vitals.v1.0.0", key.ToString());
        ArchetypeTerminology term = opt.ComponentTerminologies[key];
        Assert.True(term.TermDefinitions.TryGetValue("en", out Dictionary<string, ArchetypeTerm>? en));
        Assert.NotNull(en);
        Assert.Contains("id1", en!.Keys);
    }

    [Fact]
    public void MinimalVitals_nodes_collection_is_populated()
    {
        OperationalTemplate opt = Opt2Parser.Parse(LoadFixture("minimal_vitals.opt2"));
        Assert.True(opt.Nodes.Count >= 7, $"Expected ≥7 nodes, got {opt.Nodes.Count}.");
        Assert.Contains(opt.Nodes, n => n.RmTypeName == "OBSERVATION" && n.AqlPath == "/");
        Assert.Contains(opt.Nodes, n => n.RmTypeName == "DV_QUANTITY");
    }

    [Fact]
    public void MinimalVitals_TryResolveType_finds_leaf_value()
    {
        OperationalTemplate opt = Opt2Parser.Parse(LoadFixture("minimal_vitals.opt2"));
        string flat = $"{opt.TemplateId}/data/events/data/items/value";
        bool ok = opt.TryResolveType(flat.AsSpan(), out TemplateRmTypeResolution res);
        Assert.True(ok, $"Resolver should find '{flat}'.");
        Assert.Equal("DV_QUANTITY", res.RmTypeName);
    }

    [Fact]
    public void MinimalVitals_TryResolveType_returns_false_for_unknown_path()
    {
        OperationalTemplate opt = Opt2Parser.Parse(LoadFixture("minimal_vitals.opt2"));
        bool ok = opt.TryResolveType("totally/unknown/path".AsSpan(), out TemplateRmTypeResolution res);
        Assert.False(ok);
        Assert.Equal(default, res);
    }

    [Fact]
    public void MinimalVitals_marks_polymorphic_value_attribute()
    {
        OperationalTemplate opt = Opt2Parser.Parse(LoadFixture("minimal_vitals.opt2"));
        string flat = $"{opt.TemplateId}/data/events/data/items/value";
        Assert.True(opt.TryResolveType(flat.AsSpan(), out TemplateRmTypeResolution res));
        // ELEMENT.value is declared as DATA_VALUE in the RM — many subtypes
        // (DV_QUANTITY, DV_TEXT, ...) exist, so the resolution must be
        // polymorphic.
        Assert.True(res.IsPolymorphic, "ELEMENT.value should be flagged polymorphic.");
    }

    // ---- report_composition.opt2 ------------------------------------

    [Fact]
    public void ReportComposition_parses_to_OperationalTemplate_with_expected_hrid()
    {
        OperationalTemplate opt = Opt2Parser.Parse(LoadFixture("report_composition.opt2"));
        Assert.NotNull(opt);
        Assert.Equal("openEHR-EHR-COMPOSITION.report.v1.0.0", opt.ArchetypeId.ToString());
        Assert.Equal("COMPOSITION", opt.Definition.RmTypeName);
    }

    [Fact]
    public void ReportComposition_has_two_component_terminologies()
    {
        OperationalTemplate opt = Opt2Parser.Parse(LoadFixture("report_composition.opt2"));
        Assert.Equal(2, opt.ComponentTerminologies.Count);
        Assert.Contains(opt.ComponentTerminologies.Keys,
            k => k.ToString() == "openEHR-EHR-OBSERVATION.notes.v1.0.0");
        Assert.Contains(opt.ComponentTerminologies.Keys,
            k => k.ToString() == "openEHR-EHR-SECTION.summary.v1.0.0");
    }

    [Fact]
    public void ReportComposition_nodes_count_exceeds_threshold()
    {
        OperationalTemplate opt = Opt2Parser.Parse(LoadFixture("report_composition.opt2"));
        Assert.True(opt.Nodes.Count >= 8,
            $"Expected ≥8 nodes for the report composition, got {opt.Nodes.Count}.");
    }

    [Fact]
    public void ReportComposition_TryResolveType_finds_notes_value()
    {
        OperationalTemplate opt = Opt2Parser.Parse(LoadFixture("report_composition.opt2"));
        string flat = $"{opt.TemplateId}/content/data/events/data/items/value";
        Assert.True(opt.TryResolveType(flat.AsSpan(), out TemplateRmTypeResolution res));
        Assert.Equal("DV_TEXT", res.RmTypeName);
    }

    [Fact]
    public void ReportComposition_records_occurrences_for_multiplicity()
    {
        OperationalTemplate opt = Opt2Parser.Parse(LoadFixture("report_composition.opt2"));
        TemplateNode? section = opt.Nodes.FirstOrDefault(n => n.RmTypeName == "SECTION");
        Assert.NotNull(section);
        // SECTION[id8] declared `occurrences matches {0..1}` in the fixture.
        Assert.Equal(0, section!.Occurrence.Min);
        Assert.Equal(1, section.Occurrence.Max);
    }

    // ---- Cross-cutting ----------------------------------------------

    [Fact]
    public void Parse_via_span_overload_matches_string_overload()
    {
        string src = LoadFixture("minimal_vitals.opt2");
        OperationalTemplate a = Opt2Parser.Parse(src);
        OperationalTemplate b = Opt2Parser.Parse(src.AsSpan());
        Assert.Equal(a.ArchetypeId.ToString(), b.ArchetypeId.ToString());
        Assert.Equal(a.Nodes.Count, b.Nodes.Count);
        Assert.Equal(a.ComponentTerminologies.Count, b.ComponentTerminologies.Count);
    }

    [Fact]
    public void Parse_rejects_null_source()
    {
        Assert.Throws<System.ArgumentNullException>(() => Opt2Parser.Parse((string)null!));
    }

    [Fact]
    public void Initialize_can_be_called_explicitly_with_custom_bmm()
    {
        OperationalTemplate opt = Opt2Parser.Parse(LoadFixture("minimal_vitals.opt2"));
        int original = opt.Nodes.Count;
        opt.Initialize(s_rmBmm);
        Assert.Equal(original, opt.Nodes.Count);
    }
}
