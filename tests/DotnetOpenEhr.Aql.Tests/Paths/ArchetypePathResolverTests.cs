using DotnetOpenEhr.Aql;
using DotnetOpenEhr.Aql.Tests.Evaluation;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using Xunit;

namespace DotnetOpenEhr.Aql.Tests.Paths;

/// <summary>
/// Phase-3 smoke pins for <see cref="ArchetypePathResolver"/>. The
/// full FR contract matrix is covered in Phase 4.
/// </summary>
public class ArchetypePathResolverTests
{
    private static Observation BloodPressure(double systolic = 120, double diastolic = 80)
        => CompositionBuilder.NewBloodPressure(
            "openEHR-EHR-OBSERVATION.blood_pressure.v2",
            systolic, "mm[Hg]",
            diastolic, "mm[Hg]");

    [Fact]
    public void Resolve_returns_scalar_for_simple_attribute_chain()
    {
        Observation bp = BloodPressure();
        object? value = ArchetypePathResolver.Resolve(bp, "/data/origin/value");
        Assert.NotNull(value);
    }

    [Fact]
    public void Resolve_returns_null_when_intermediate_step_is_null()
    {
        Observation bp = BloodPressure();
        object? value = ArchetypePathResolver.Resolve(bp, "/protocol/items");
        Assert.Null(value);
    }

    [Fact]
    public void Resolve_throws_on_multi_match()
    {
        Observation bp = BloodPressure();
        // /data/events/data/items has 2 Elements (Systolic + Diastolic).
        Assert.Throws<InvalidOperationException>(
            () => ArchetypePathResolver.Resolve(bp, "/data/events/data/items"));
    }

    [Fact]
    public void ResolveAll_yields_matches_in_document_order()
    {
        Observation bp = BloodPressure();
        List<object?> items =
        [
            .. ArchetypePathResolver.ResolveAll(bp, "/data/events/data/items/value/magnitude"),
        ];
        Assert.Equal(2, items.Count);
        Assert.Equal(120d, items[0]);
        Assert.Equal(80d, items[1]);
    }
}
