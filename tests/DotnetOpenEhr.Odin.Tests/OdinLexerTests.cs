using DotnetOpenEhr.Odin;
using Xunit;

namespace DotnetOpenEhr.Odin.Tests;

/// <summary>
/// Single-token lexer tests, one positive case per emitted
/// <see cref="OdinTokenKind"/> plus a representative set of negative
/// cases that assert position-aware error reporting.
/// </summary>
public class OdinLexerTests
{
    private static List<OdinTokenSnapshot> Lex(string source)
    {
        OdinLexer lexer = new(source.AsSpan());
        List<OdinTokenSnapshot> tokens = [];
        while (true)
        {
            OdinToken t = lexer.NextToken();
            tokens.Add(new OdinTokenSnapshot(t.Kind, t.Text, t.Line, t.Column));
            if (t.Kind == OdinTokenKind.EndOfFile) break;
        }
        return tokens;
    }

    private static OdinTokenSnapshot LexSingle(string source)
    {
        List<OdinTokenSnapshot> all = Lex(source);
        Assert.Equal(2, all.Count);
        Assert.Equal(OdinTokenKind.EndOfFile, all[1].Kind);
        return all[0];
    }

    public readonly record struct OdinTokenSnapshot(OdinTokenKind Kind, string Text, int Line, int Column);

    [Theory]
    [InlineData("<", OdinTokenKind.LeftAngle)]
    [InlineData(">", OdinTokenKind.RightAngle)]
    [InlineData("(", OdinTokenKind.LeftParen)]
    [InlineData(")", OdinTokenKind.RightParen)]
    [InlineData("[", OdinTokenKind.LeftBracket)]
    [InlineData("]", OdinTokenKind.RightBracket)]
    [InlineData("=", OdinTokenKind.Equals)]
    [InlineData(",", OdinTokenKind.Comma)]
    [InlineData(";", OdinTokenKind.Semicolon)]
    [InlineData("|", OdinTokenKind.Pipe)]
    [InlineData("/", OdinTokenKind.Slash)]
    [InlineData("@", OdinTokenKind.AtSign)]
    [InlineData("<=", OdinTokenKind.LessEqual)]
    [InlineData(">=", OdinTokenKind.GreaterEqual)]
    [InlineData("..", OdinTokenKind.Range)]
    [InlineData("...", OdinTokenKind.Ellipsis)]
    [InlineData("+/-", OdinTokenKind.PlusMinus)]
    [InlineData("\u00b1", OdinTokenKind.PlusMinus)]
    public void Single_punctuation_tokens(string text, OdinTokenKind expected)
    {
        OdinTokenSnapshot t = LexSingle(text);
        Assert.Equal(expected, t.Kind);
        Assert.Equal(1, t.Line);
        Assert.Equal(1, t.Column);
    }

    [Fact]
    public void Identifier_token()
    {
        OdinTokenSnapshot t = LexSingle("description");
        Assert.Equal(OdinTokenKind.Identifier, t.Kind);
        Assert.Equal("description", t.Text);
    }

    [Theory]
    [InlineData("True")]
    [InlineData("false")]
    [InlineData("TRUE")]
    public void Boolean_tokens(string text)
    {
        OdinTokenSnapshot t = LexSingle(text);
        Assert.Equal(OdinTokenKind.BooleanLiteral, t.Kind);
        Assert.Equal(text, t.Text);
    }

    [Theory]
    [InlineData("25")]
    [InlineData("300000")]
    [InlineData("-7")]
    [InlineData("29e6")]
    [InlineData("0")]
    public void Integer_literal(string text)
    {
        OdinTokenSnapshot t = LexSingle(text);
        Assert.Equal(OdinTokenKind.IntegerLiteral, t.Kind);
        Assert.Equal(text, t.Text);
    }

    [Theory]
    [InlineData("25.0")]
    [InlineData("3.1415926")]
    [InlineData("6.023e23")]
    [InlineData("-2.5e-4")]
    public void Real_literal(string text)
    {
        OdinTokenSnapshot t = LexSingle(text);
        Assert.Equal(OdinTokenKind.RealLiteral, t.Kind);
        Assert.Equal(text, t.Text);
    }

    [Theory]
    [InlineData("\"hello\"", "hello")]
    [InlineData("\"a\\nb\"", "a\nb")]
    [InlineData("\"q\\\"q\"", "q\"q")]
    [InlineData("\"\\\\\"", "\\")]
    [InlineData("\"\\u00e9\"", "\u00e9")]
    public void String_literal(string source, string expected)
    {
        OdinTokenSnapshot t = LexSingle(source);
        Assert.Equal(OdinTokenKind.StringLiteral, t.Kind);
        Assert.Equal(expected, t.Text);
    }

    [Fact]
    public void Char_literal()
    {
        OdinTokenSnapshot t = LexSingle("'a'");
        Assert.Equal(OdinTokenKind.CharLiteral, t.Kind);
        Assert.Equal("a", t.Text);
    }

    [Theory]
    [InlineData("2024-05-27")]
    [InlineData("2024-05")]
    [InlineData("2024-??-??")]
    [InlineData("2024-05-??")]
    public void Date_literal(string text)
    {
        OdinTokenSnapshot t = LexSingle(text);
        Assert.Equal(OdinTokenKind.DateLiteral, t.Kind);
        Assert.Equal(text, t.Text);
    }

    [Theory]
    [InlineData("08:30:00")]
    [InlineData("08:30")]
    [InlineData("16:35:04,5")]
    [InlineData("16:35:04.5")]
    [InlineData("12:00:00Z")]
    [InlineData("12:00:00+10:00")]
    public void Time_literal(string text)
    {
        OdinTokenSnapshot t = LexSingle(text);
        Assert.Equal(OdinTokenKind.TimeLiteral, t.Kind);
        Assert.Equal(text, t.Text);
    }

    [Theory]
    [InlineData("2001-05-12T07:35:20+10:00")]
    [InlineData("2024-05-27T10:25:03Z")]
    [InlineData("2024-05-27T10:25")]
    public void DateTime_literal(string text)
    {
        OdinTokenSnapshot t = LexSingle(text);
        Assert.Equal(OdinTokenKind.DateTimeLiteral, t.Kind);
        Assert.Equal(text, t.Text);
    }

    [Theory]
    [InlineData("P1Y2M")]
    [InlineData("PT4H5M6S")]
    [InlineData("P22DT4H15M0S")]
    [InlineData("P1Y2M3DT4H5M6S")]
    public void Duration_literal(string text)
    {
        OdinTokenSnapshot t = LexSingle(text);
        Assert.Equal(OdinTokenKind.DurationLiteral, t.Kind);
        Assert.Equal(text, t.Text);
    }

    [Fact]
    public void Comments_are_skipped()
    {
        List<OdinTokenSnapshot> tokens = Lex("-- a comment\n  description");
        Assert.Equal(2, tokens.Count);
        Assert.Equal(OdinTokenKind.Identifier, tokens[0].Kind);
        Assert.Equal("description", tokens[0].Text);
        Assert.Equal(2, tokens[0].Line);
        Assert.Equal(3, tokens[0].Column);
    }

    [Fact]
    public void Multiline_position_tracking()
    {
        // Two tokens on different lines.
        List<OdinTokenSnapshot> tokens = Lex("a\n  b");
        Assert.Equal(OdinTokenKind.Identifier, tokens[0].Kind);
        Assert.Equal(1, tokens[0].Line);
        Assert.Equal(1, tokens[0].Column);
        Assert.Equal(OdinTokenKind.Identifier, tokens[1].Kind);
        Assert.Equal(2, tokens[1].Line);
        Assert.Equal(3, tokens[1].Column);
    }

    [Fact]
    public void Unterminated_string_reports_position()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(() =>
        {
            OdinLexer lexer = new("\"oops".AsSpan());
            lexer.NextToken();
        });
        Assert.Equal(1, ex.Line);
        Assert.Equal(1, ex.Column);
    }

    [Fact]
    public void Invalid_escape_reports_position()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(() =>
        {
            OdinLexer lexer = new("\"\\q\"".AsSpan());
            lexer.NextToken();
        });
        Assert.Equal(1, ex.Line);
        Assert.Equal(1, ex.Column);
    }

    [Fact]
    public void Unterminated_char_reports_position()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(() =>
        {
            OdinLexer lexer = new("'a".AsSpan());
            lexer.NextToken();
        });
        Assert.Equal(1, ex.Line);
        Assert.Equal(1, ex.Column);
    }

    [Fact]
    public void Stray_dot_reports_position()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(() =>
        {
            OdinLexer lexer = new(".".AsSpan());
            lexer.NextToken();
        });
        Assert.Equal(1, ex.Line);
        Assert.Equal(1, ex.Column);
    }

    [Fact]
    public void Stray_plus_reports_position()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(() =>
        {
            OdinLexer lexer = new("  +".AsSpan());
            lexer.NextToken();
        });
        Assert.Equal(1, ex.Line);
        Assert.Equal(3, ex.Column);
    }

    [Fact]
    public void Unknown_char_reports_position()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(() =>
        {
            OdinLexer lexer = new("\n  $".AsSpan());
            lexer.NextToken();
        });
        Assert.Equal(2, ex.Line);
        Assert.Equal(3, ex.Column);
    }

    [Fact]
    public void Unicode_escape_4_or_8_hex_required()
    {
        OdinParseException ex = Assert.Throws<OdinParseException>(() =>
        {
            OdinLexer lexer = new("\"\\u00\"".AsSpan());
            lexer.NextToken();
        });
        Assert.Equal(1, ex.Line);
    }

    [Fact]
    public void Range_distinguishes_from_decimal()
    {
        // "1..5" - first '..' starts as integer then range token.
        List<OdinTokenSnapshot> tokens = Lex("1..5");
        Assert.Equal(OdinTokenKind.IntegerLiteral, tokens[0].Kind);
        Assert.Equal("1", tokens[0].Text);
        Assert.Equal(OdinTokenKind.Range, tokens[1].Kind);
        Assert.Equal(OdinTokenKind.IntegerLiteral, tokens[2].Kind);
        Assert.Equal("5", tokens[2].Text);
    }

    [Fact]
    public void Terminology_code_body_bare_form()
    {
        OdinLexer lexer = new("[at0001]".AsSpan());
        OdinToken open = lexer.NextToken();
        Assert.Equal(OdinTokenKind.LeftBracket, open.Kind);
        bool ok = lexer.TryReadTerminologyCodeBody(out string id, out string code, out _, out _);
        Assert.True(ok);
        Assert.Equal("local", id);
        Assert.Equal("at0001", code);
    }

    [Fact]
    public void Terminology_code_body_full_form()
    {
        OdinLexer lexer = new("[ISO_639-1::en]".AsSpan());
        lexer.NextToken();
        bool ok = lexer.TryReadTerminologyCodeBody(out string id, out string code, out _, out _);
        Assert.True(ok);
        Assert.Equal("ISO_639-1", id);
        Assert.Equal("en", code);
    }

    [Fact]
    public void Terminology_code_body_rejects_string_key()
    {
        OdinLexer lexer = new("[\"en\"]".AsSpan());
        lexer.NextToken();
        bool ok = lexer.TryReadTerminologyCodeBody(out _, out _, out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void Type_marker_body_with_nested_generics()
    {
        OdinLexer lexer = new("(List<HOTEL>)".AsSpan());
        OdinToken open = lexer.NextToken();
        Assert.Equal(OdinTokenKind.LeftParen, open.Kind);
        string body = lexer.ReadTypeMarkerBody();
        Assert.Equal("List<HOTEL>", body);
    }
}
