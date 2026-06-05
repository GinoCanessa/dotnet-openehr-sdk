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

    [Theory]
    [InlineData("P2147483648Y")]
    [InlineData("P2147483648M")]
    [InlineData("P2147483648W")]
    [InlineData("P2147483648D")]
    [InlineData("PT2147483648H")]
    [InlineData("PT2147483648M")]
    public void IsoDuration_TryParse_OversizedIntegerComponent_ReturnsFalse(string text)
    {
        bool parsed = IsoDuration.TryParse(text, out IsoDuration? value);

        Assert.False(parsed);
        Assert.Null(value);
    }

    [Theory]
    [InlineData("P2147483648Y")]
    [InlineData("P2147483648M")]
    [InlineData("P2147483648W")]
    [InlineData("P2147483648D")]
    [InlineData("PT2147483648H")]
    [InlineData("PT2147483648M")]
    public void IsoDuration_Parse_OversizedIntegerComponent_ThrowsFormatException(string text)
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

    /// <summary>
    /// H7 — canonical zero-only forms must parse as legal ISO 8601
    /// durations, not raise FormatException. PT0S, PT0H, PT0M, P0D,
    /// P0W, P0Y are all spec-permitted.
    /// </summary>
    [Theory]
    [InlineData("PT0S")]
    [InlineData("PT0H")]
    [InlineData("PT0M")]
    [InlineData("P0D")]
    [InlineData("P0W")]
    [InlineData("P0Y")]
    [InlineData("P0M")]
    public void Parse_acceptsAllZeroOnlyForms(string text)
    {
        IsoDuration d = IsoDuration.Parse(text);
        Assert.NotNull(d);
        Assert.Equal(text, d.OriginalLexicalForm);
    }

    [Fact]
    public void Parse_acceptsMixedZeroAndNonzero_PT0H1M0S()
    {
        IsoDuration d = IsoDuration.Parse("PT0H1M0S");
        Assert.Equal(1, d.Minutes);
        Assert.Equal("PT0H1M0S", d.OriginalLexicalForm);
    }

    [Theory]
    [InlineData("P")]
    [InlineData("PT")]
    public void Parse_rejectsBareP_andBarePT(string text)
    {
        Assert.Throws<FormatException>(() => IsoDuration.Parse(text));
    }
}
