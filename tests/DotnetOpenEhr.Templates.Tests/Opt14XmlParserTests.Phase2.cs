using Xunit;

namespace DotnetOpenEhr.Templates.Tests;

/// <summary>
/// Phase-2 coverage: <see cref="Opt14XmlParser"/> reads the OPT1.4
/// envelope (root element, language, description, uid, archetype id,
/// header metadata) on every embedded KDS fixture.
/// </summary>
public sealed partial class Opt14XmlParserTests
{
    // Per-fixture expectations: archetype HRID (= root <definition>/<archetype_id>)
    // and the OPT1.4 friendly <template_id>/<value>.
    public static readonly System.Collections.Generic.IEnumerable<object[]> FixtureIdentityRows =
    [
        ["KDS_Vitalstatus.opt", "openEHR-EHR-COMPOSITION.report.v1",          "report",          "KDS_Vitalstatus", "de"],
        ["KDS_Diagnose.opt",    "openEHR-EHR-COMPOSITION.report.v1",          "report",          "KDS_Diagnose",    "de"],
        ["KDS_Person.opt",      "openEHR-EHR-COMPOSITION.person.v0",          "person",          "KDS_Person",      "de"],
        ["Blood Pressure.opt",  "openEHR-EHR-COMPOSITION.blood_pressure.v0",  "blood_pressure",  "Blood Pressure",  "en"],
    ];

    [Theory]
    [MemberData(nameof(FixtureIdentityRows))]
    public void Fixture_TemplateId_matches_root_archetype_concept_id(
        string fixture,
        string expectedHrid,
        string expectedConceptId,
        string expectedFriendlyName,
        string expectedLang)
    {
        _ = expectedFriendlyName;
        _ = expectedLang;
        OperationalTemplate opt = Opt14XmlParser.Parse(ReadFixture(fixture));
        Assert.NotNull(opt.ArchetypeId);
        Assert.Equal(expectedHrid, opt.ArchetypeId.ToString());
        // ITemplateSchema.TemplateId is the HRID's ConceptId segment.
        // For OPT1.4 inputs this is the root archetype's concept id —
        // NOT the OPT1.4 friendly name. (Criterion (b).)
        Assert.Equal(expectedConceptId, opt.TemplateId);
    }

    [Theory]
    [MemberData(nameof(FixtureIdentityRows))]
    public void Fixture_HeaderMetadata_records_opt14_template_id(
        string fixture,
        string expectedHrid,
        string expectedConceptId,
        string expectedFriendlyName,
        string expectedLang)
    {
        _ = expectedHrid;
        _ = expectedConceptId;
        _ = expectedLang;
        OperationalTemplate opt = Opt14XmlParser.Parse(ReadFixture(fixture));
        Assert.True(opt.HeaderMetadata.TryGetValue("template_id", out string? friendly),
            $"HeaderMetadata must surface the OPT1.4 friendly <template_id>/<value> for {fixture}.");
        Assert.Equal(expectedFriendlyName, friendly);
    }

    [Theory]
    [MemberData(nameof(FixtureIdentityRows))]
    public void Fixture_OriginalLanguage_matches_source(
        string fixture,
        string expectedHrid,
        string expectedConceptId,
        string expectedFriendlyName,
        string expectedLang)
    {
        _ = expectedHrid;
        _ = expectedConceptId;
        _ = expectedFriendlyName;
        OperationalTemplate opt = Opt14XmlParser.Parse(ReadFixture(fixture));
        Assert.Equal(expectedLang, opt.OriginalLanguage);
    }

    [Fact]
    public void IsTemplate_is_true_for_every_fixture()
    {
        foreach (string name in s_fixtureNames)
        {
            OperationalTemplate opt = Opt14XmlParser.Parse(ReadFixture(name));
            Assert.True(opt.IsTemplate, $"{name}: every parsed OPT must report IsTemplate=true.");
        }
    }

    [Fact]
    public void KDS_Vitalstatus_uid_populated()
    {
        OperationalTemplate opt = Opt14XmlParser.Parse(ReadFixture("KDS_Vitalstatus.opt"));
        Assert.Equal("2aeb8bbd-8b54-4f68-9230-b07e788e4619", opt.Uid);
    }

    [Fact]
    public void KDS_Vitalstatus_description_lifecycle_state_populated()
    {
        OperationalTemplate opt = Opt14XmlParser.Parse(ReadFixture("KDS_Vitalstatus.opt"));
        Assert.Equal("Initial", opt.Description.LifecycleState);
        Assert.True(opt.Description.Details.ContainsKey("de"),
            "German description-details block should land in Description.Details['de'].");
        Assert.Equal("Zur Repräsentation des Vitalstatus eines Patienten.",
            opt.Description.Details["de"].Purpose);
    }

    [Fact]
    public void KDS_Vitalstatus_concept_recorded_on_header_metadata()
    {
        OperationalTemplate opt = Opt14XmlParser.Parse(ReadFixture("KDS_Vitalstatus.opt"));
        Assert.Equal("KDS_Vitalstatus", opt.HeaderMetadata["concept"]);
    }

    [Fact]
    public void Load_stream_overload_matches_Parse_string_overload()
    {
        OperationalTemplate viaParse = Opt14XmlParser.Parse(ReadFixture("KDS_Vitalstatus.opt"));
        using System.IO.Stream s = OpenFixture("KDS_Vitalstatus.opt");
        OperationalTemplate viaLoad = Opt14XmlParser.Load(s);
        Assert.Equal(viaParse.ArchetypeId!.ToString(), viaLoad.ArchetypeId!.ToString());
        Assert.Equal(viaParse.TemplateId, viaLoad.TemplateId);
        Assert.Equal(viaParse.OriginalLanguage, viaLoad.OriginalLanguage);
        Assert.Equal(viaParse.HeaderMetadata["template_id"], viaLoad.HeaderMetadata["template_id"]);
    }
}
