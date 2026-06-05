using DotnetOpenEhr.Aql;
using DotnetOpenEhr.Aql.Tests.Evaluation;
using DotnetOpenEhr.Rm.Composition;
using Xunit;

namespace DotnetOpenEhr.Aql.Tests.Paths;

/// <summary>
/// Behavioral pins for the pre-compiled <see cref="ArchetypePath"/>:
/// <c>Parse</c> / <c>TryParse</c> semantics, segment caching across
/// repeated resolutions, and <c>ToString</c> canonicalization.
/// </summary>
public class ArchetypePathTests
{
    [Fact]
    public void Parse_caches_segments_across_resolve_calls()
    {
        Observation bpA = CompositionBuilder.NewBloodPressure(
            "openEHR-EHR-OBSERVATION.blood_pressure.v2",
            120, "mm[Hg]", 80, "mm[Hg]");
        Observation bpB = CompositionBuilder.NewBloodPressure(
            "openEHR-EHR-OBSERVATION.blood_pressure.v2",
            130, "mm[Hg]", 85, "mm[Hg]");

        ArchetypePath path = ArchetypePath.Parse(
            "/data/events/data/items[at0004]/value/magnitude");

        double magA = path.Resolve<double>(bpA);
        double magB = path.Resolve<double>(bpB);

        Assert.Equal(120d, magA);
        Assert.Equal(130d, magB);
    }

    [Fact]
    public void TryParse_returns_true_with_valid_path()
    {
        bool ok = ArchetypePath.TryParse("/data/origin", out ArchetypePath? result);
        Assert.True(ok);
        Assert.NotNull(result);
    }

    [Fact]
    public void TryParse_returns_false_and_null_with_invalid_path()
    {
        bool ok = ArchetypePath.TryParse("data//items", out ArchetypePath? result);
        Assert.False(ok);
        Assert.Null(result);
    }

    [Fact]
    public void TryParse_returns_false_with_null_string()
    {
        bool ok = ArchetypePath.TryParse((string?)null, out ArchetypePath? result);
        Assert.False(ok);
        Assert.Null(result);
    }

    [Fact]
    public void ToString_normalizes_to_leading_slash_form()
    {
        ArchetypePath path = ArchetypePath.Parse("data/items[at0001]");
        Assert.Equal("/data/items[at0001]", path.ToString());
    }

    [Fact]
    public void ToString_round_trips_combined_predicate()
    {
        ArchetypePath path = ArchetypePath.Parse("/items[at0004, 'Systolic']");
        Assert.Equal("/items[at0004, 'Systolic']", path.ToString());
    }

    [Fact]
    public void ToString_round_trips_name_only_predicate_with_escapes()
    {
        ArchetypePath path = ArchetypePath.Parse(@"/items['It\'s']");
        Assert.Equal(@"/items['It\'s']", path.ToString());
    }

    // ---- M19: typed Resolve<T> cast contract -------------------------

    [Fact]
    public void Compiled_path_ResolveT_with_wrong_type_throws_InvalidCastException()
    {
        Observation bp = CompositionBuilder.NewBloodPressure(
            "openEHR-EHR-OBSERVATION.blood_pressure.v2",
            120, "mm[Hg]", 80, "mm[Hg]");
        ArchetypePath path = ArchetypePath.Parse(
            "/data/events/data/items[at0004]/value/magnitude");

        // The magnitude is a double — asking for string throws.
        Assert.Throws<InvalidCastException>(() => path.Resolve<string>(bp));
    }

    [Fact]
    public void Resolver_ResolveT_with_wrong_type_throws_InvalidCastException()
    {
        Observation bp = CompositionBuilder.NewBloodPressure(
            "openEHR-EHR-OBSERVATION.blood_pressure.v2",
            120, "mm[Hg]", 80, "mm[Hg]");
        Assert.Throws<InvalidCastException>(
            () => ArchetypePathResolver.Resolve<string>(
                bp,
                "/data/events/data/items[at0004]/value/magnitude"));
    }
}
