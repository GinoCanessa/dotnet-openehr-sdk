using DotnetOpenEhr.Foundation.Iso;
using Xunit;

namespace DotnetOpenEhr.Foundation.Tests;

public class IsoTimeTests
{
    [Theory]
    [InlineData("10", 10, null, null, null)]
    [InlineData("10:25", 10, 25, null, null)]
    [InlineData("10:25:03", 10, 25, 3, null)]
    [InlineData("10:25:03.500", 10, 25, 3, 0.500)]
    [InlineData("10:25:03,500", 10, 25, 3, 0.500)]
    public void Parse_partial_precision(string text, int hour, int? minute, int? second, double? fractional)
    {
        IsoTime parsed = IsoTime.Parse(text);
        Assert.Equal(hour, parsed.Hour);
        Assert.Equal(minute, parsed.Minute);
        Assert.Equal(second, parsed.Second);
        if (fractional is null)
        {
            Assert.Null(parsed.FractionalSecond);
        }
        else
        {
            Assert.Equal((decimal)fractional.Value, parsed.FractionalSecond!.Value);
        }
        Assert.Equal(text, parsed.OriginalLexicalForm);
    }

    [Theory]
    [InlineData("10:25:03Z")]
    [InlineData("10:25:03+02:00")]
    [InlineData("10:25:03.123-05:30")]
    public void Parse_with_timezone(string text)
    {
        IsoTime parsed = IsoTime.Parse(text);
        Assert.NotNull(parsed.TimeZone);
        Assert.Equal(text, parsed.ToString());
    }

    [Theory]
    [InlineData("24:00")]
    [InlineData("10:60")]
    [InlineData("not-a-time")]
    [InlineData("")]
    public void Parse_invalid_text_throws(string text)
    {
        Assert.Throws<FormatException>(() => IsoTime.Parse(text));
    }

    [Fact]
    public void IsoTime_CompareTo_OrdersOffsetTimesByReferenceDayInstant()
    {
        IsoTime earlier = IsoTime.Parse("10:00:00+02:00");
        IsoTime later = IsoTime.Parse("09:00:00Z");
        IsoTime equivalent = IsoTime.Parse("08:00:00Z");

        Assert.True(earlier.CompareTo(later) < 0);
        Assert.Equal(0, earlier.CompareTo(equivalent));
    }

    [Fact]
    public void IsoTime_CompareTo_PreservesLocalOrdering_WhenTimezoneMissing()
    {
        IsoTime withTimezone = IsoTime.Parse("10:00:00+02:00");
        IsoTime withoutTimezone = IsoTime.Parse("09:00:00");

        Assert.True(withTimezone.CompareTo(withoutTimezone) > 0);
    }
}
