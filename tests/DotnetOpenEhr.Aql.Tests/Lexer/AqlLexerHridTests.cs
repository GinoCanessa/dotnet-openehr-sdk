using DotnetOpenEhr.Aql;
using DotnetOpenEhr.Aql.Lexer;
using Xunit;

namespace DotnetOpenEhr.Aql.Tests.Lexer;

/// <summary>
/// M12 (0604-04): pin the HRID scan terminator set in
/// <see cref="AqlLexer"/>. The scanner must stop at any character
/// outside the openEHR Archetype Identification spec § 3.2.1 body
/// charset (letter, digit, <c>_</c>, <c>-</c>, <c>.</c>) plus the
/// two-character <c>::</c> namespace separator. Tests assert the HRID
/// lexeme stops at the terminator and the next token is what the spec
/// would predict.
/// </summary>
public class AqlLexerHridTests
{
    private static List<(AqlTokenKind kind, string? text, string? value)> Lex(string source)
    {
        AqlLexer lexer = new(source.AsSpan());
        List<(AqlTokenKind, string?, string?)> all = [];
        while (true)
        {
            AqlToken t = lexer.NextToken();
            all.Add((t.Kind, t.Span.ToString(), t.Value));
            if (t.Kind == AqlTokenKind.EndOfFile)
            {
                break;
            }
        }
        return all;
    }

    [Fact]
    public void Hrid_terminates_at_open_paren()
    {
        List<(AqlTokenKind kind, string? text, string? value)> tokens =
            Lex("[openEHR-EHR-OBSERVATION.blood_pressure.v1(draft)]");
        Assert.Equal(AqlTokenKind.LeftBracket, tokens[0].kind);
        Assert.Equal(AqlTokenKind.ArchetypeHridLiteral, tokens[1].kind);
        Assert.Equal("openEHR-EHR-OBSERVATION.blood_pressure.v1", tokens[1].value);
        Assert.Equal(AqlTokenKind.LeftParen, tokens[2].kind);
    }

    [Fact]
    public void Hrid_terminates_at_left_angle()
    {
        List<(AqlTokenKind kind, string? text, string? value)> tokens =
            Lex("[openEHR-EHR-CLUSTER.parent.v1<some_path>]");
        Assert.Equal(AqlTokenKind.LeftBracket, tokens[0].kind);
        Assert.Equal(AqlTokenKind.ArchetypeHridLiteral, tokens[1].kind);
        Assert.Equal("openEHR-EHR-CLUSTER.parent.v1", tokens[1].value);
        Assert.Equal(AqlTokenKind.LessThan, tokens[2].kind);
    }

    [Fact]
    public void Hrid_terminates_at_newline_then_resumes_with_identifier()
    {
        List<(AqlTokenKind kind, string? text, string? value)> tokens =
            Lex("[openEHR-EHR-CLUSTER.x.v1\nfoo]");
        Assert.Equal(AqlTokenKind.LeftBracket, tokens[0].kind);
        Assert.Equal(AqlTokenKind.ArchetypeHridLiteral, tokens[1].kind);
        Assert.Equal("openEHR-EHR-CLUSTER.x.v1", tokens[1].value);
        Assert.Equal(AqlTokenKind.Identifier, tokens[2].kind);
        Assert.Equal("foo", tokens[2].value);
        Assert.Equal(AqlTokenKind.RightBracket, tokens[3].kind);
    }
}
