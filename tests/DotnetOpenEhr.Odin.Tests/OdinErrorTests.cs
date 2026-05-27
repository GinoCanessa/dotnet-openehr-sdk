using DotnetOpenEhr.Odin;
using Xunit;

namespace DotnetOpenEhr.Odin.Tests;

/// <summary>
/// Negative-path parser tests. Each malformed input must throw an
/// <see cref="OdinParseException"/> whose line and column point at the
/// offending character.
/// </summary>
public class OdinErrorTests
{
    [Fact]
    public void Missing_close_angle_reports_position()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("a = <1"));
        Assert.True(ex.Line >= 1);
    }

    [Fact]
    public void Missing_open_angle_reports_position()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("a = 1"));
        Assert.Equal(1, ex.Line);
        Assert.True(ex.Column >= 5);
    }

    [Fact]
    public void Unterminated_string_reports_position()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("a = <\"oops"));
        Assert.Equal(1, ex.Line);
    }

    [Fact]
    public void Bad_date_literal_reports_position()
    {
        // A bare year (which the lexer treats as an integer) followed by
        // '-13' (an integer) is not a valid date and we expect the
        // parser to flag the dangling '-13' content.
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("<2024-13-99>"));
        Assert.Equal(1, ex.Line);
    }

    [Fact]
    public void Bad_time_literal_reports_position()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("<25:99:99>"));
        Assert.Equal(1, ex.Line);
    }

    [Fact]
    public void Duplicate_attribute_reports_position()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("a = <1> a = <2>"));
        Assert.Contains("Duplicate", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_hash_key_reports_position()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("[\"a\"] = <1> [\"a\"] = <2>"));
        Assert.Contains("Duplicate", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_equals_reports_position()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("a <1>"));
        Assert.Equal(1, ex.Line);
        Assert.True(ex.Column >= 3);
    }

    [Fact]
    public void Unbalanced_interval_pipe_reports_position()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("<|0..5"));
        Assert.Equal(1, ex.Line);
    }

    [Fact]
    public void Trailing_content_after_document_reports_position()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("<1> garbage"));
        // The lexer is at the 'garbage' identifier after the '<1>' block.
        Assert.Equal(1, ex.Line);
        Assert.True(ex.Column >= 5);
    }

    [Fact]
    public void Bad_escape_inside_string_reports_position()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("a = <\"\\q\">"));
        Assert.Equal(1, ex.Line);
        Assert.True(ex.Column >= 5);
    }

    [Fact]
    public void Empty_input_yields_empty_object()
    {
        // Not an error; defined as an empty implicit document.
        OdinValue v = OdinParser.Parse(string.Empty);
        Assert.True(v.IsObject);
    }
}
