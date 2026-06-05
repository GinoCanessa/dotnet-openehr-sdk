using DotnetOpenEhr.Foundation.Iso;
using Xunit;

namespace DotnetOpenEhr.Foundation.Tests;

/// <summary>
/// M6 — IsoTimeZone parser tightening, range checks, mode-aware
/// rounding/clamping, and canonical equality contract.
/// </summary>
public class IsoTimeZoneTests
{
    [Theory]
    [InlineData("Z")]
    [InlineData("+00:00")]
    [InlineData("-00:00")]
    [InlineData("+14:00")]
    [InlineData("-12:00")]
    [InlineData("+05:30")]
    public void Parse_acceptsBoundaryAndCommonOffsets(string text)
    {
        IsoTimeZone z = IsoTimeZone.Parse(text);
        Assert.NotNull(z);
    }

    [Fact]
    public void Parse_Strict_rejectsOutOfRangeHours_PlusFifteen()
    {
        Assert.False(IsoTimeZone.TryParse("+15:00", IsoParseMode.Strict, out _));
    }

    [Fact]
    public void Parse_FixAsPossible_clamps_PlusFifteenToPlusFourteen()
    {
        IsoTimeZone z = IsoTimeZone.Parse("+15:00", IsoParseMode.FixAsPossible);
        Assert.Equal(14, z.Hours);
        Assert.Equal(0, z.Minutes);
        Assert.False(z.IsNegative);
    }

    [Fact]
    public void Parse_FixAsPossible_clampsNegative_MinusThirteenToMinusTwelve()
    {
        IsoTimeZone z = IsoTimeZone.Parse("-13:00", IsoParseMode.FixAsPossible);
        Assert.Equal(12, z.Hours);
        Assert.Equal(0, z.Minutes);
        Assert.True(z.IsNegative);
    }

    [Fact]
    public void Parse_Strict_rejects_PlusFiveSeventeen()
    {
        Assert.False(IsoTimeZone.TryParse("+05:17", IsoParseMode.Strict, out _));
    }

    [Fact]
    public void Parse_FixAsPossible_rounds_PlusFiveSeventeenToPlusFifteen()
    {
        IsoTimeZone z = IsoTimeZone.Parse("+05:17", IsoParseMode.FixAsPossible);
        Assert.Equal(5, z.Hours);
        Assert.Equal(15, z.Minutes);
    }

    [Fact]
    public void Parse_acceptsBasicFormWithoutColon()
    {
        IsoTimeZone z = IsoTimeZone.Parse("+0530");
        Assert.Equal(5, z.Hours);
        Assert.Equal(30, z.Minutes);
    }

    [Fact]
    public void Parse_acceptsLowercaseZ_andCanonicalisesToZ()
    {
        IsoTimeZone z = IsoTimeZone.Parse("z");
        Assert.Equal("Z", z.OriginalLexicalForm);
        Assert.True(z.IsUtc);
    }

    [Theory]
    [InlineData("Z")]
    [InlineData("+00:00")]
    [InlineData("-00:00")]
    public void IsUtc_isTrueFor_Z_PlusZeroZero_andMinusZeroZero(string text)
    {
        IsoTimeZone z = IsoTimeZone.Parse(text);
        Assert.True(z.IsUtc);
    }

    [Theory]
    [InlineData("Z", "+00:00")]
    [InlineData("Z", "-00:00")]
    [InlineData("+00:00", "-00:00")]
    [InlineData("+05:30", "+0530")]
    public void Equals_GetHashCode_areConsistent_acrossEquivalentForms(string a, string b)
    {
        IsoTimeZone za = IsoTimeZone.Parse(a);
        IsoTimeZone zb = IsoTimeZone.Parse(b);
        Assert.Equal(za, zb);
        Assert.Equal(za.GetHashCode(), zb.GetHashCode());
    }

    [Fact]
    public void CompareTo_normalisesToTimeSpan()
    {
        IsoTimeZone za = IsoTimeZone.Parse("Z");
        IsoTimeZone zb = IsoTimeZone.Parse("-00:00");
        Assert.Equal(0, za.CompareTo(zb));
    }

    [Fact]
    public void Parse_FixAsPossible_rejects_egregiouslyOutOfRange()
    {
        // +99:99 is too far out to sensibly fix; reject even in
        // FixAsPossible mode.
        Assert.False(IsoTimeZone.TryParse("+99:99", IsoParseMode.FixAsPossible, out _));
    }
}
