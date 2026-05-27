using DotnetOpenEhr.Foundation.Iso;
using Xunit;

namespace DotnetOpenEhr.Foundation.Tests;

public class IsoDurationTests
{
    [Theory]
    [InlineData("P1Y", 1, 0, 0, 0, 0, 0, 0.0)]
    [InlineData("P1Y2M3DT4H5M6.789S", 1, 2, 0, 3, 4, 5, 6.789)]
    [InlineData("PT0.5S", 0, 0, 0, 0, 0, 0, 0.5)]
    [InlineData("P3W", 0, 0, 3, 0, 0, 0, 0.0)]
    [InlineData("PT1H30M", 0, 0, 0, 0, 1, 30, 0.0)]
    public void Parse_round_trip_preserves_lexical_form(string text, int y, int mo, int w, int d, int h, int mi, double s)
    {
        IsoDuration parsed = IsoDuration.Parse(text);

        Assert.Equal(y, parsed.Years);
        Assert.Equal(mo, parsed.Months);
        Assert.Equal(w, parsed.Weeks);
        Assert.Equal(d, parsed.Days);
        Assert.Equal(h, parsed.Hours);
        Assert.Equal(mi, parsed.Minutes);
        Assert.Equal((decimal)s, parsed.Seconds);
        Assert.False(parsed.IsNegative);
        Assert.Equal(text, parsed.OriginalLexicalForm);
        Assert.Equal(text, parsed.ToString());
    }

    [Theory]
    [InlineData("-P1Y")]
    [InlineData("+PT2H")]
    public void Parse_sign_handling(string text)
    {
        IsoDuration parsed = IsoDuration.Parse(text);
        Assert.Equal(text, parsed.ToString());
        Assert.Equal(text.StartsWith('-'), parsed.IsNegative);
    }

    [Theory]
    [InlineData("P")]
    [InlineData("1Y")]
    [InlineData("PT")]
    [InlineData("P1YT")]
    [InlineData("PT1.5H")] // hours must be integer
    public void Parse_invalid_text_throws(string text)
    {
        Assert.Throws<FormatException>(() => IsoDuration.Parse(text));
    }

    [Fact]
    public void Canonical_format_matches_input_when_constructed_from_components()
    {
        IsoDuration d = new IsoDuration(years: 1, months: 2, days: 3, hours: 4, minutes: 5, seconds: 6.789m);
        Assert.Equal("P1Y2M3DT4H5M6.789S", d.OriginalLexicalForm);
    }

    [Fact]
    public void Zero_duration_canonical_form()
    {
        IsoDuration d = new IsoDuration();
        Assert.Equal("PT0S", d.OriginalLexicalForm);
    }
}
