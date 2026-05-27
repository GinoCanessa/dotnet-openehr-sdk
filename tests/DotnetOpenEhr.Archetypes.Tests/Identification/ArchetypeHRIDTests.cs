using DotnetOpenEhr.Archetypes.Identification;
using Xunit;

namespace DotnetOpenEhr.Archetypes.Tests.Identification;

public class ArchetypeHRIDTests
{
    [Theory]
    [InlineData("openEHR-EHR-OBSERVATION.blood_pressure.v2")]
    [InlineData("openEHR-EHR-OBSERVATION.blood_pressure.v2.0.1")]
    [InlineData("openEHR-EHR-OBSERVATION.blood_pressure.v2.0.1-alpha.3")]
    [InlineData("openEHR-EHR-COMPOSITION.report.v1.0.0")]
    [InlineData("org.openehr/openEHR-EHR-OBSERVATION.blood_pressure.v2.0.1")]
    [InlineData("uk.nhs.ckm/openEHR-EHR-EVALUATION.problem_diagnosis.v1.0.0")]
    public void Parse_round_trips(string text)
    {
        ArchetypeHRID hrid = ArchetypeHRID.Parse(text);
        Assert.Equal(text, hrid.ToString());
    }

    [Fact]
    public void Parse_breaks_apart_the_qualified_rm_entity()
    {
        ArchetypeHRID hrid = ArchetypeHRID.Parse("openEHR-EHR-OBSERVATION.blood_pressure.v2.0.1");
        Assert.Null(hrid.Namespace);
        Assert.Equal("openEHR", hrid.QualifiedRmEntity.PublisherId);
        Assert.Equal("EHR", hrid.QualifiedRmEntity.Package);
        Assert.Equal("OBSERVATION", hrid.QualifiedRmEntity.ClassName);
        Assert.Equal("blood_pressure", hrid.ConceptId);
        Assert.Equal(2, hrid.VersionId.Major);
        Assert.Equal(0, hrid.VersionId.Minor);
        Assert.Equal(1, hrid.VersionId.Patch);
    }

    [Fact]
    public void Parse_captures_namespace_when_present()
    {
        ArchetypeHRID hrid = ArchetypeHRID.Parse("org.openehr/openEHR-EHR-OBSERVATION.blood_pressure.v2");
        Assert.Equal("org.openehr", hrid.Namespace);
    }

    [Theory]
    [InlineData("")]
    [InlineData("openEHR")]
    [InlineData("openEHR-EHR-OBSERVATION")]
    [InlineData("openEHR-EHR-OBSERVATION.blood_pressure")]
    [InlineData("openEHR-EHR-OBSERVATION.blood_pressure.v")]
    [InlineData("openEHR-EHR-OBSERVATION..v2")]
    [InlineData("/openEHR-EHR-OBSERVATION.blood_pressure.v2")]
    public void Parse_rejects_invalid(string text)
    {
        Assert.False(ArchetypeHRID.TryParse(text, out _));
        Assert.Throws<FormatException>(() => ArchetypeHRID.Parse(text));
    }

    [Fact]
    public void Equality_is_by_shape()
    {
        ArchetypeHRID a = ArchetypeHRID.Parse("openEHR-EHR-OBSERVATION.blood_pressure.v2.0.1");
        ArchetypeHRID b = ArchetypeHRID.Parse("openEHR-EHR-OBSERVATION.blood_pressure.v2.0.1");
        ArchetypeHRID c = ArchetypeHRID.Parse("openEHR-EHR-OBSERVATION.blood_pressure.v2.0.2");
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
    }
}
