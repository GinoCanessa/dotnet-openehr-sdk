using DotnetOpenEhr.Odin;
using DotnetOpenEhr.Odin.Values;
using Xunit;

namespace DotnetOpenEhr.Odin.Tests;

/// <summary>
/// Focused tests for the <c>*</c> unbounded sentinel in ODIN intervals
/// (spec 7.2). Covers the canonical BMM cardinality shapes
/// (<c>|0..*|</c>, <c>|&gt;=0..*|</c>, <c>|*..5|</c>, etc.) and the
/// rejection of the degenerate <c>|*..*|</c> form.
/// </summary>
public class IntervalTests
{
    private static OdinValue RoundTrip(string source)
    {
        OdinValue parsed = OdinParser.Parse(source);
        string rendered = OdinWriter.Write(parsed);
        OdinValue reparsed = OdinParser.Parse(rendered);
        Assert.True(
            OdinValue.StructurallyEqual(parsed, reparsed),
            $"Round-trip mismatch.\n--- original ---\n{source}\n--- rendered ---\n{rendered}");
        return parsed;
    }

    [Fact]
    public void Parses_zero_to_star_cardinality()
    {
        OdinValue v = OdinParser.Parse("<|0..*|>");
        OdinInterval iv = v.AsInterval();
        Assert.NotNull(iv.Lower);
        Assert.Equal(0L, iv.Lower!.AsInteger().Value);
        Assert.True(iv.LowerIncluded);
        Assert.Null(iv.Upper);
        RoundTrip("<|0..*|>");
    }

    [Fact]
    public void Parses_greater_equal_zero_to_star()
    {
        OdinValue v = OdinParser.Parse("<|>=0..*|>");
        OdinInterval iv = v.AsInterval();
        Assert.NotNull(iv.Lower);
        Assert.Equal(0L, iv.Lower!.AsInteger().Value);
        Assert.True(iv.LowerIncluded);
        Assert.Null(iv.Upper);
        RoundTrip("<|>=0..*|>");
    }

    [Fact]
    public void Parses_star_to_finite_upper()
    {
        OdinValue v = OdinParser.Parse("<|*..5|>");
        OdinInterval iv = v.AsInterval();
        Assert.Null(iv.Lower);
        Assert.NotNull(iv.Upper);
        Assert.Equal(5L, iv.Upper!.AsInteger().Value);
        Assert.True(iv.UpperIncluded);
        RoundTrip("<|*..5|>");
    }

    [Fact]
    public void Parses_star_to_excluded_upper()
    {
        OdinValue v = OdinParser.Parse("<|*..<5|>");
        OdinInterval iv = v.AsInterval();
        Assert.Null(iv.Lower);
        Assert.NotNull(iv.Upper);
        Assert.Equal(5L, iv.Upper!.AsInteger().Value);
        Assert.False(iv.UpperIncluded);
        RoundTrip("<|*..<5|>");
    }

    [Fact]
    public void Parses_real_greater_equal_to_star()
    {
        OdinValue v = OdinParser.Parse("<|>=18.5..*|>");
        OdinInterval iv = v.AsInterval();
        Assert.NotNull(iv.Lower);
        Assert.Equal(18.5, iv.Lower!.AsReal().Value, 10);
        Assert.True(iv.LowerIncluded);
        Assert.Null(iv.Upper);
        RoundTrip("<|>=18.5..*|>");
    }

    [Fact]
    public void Rejects_both_sides_star()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("<|*..*|>"));
        Assert.Contains("*..*", ex.Message);
    }

    [Fact]
    public void Writer_emits_canonical_star_form_for_unbounded_upper()
    {
        OdinInterval iv = new(
            lower: new OdinInteger(0),
            lowerIncluded: true,
            upper: null,
            upperIncluded: true);
        string rendered = OdinWriter.Write(iv);
        Assert.Equal("<|>=0..*|>", rendered);
    }

    [Fact]
    public void Writer_emits_canonical_star_form_for_unbounded_lower()
    {
        OdinInterval iv = new(
            lower: null,
            lowerIncluded: true,
            upper: new OdinInteger(5),
            upperIncluded: true);
        string rendered = OdinWriter.Write(iv);
        Assert.Equal("<|*..5|>", rendered);
    }

    [Fact]
    public void Star_outside_interval_is_rejected()
    {
        Assert.Throws<OdinParseException>(() => OdinParser.Parse("attr = <*>"));
    }
}
