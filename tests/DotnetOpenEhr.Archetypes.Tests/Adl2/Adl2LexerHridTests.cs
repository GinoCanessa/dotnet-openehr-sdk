using System.Linq;
using DotnetOpenEhr.Archetypes.Adl2;
using Xunit;

namespace DotnetOpenEhr.Archetypes.Tests.Adl2;

/// <summary>
/// M12 (0604-04): pin the HRID scan terminator set in
/// <see cref="Adl2Lexer"/>. The scanner must stop at any character
/// outside the openEHR Archetype Identification spec § 3.2.1 body
/// charset (letter, digit, <c>_</c>, <c>-</c>, <c>.</c>). Tests assert
/// the HRID lexeme stops at the terminator and the next non-newline
/// token is what the spec would predict.
/// </summary>
public class Adl2LexerHridTests
{
    private static (Adl2TokenKind kind, string value)[] LexNoNewlines(string source)
    {
        List<(Adl2TokenKind, string)> all = [];
        Adl2Lexer lexer = new(source.AsSpan());
        while (true)
        {
            Adl2Token tok = lexer.NextToken();
            if (tok.Kind != Adl2TokenKind.Newline)
            {
                all.Add((tok.Kind, tok.Text));
            }
            if (tok.Kind == Adl2TokenKind.Eof)
            {
                break;
            }
        }
        return [.. all];
    }

    [Fact]
    public void Hrid_terminates_at_open_paren()
    {
        (Adl2TokenKind kind, string value)[] tokens =
            LexNoNewlines("archetype openEHR-EHR-OBSERVATION.blood_pressure.v1(draft)");
        Assert.Equal(Adl2TokenKind.Keyword, tokens[0].kind);
        Assert.Equal(Adl2TokenKind.ArchetypeHridLiteral, tokens[1].kind);
        Assert.Equal("openEHR-EHR-OBSERVATION.blood_pressure.v1", tokens[1].value);
        Assert.Equal(Adl2TokenKind.LParen, tokens[2].kind);
    }

    [Fact]
    public void Hrid_terminates_at_specialisation_marker()
    {
        // Use a simple `<key>` body so we exercise HRID termination at
        // '<' without dragging the OdinBlock lexer into invalid input.
        (Adl2TokenKind kind, string value)[] tokens =
            LexNoNewlines("archetype openEHR-EHR-CLUSTER.parent.v1<thing>");
        Assert.Equal(Adl2TokenKind.Keyword, tokens[0].kind);
        Assert.Equal(Adl2TokenKind.ArchetypeHridLiteral, tokens[1].kind);
        Assert.Equal("openEHR-EHR-CLUSTER.parent.v1", tokens[1].value);
        // The token after the HRID may be either a bare LessThan or an
        // OdinBlock spanning the `<...>`; both shapes prove the HRID
        // scan terminated at '<' rather than swallowing it.
        Assert.Contains(tokens[2].kind, new[] { Adl2TokenKind.LessThan, Adl2TokenKind.OdinBlock });
    }

    [Fact]
    public void Hrid_terminates_at_newline_then_resumes_with_identifier()
    {
        (Adl2TokenKind kind, string value)[] tokens =
            LexNoNewlines("archetype openEHR-EHR-CLUSTER.x.v1\nfoo");
        Assert.Equal(Adl2TokenKind.Keyword, tokens[0].kind);
        Assert.Equal(Adl2TokenKind.ArchetypeHridLiteral, tokens[1].kind);
        Assert.Equal("openEHR-EHR-CLUSTER.x.v1", tokens[1].value);
        Assert.Equal(Adl2TokenKind.Identifier, tokens[2].kind);
        Assert.Equal("foo", tokens[2].value);
    }
}
