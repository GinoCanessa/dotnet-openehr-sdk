using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Support;
using Xunit;

namespace DotnetOpenEhr.Rm.Tests;

public sealed class DataValueSmokeTests
{
    [Fact]
    public void DvText_ToString_ReturnsValue()
    {
        DvText t = new("hello");
        Assert.Equal("hello", t.ToString());
    }

    [Fact]
    public void DvCodedText_ToString_IncludesCode()
    {
        DvCodedText t = new("Heart rate",
            new CodePhrase(new TerminologyId { Value = "SNOMED-CT" }, "364075005"));
        Assert.Equal("Heart rate [SNOMED-CT::364075005]", t.ToString());
    }

    [Fact]
    public void CodePhrase_ToString_IsTerminologyColonColonCode()
    {
        CodePhrase c = new(new TerminologyId { Value = "openehr" }, "433");
        Assert.Equal("openehr::433", c.ToString());
    }

    [Fact]
    public void DvQuantity_ToString_FormatsMagnitudeAndUnits()
    {
        DvQuantity q = new(120, "mm[Hg]");
        Assert.Equal("120 mm[Hg]", q.ToString());
    }

    [Fact]
    public void DvCount_StoresIntegerMagnitude()
    {
        DvCount c = new(7);
        Assert.Equal(7, c.Magnitude);
    }

    [Fact]
    public void DvOrdered_CompareTo_OrdersByMagnitude()
    {
        DvQuantity a = new(80, "mm[Hg]");
        DvQuantity b = new(120, "mm[Hg]");
        Assert.True(a.CompareTo(b) < 0);
        Assert.True(b.CompareTo(a) > 0);
        Assert.Equal(0, a.CompareTo(new DvQuantity(80, "mm[Hg]")));
    }

    [Fact]
    public void DvDateTime_DefaultsToValidValue()
    {
        DvDateTime now = new(new IsoDateTime(new IsoDate(2024, 6, 15), new IsoTime(10, 30)));
        Assert.Equal("2024-06-15T10:30", now.Value.OriginalLexicalForm);
    }
}
