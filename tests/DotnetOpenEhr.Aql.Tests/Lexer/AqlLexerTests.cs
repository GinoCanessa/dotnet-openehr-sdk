using DotnetOpenEhr.Aql;
using DotnetOpenEhr.Aql.Lexer;
using Xunit;

namespace DotnetOpenEhr.Aql.Tests.Lexer;

/// <summary>
/// Lexer-level tests: keyword case-insensitivity, identifiers, each
/// literal kind, paths, predicates, operators, line/column tracking,
/// and error positions.
/// </summary>
public class AqlLexerTests
{
    private static List<AqlTokenSnapshot> Lex(string source)
    {
        AqlLexer lexer = new(source.AsSpan());
        List<AqlTokenSnapshot> tokens = [];
        while (true)
        {
            AqlToken t = lexer.NextToken();
            tokens.Add(new AqlTokenSnapshot(t.Kind, t.Span.ToString(), t.Value, t.EmbeddedNodeId, t.Line, t.Column));
            if (t.Kind == AqlTokenKind.EndOfFile)
            {
                break;
            }
        }
        return tokens;
    }

    private static AqlTokenSnapshot LexSingle(string source)
    {
        List<AqlTokenSnapshot> all = Lex(source);
        Assert.Equal(2, all.Count);
        Assert.Equal(AqlTokenKind.EndOfFile, all[1].Kind);
        return all[0];
    }

    public readonly record struct AqlTokenSnapshot(
        AqlTokenKind Kind,
        string Text,
        string? Value,
        string? EmbeddedNodeId,
        int Line,
        int Column);

    [Theory]
    [InlineData("(", AqlTokenKind.LeftParen)]
    [InlineData(")", AqlTokenKind.RightParen)]
    [InlineData("[", AqlTokenKind.LeftBracket)]
    [InlineData("]", AqlTokenKind.RightBracket)]
    [InlineData("{", AqlTokenKind.LeftBrace)]
    [InlineData("}", AqlTokenKind.RightBrace)]
    [InlineData(",", AqlTokenKind.Comma)]
    [InlineData(".", AqlTokenKind.Dot)]
    [InlineData(";", AqlTokenKind.Semicolon)]
    [InlineData("=", AqlTokenKind.Equals)]
    [InlineData("!=", AqlTokenKind.NotEqual)]
    [InlineData("<", AqlTokenKind.LessThan)]
    [InlineData("<=", AqlTokenKind.LessEqual)]
    [InlineData(">", AqlTokenKind.GreaterThan)]
    [InlineData(">=", AqlTokenKind.GreaterEqual)]
    [InlineData("+", AqlTokenKind.Plus)]
    [InlineData("-", AqlTokenKind.Minus)]
    [InlineData("*", AqlTokenKind.Star)]
    [InlineData("||", AqlTokenKind.Concat)]
    public void Punctuation_and_operators(string text, AqlTokenKind expected)
    {
        AqlTokenSnapshot t = LexSingle(text);
        Assert.Equal(expected, t.Kind);
        Assert.Equal(1, t.Line);
        Assert.Equal(1, t.Column);
    }

    [Theory]
    [InlineData("SELECT", AqlTokenKind.Select)]
    [InlineData("select", AqlTokenKind.Select)]
    [InlineData("Select", AqlTokenKind.Select)]
    [InlineData("FROM", AqlTokenKind.From)]
    [InlineData("where", AqlTokenKind.Where)]
    [InlineData("ORDER", AqlTokenKind.Order)]
    [InlineData("by", AqlTokenKind.By)]
    [InlineData("LIMIT", AqlTokenKind.Limit)]
    [InlineData("offset", AqlTokenKind.Offset)]
    [InlineData("CONTAINS", AqlTokenKind.Contains)]
    [InlineData("EHR", AqlTokenKind.Ehr)]
    [InlineData("composition", AqlTokenKind.Composition)]
    [InlineData("AND", AqlTokenKind.And)]
    [InlineData("or", AqlTokenKind.Or)]
    [InlineData("NOT", AqlTokenKind.Not)]
    [InlineData("EXISTS", AqlTokenKind.Exists)]
    [InlineData("MATCHES", AqlTokenKind.Matches)]
    [InlineData("LIKE", AqlTokenKind.Like)]
    [InlineData("IS", AqlTokenKind.Is)]
    [InlineData("NULL", AqlTokenKind.Null)]
    [InlineData("TRUE", AqlTokenKind.True)]
    [InlineData("FALSE", AqlTokenKind.False)]
    [InlineData("ASC", AqlTokenKind.Asc)]
    [InlineData("ascending", AqlTokenKind.Asc)]
    [InlineData("DESC", AqlTokenKind.Desc)]
    [InlineData("descending", AqlTokenKind.Desc)]
    [InlineData("AS", AqlTokenKind.As)]
    [InlineData("DISTINCT", AqlTokenKind.Distinct)]
    [InlineData("TOP", AqlTokenKind.Top)]
    [InlineData("BACKWARD", AqlTokenKind.Backward)]
    [InlineData("FORWARD", AqlTokenKind.Forward)]
    public void Keywords_case_insensitive(string text, AqlTokenKind expected)
    {
        AqlTokenSnapshot t = LexSingle(text);
        Assert.Equal(expected, t.Kind);
    }

    [Fact]
    public void Identifier_token()
    {
        AqlTokenSnapshot t = LexSingle("composition_id");
        Assert.Equal(AqlTokenKind.Identifier, t.Kind);
        Assert.Equal("composition_id", t.Value);
    }

    [Fact]
    public void Identifier_distinct_from_keyword()
    {
        AqlTokenSnapshot t = LexSingle("selected"); // not SELECT
        Assert.Equal(AqlTokenKind.Identifier, t.Kind);
    }

    [Theory]
    [InlineData("0", AqlTokenKind.IntegerLiteral)]
    [InlineData("42", AqlTokenKind.IntegerLiteral)]
    [InlineData("12345", AqlTokenKind.IntegerLiteral)]
    [InlineData("3.14", AqlTokenKind.RealLiteral)]
    [InlineData("0.5", AqlTokenKind.RealLiteral)]
    [InlineData("1e10", AqlTokenKind.RealLiteral)]
    [InlineData("1.5e-3", AqlTokenKind.RealLiteral)]
    public void Numeric_literals(string text, AqlTokenKind expected)
    {
        AqlTokenSnapshot t = LexSingle(text);
        Assert.Equal(expected, t.Kind);
        Assert.Equal(text, t.Text);
    }

    [Fact]
    public void String_literal_single_quotes()
    {
        AqlTokenSnapshot t = LexSingle("'hello world'");
        Assert.Equal(AqlTokenKind.StringLiteral, t.Kind);
        Assert.Equal("hello world", t.Value);
    }

    [Fact]
    public void String_literal_double_quotes()
    {
        AqlTokenSnapshot t = LexSingle("\"alert\"");
        Assert.Equal(AqlTokenKind.StringLiteral, t.Kind);
        Assert.Equal("alert", t.Value);
    }

    [Fact]
    public void String_literal_with_escape()
    {
        AqlTokenSnapshot t = LexSingle("'it\\'s'");
        Assert.Equal(AqlTokenKind.StringLiteral, t.Kind);
        Assert.Equal("it's", t.Value);
    }

    [Fact]
    public void Placeholder_token()
    {
        AqlTokenSnapshot t = LexSingle("$ehrUid");
        Assert.Equal(AqlTokenKind.Placeholder, t.Kind);
        Assert.Equal("ehrUid", t.Value);
    }

    [Theory]
    [InlineData("at0001", AqlTokenKind.AtCode)]
    [InlineData("at0001.5", AqlTokenKind.AtCode)]
    [InlineData("ac0003", AqlTokenKind.AcCode)]
    [InlineData("id3", AqlTokenKind.IdCode)]
    [InlineData("id3.1", AqlTokenKind.IdCode)]
    public void Adl_codes_recognised(string text, AqlTokenKind expected)
    {
        AqlTokenSnapshot t = LexSingle(text);
        Assert.Equal(expected, t.Kind);
        Assert.Equal(text, t.Value);
    }

    [Fact]
    public void Path_segment_with_embedded_at_code()
    {
        List<AqlTokenSnapshot> tokens = Lex("c/data[at0001]/items");
        Assert.Equal(AqlTokenKind.Identifier, tokens[0].Kind);
        Assert.Equal("c", tokens[0].Value);
        Assert.Equal(AqlTokenKind.PathSegment, tokens[1].Kind);
        Assert.Equal("data", tokens[1].Value);
        Assert.Equal("at0001", tokens[1].EmbeddedNodeId);
        Assert.Equal(AqlTokenKind.PathSegment, tokens[2].Kind);
        Assert.Equal("items", tokens[2].Value);
        Assert.Equal(AqlTokenKind.EndOfFile, tokens[3].Kind);
    }

    [Fact]
    public void Path_segment_with_embedded_id_code()
    {
        List<AqlTokenSnapshot> tokens = Lex("/items[id4]");
        Assert.Equal(AqlTokenKind.PathSegment, tokens[0].Kind);
        Assert.Equal("items", tokens[0].Value);
        Assert.Equal("id4", tokens[0].EmbeddedNodeId);
    }

    [Fact]
    public void Archetype_hrid_inside_brackets()
    {
        List<AqlTokenSnapshot> tokens = Lex("[openEHR-EHR-OBSERVATION.blood_pressure.v2]");
        Assert.Equal(AqlTokenKind.LeftBracket, tokens[0].Kind);
        Assert.Equal(AqlTokenKind.ArchetypeHridLiteral, tokens[1].Kind);
        Assert.Equal("openEHR-EHR-OBSERVATION.blood_pressure.v2", tokens[1].Value);
        Assert.Equal(AqlTokenKind.RightBracket, tokens[2].Kind);
    }

    [Fact]
    public void Line_and_column_tracking_across_newlines()
    {
        List<AqlTokenSnapshot> tokens = Lex("SELECT c\nFROM EHR e");
        // SELECT @ 1:1, c @ 1:8, FROM @ 2:1, EHR @ 2:6, e @ 2:10
        Assert.Equal(AqlTokenKind.Select, tokens[0].Kind);
        Assert.Equal(1, tokens[0].Line);
        Assert.Equal(1, tokens[0].Column);
        Assert.Equal("c", tokens[1].Value);
        Assert.Equal(1, tokens[1].Line);
        Assert.Equal(8, tokens[1].Column);
        Assert.Equal(AqlTokenKind.From, tokens[2].Kind);
        Assert.Equal(2, tokens[2].Line);
        Assert.Equal(1, tokens[2].Column);
        Assert.Equal(AqlTokenKind.Ehr, tokens[3].Kind);
        Assert.Equal(2, tokens[3].Line);
        Assert.Equal(6, tokens[3].Column);
    }

    [Fact]
    public void Line_comment_is_skipped()
    {
        List<AqlTokenSnapshot> tokens = Lex("SELECT -- comment to end of line\nc");
        Assert.Equal(AqlTokenKind.Select, tokens[0].Kind);
        Assert.Equal("c", tokens[1].Value);
        Assert.Equal(2, tokens[1].Line);
    }

    [Fact]
    public void Iso_datetime_literal_string_shape()
    {
        AqlTokenSnapshot t = LexSingle("'2024-05-27T10:25:03Z'");
        Assert.Equal(AqlTokenKind.StringLiteral, t.Kind);
        Assert.Equal("2024-05-27T10:25:03Z", t.Value);
    }

    [Fact]
    public void Unterminated_string_reports_position()
    {
        AqlLexException ex = Assert.Throws<AqlLexException>(() =>
        {
            AqlLexer lexer = new("SELECT 'oops".AsSpan());
            while (true)
            {
                AqlToken t = lexer.NextToken();
                if (t.Kind == AqlTokenKind.EndOfFile) break;
            }
        });
        Assert.Equal(1, ex.Line);
        Assert.Equal(8, ex.Column);
    }

    [Fact]
    public void Bad_bang_reports_position()
    {
        AqlLexException ex = Assert.Throws<AqlLexException>(() =>
        {
            AqlLexer lexer = new("a ! b".AsSpan());
            while (true)
            {
                AqlToken t = lexer.NextToken();
                if (t.Kind == AqlTokenKind.EndOfFile) break;
            }
        });
        Assert.Equal(1, ex.Line);
        Assert.Equal(3, ex.Column);
    }

    [Fact]
    public void Placeholder_without_identifier_errors()
    {
        AqlLexException ex = Assert.Throws<AqlLexException>(() =>
        {
            AqlLexer lexer = new("$ ".AsSpan());
            while (true)
            {
                AqlToken t = lexer.NextToken();
                if (t.Kind == AqlTokenKind.EndOfFile) break;
            }
        });
        Assert.Equal(1, ex.Line);
        Assert.Equal(1, ex.Column);
    }

    [Fact]
    public void Sequence_of_tokens_select_clause()
    {
        List<AqlTokenSnapshot> tokens = Lex("SELECT c FROM EHR e");
        Assert.Equal(AqlTokenKind.Select, tokens[0].Kind);
        Assert.Equal(AqlTokenKind.Identifier, tokens[1].Kind);
        Assert.Equal(AqlTokenKind.From, tokens[2].Kind);
        Assert.Equal(AqlTokenKind.Ehr, tokens[3].Kind);
        Assert.Equal(AqlTokenKind.Identifier, tokens[4].Kind);
        Assert.Equal(AqlTokenKind.EndOfFile, tokens[5].Kind);
    }
}
