using DotnetOpenEhr.Aql.Evaluation;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using Xunit;

namespace DotnetOpenEhr.Aql.Tests.Evaluation;

/// <summary>
/// Pins B1 — AqlEvaluator's DISTINCT row-key hash/equality contract.
/// Before B1's fix, RowKey.Equals routed through AreEqual (which
/// numerically unified int/long/double) while RowKey.GetHashCode
/// routed through Coerce only, so DISTINCT could silently emit
/// duplicate rows for `1` / `1L` / `1.0`.
/// </summary>
public sealed class AqlEvaluatorDistinctTests
{
    [Theory]
    [InlineData(1, 1L)]
    [InlineData(1, 1.0)]
    [InlineData(1L, 1.0)]
    [InlineData(1, (short)1)]
    [InlineData(1L, (byte)1)]
    public void CanonicalKey_collapsesEquivalentNumerics(object a, object b)
    {
        Assert.Equal(AqlEvaluator.CanonicalKey(a), AqlEvaluator.CanonicalKey(b));
        Assert.Equal(
            AqlEvaluator.CanonicalKey(a)?.GetHashCode(),
            AqlEvaluator.CanonicalKey(b)?.GetHashCode());
    }

    [Fact]
    public void CanonicalKey_collapsesDvCountMagnitudeToCanonicalNumeric()
    {
        DvCount c = new() { Magnitude = 1 };
        Assert.Equal(AqlEvaluator.CanonicalKey(c), AqlEvaluator.CanonicalKey(1));
        Assert.Equal(AqlEvaluator.CanonicalKey(c), AqlEvaluator.CanonicalKey(1L));
        Assert.Equal(AqlEvaluator.CanonicalKey(c), AqlEvaluator.CanonicalKey(1.0));
    }

    [Fact]
    public void CanonicalKey_distinguishesDvQuantityWithUnitsFromBareNumeric()
    {
        DvQuantity q = new(1, "mg");
        Assert.NotEqual(AqlEvaluator.CanonicalKey(q), AqlEvaluator.CanonicalKey(1));
        Assert.NotEqual(AqlEvaluator.CanonicalKey(q), AqlEvaluator.CanonicalKey(1L));
        // But two DvQuantities with the same (magnitude, units) collapse.
        DvQuantity q2 = new(1.0, "mg");
        Assert.Equal(AqlEvaluator.CanonicalKey(q), AqlEvaluator.CanonicalKey(q2));
        Assert.Equal(
            AqlEvaluator.CanonicalKey(q)?.GetHashCode(),
            AqlEvaluator.CanonicalKey(q2)?.GetHashCode());
    }

    [Fact]
    public void CanonicalKey_distinguishesDvQuantitiesWithDifferentUnits()
    {
        DvQuantity mg = new(1, "mg");
        DvQuantity g = new(1, "g");
        Assert.NotEqual(AqlEvaluator.CanonicalKey(mg), AqlEvaluator.CanonicalKey(g));
    }

    [Fact]
    public void CanonicalKey_preservesNonIntegerReals()
    {
        Assert.Equal(AqlEvaluator.CanonicalKey(1.5), AqlEvaluator.CanonicalKey(1.5));
        Assert.NotEqual(AqlEvaluator.CanonicalKey(1.5), AqlEvaluator.CanonicalKey(1));
        Assert.NotEqual(AqlEvaluator.CanonicalKey(1.5), AqlEvaluator.CanonicalKey(2));
    }

    [Fact]
    public void CanonicalKey_unwrapsDvTextToString()
    {
        DvText t = new("hello");
        Assert.Equal(AqlEvaluator.CanonicalKey(t), AqlEvaluator.CanonicalKey("hello"));
    }

    [Fact]
    public void CanonicalKey_nullStaysNull()
    {
        Assert.Null(AqlEvaluator.CanonicalKey(null));
    }
}
