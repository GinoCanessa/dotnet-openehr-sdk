using DotnetOpenEhr.Odin;
using Xunit;

namespace DotnetOpenEhr.Odin.Tests;

/// <summary>
/// Pin tests for H9: every <see cref="OdinParseException"/> thrown by
/// <see cref="OdinParser"/> must populate <see cref="OdinParseException.Snippet"/>
/// so the formatter's <c>(near '...')</c> message suffix fires. The
/// suffix is gated on <c>Snippet != null</c> in
/// <c>OdinParseException.FormatMessage</c>.
/// </summary>
public class OdinParserSnippetTests
{
    [Fact]
    public void Bare_equals_with_no_value_populates_snippet()
    {
        // `a = 1` — the leaf is not wrapped in `< … >` so the parser
        // throws at the '1' token from ParseTypedBlock.
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("a = 1"));
        Assert.NotNull(ex.Snippet);
        Assert.False(string.IsNullOrEmpty(ex.Snippet));
        Assert.Contains("(near '", ex.Message);
    }

    [Fact]
    public void Unterminated_string_populates_snippet()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("a = <\"oops"));
        Assert.NotNull(ex.Snippet);
        Assert.False(string.IsNullOrEmpty(ex.Snippet));
        Assert.Contains("(near '", ex.Message);
    }

    [Fact]
    public void Malformed_real_literal_populates_snippet()
    {
        // A real literal with a malformed exponent forces ParseReal
        // through its throw path.
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("a = <1.2e>"));
        Assert.NotNull(ex.Snippet);
        Assert.False(string.IsNullOrEmpty(ex.Snippet));
        Assert.Contains("(near '", ex.Message);
    }

    [Fact]
    public void Malformed_date_literal_populates_snippet()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("<2024-13-99>"));
        Assert.NotNull(ex.Snippet);
        Assert.False(string.IsNullOrEmpty(ex.Snippet));
        Assert.Contains("(near '", ex.Message);
    }

    [Fact]
    public void Integer_overflow_populates_snippet()
    {
        // 1e30 overflows Int64 in the integer-with-exponent path.
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("a = <1e30>"));
        Assert.NotNull(ex.Snippet);
        Assert.False(string.IsNullOrEmpty(ex.Snippet));
        Assert.Contains("(near '", ex.Message);
    }

    [Fact]
    public void Snippet_escapes_newline_to_keep_message_single_line()
    {
        // Position the error at the start of a CR/LF so the slice
        // that BuildSnippet returns straddles a line break.
        OdinParseException ex = Assert.Throws<OdinParseException>(
            () => OdinParser.Parse("a = 1\r\nb = 2"));
        Assert.NotNull(ex.Snippet);
        Assert.DoesNotContain('\r', ex.Snippet!);
        Assert.DoesNotContain('\n', ex.Snippet!);
        // The formatted message must also remain single-line.
        Assert.DoesNotContain('\n', ex.Message);
    }
}
