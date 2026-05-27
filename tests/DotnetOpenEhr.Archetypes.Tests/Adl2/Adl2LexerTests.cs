using System.Linq;
using DotnetOpenEhr.Archetypes.Adl2;
using DotnetOpenEhr.Odin;
using DotnetOpenEhr.Odin.Values;
using Xunit;

namespace DotnetOpenEhr.Archetypes.Tests.Adl2;

/// <summary>
/// Per-token positive and negative coverage of <see cref="Adl2Lexer"/>:
/// punctuation, keywords vs identifiers, the three terminology code
/// flavours, path segments with embedded predicates, regex disambiguation,
/// interval literals (including the <c>*</c> sentinel from Phase 7a),
/// archetype HRID context-switching, and the ODIN block hand-off
/// (including round-tripping the inner span through <see cref="OdinParser"/>).
/// </summary>
public class Adl2LexerTests
{
    private static Adl2TokenKind[] Lex(string source)
    {
        List<Adl2TokenKind> kinds = [];
        Adl2Lexer lexer = new(source.AsSpan());
        while (true)
        {
            Adl2Token tok = lexer.NextToken();
            kinds.Add(tok.Kind);
            if (tok.Kind == Adl2TokenKind.Eof)
            {
                break;
            }
        }
        return [.. kinds];
    }

    private static Adl2TokenKind[] LexNoNewlines(string source)
    {
        Adl2TokenKind[] all = Lex(source);
        return [.. all.Where(k => k != Adl2TokenKind.Newline)];
    }

    private static Adl2TokenKind[] LexKindsNoNewlines(string source)
        => LexNoNewlines(source);

    private record struct TokenInfo(Adl2TokenKind Kind, string Text, string? Embedded);

    private static TokenInfo[] LexAll(string source)
    {
        List<TokenInfo> all = [];
        Adl2Lexer lexer = new(source.AsSpan());
        while (true)
        {
            Adl2Token tok = lexer.NextToken();
            if (tok.Kind == Adl2TokenKind.Eof)
            {
                all.Add(new TokenInfo(tok.Kind, "", null));
                break;
            }
            all.Add(new TokenInfo(tok.Kind, tok.Text, tok.EmbeddedNodeId));
        }
        return [.. all];
    }

    private static Adl2Token First(string source)
    {
        Adl2Lexer lexer = new(source.AsSpan());
        return lexer.NextToken();
    }

    private static void DrainExpectingThrow(string source)
    {
        Assert.Throws<Adl2LexException>(() =>
        {
            Adl2Lexer lex = new(source.AsSpan());
            while (true)
            {
                Adl2Token t = lex.NextToken();
                if (t.Kind == Adl2TokenKind.Eof) return;
            }
        });
    }

    private static void FirstExpectingThrow(string source)
    {
        Assert.Throws<Adl2LexException>(() =>
        {
            Adl2Lexer lex = new(source.AsSpan());
            lex.NextToken();
        });
    }

    // ---- Eof / empty input -------------------------------------------------

    [Fact]
    public void Empty_source_produces_only_eof()
    {
        Adl2TokenKind[] expected = [Adl2TokenKind.Eof];
        Assert.Equal(expected, Lex(""));
    }

    [Fact]
    public void Whitespace_only_produces_only_eof()
    {
        Adl2TokenKind[] expected = [Adl2TokenKind.Eof];
        Assert.Equal(expected, Lex("   \t   "));
    }

    [Fact]
    public void Eof_is_idempotent()
    {
        Adl2Lexer lexer = new("".AsSpan());
        Assert.Equal(Adl2TokenKind.Eof, lexer.NextToken().Kind);
        Assert.Equal(Adl2TokenKind.Eof, lexer.NextToken().Kind);
    }

    // ---- Punctuation tokens ------------------------------------------------

    [Fact]
    public void LBrace_RBrace()
    {
        Adl2TokenKind[] expected = [Adl2TokenKind.LBrace, Adl2TokenKind.RBrace, Adl2TokenKind.Eof];
        Assert.Equal(expected, Lex("{}"));
    }

    [Fact]
    public void LBracket_RBracket_when_not_terminology_code()
    {
        Adl2TokenKind[] expected = [Adl2TokenKind.LBracket, Adl2TokenKind.RBracket, Adl2TokenKind.Eof];
        Assert.Equal(expected, Lex("[]"));
    }

    [Fact]
    public void LParen_RParen()
    {
        Adl2TokenKind[] expected = [Adl2TokenKind.LParen, Adl2TokenKind.RParen, Adl2TokenKind.Eof];
        Assert.Equal(expected, Lex("()"));
    }

    [Fact]
    public void Comma_Semicolon_Colon()
    {
        Adl2TokenKind[] expected =
            [Adl2TokenKind.Comma, Adl2TokenKind.Semicolon, Adl2TokenKind.Colon, Adl2TokenKind.Eof];
        Assert.Equal(expected, Lex(", ; :"));
    }

    [Fact]
    public void Equals_token()
    {
        Assert.Equal(Adl2TokenKind.Equals, First("=").Kind);
    }

    [Fact]
    public void Equals_equals_collapses_to_single_equals()
    {
        Assert.Equal(Adl2TokenKind.Equals, First("==").Kind);
        Assert.Equal(2, First("==").Length);
    }

    [Fact]
    public void Less_than_outside_odin_section()
    {
        Assert.Equal(Adl2TokenKind.LessThan, First("<").Kind);
    }

    [Fact]
    public void Greater_than_token()
    {
        Assert.Equal(Adl2TokenKind.GreaterThan, First(">").Kind);
    }

    [Fact]
    public void Less_equal_token()
    {
        Adl2Token t = First("<=");
        Assert.Equal(Adl2TokenKind.LessEqual, t.Kind);
        Assert.Equal(2, t.Length);
    }

    [Fact]
    public void Greater_equal_token()
    {
        Adl2Token t = First(">=");
        Assert.Equal(Adl2TokenKind.GreaterEqual, t.Kind);
        Assert.Equal(2, t.Length);
    }

    [Fact]
    public void Not_equal_token()
    {
        Adl2Token t = First("!=");
        Assert.Equal(Adl2TokenKind.NotEqual, t.Kind);
    }

    [Fact]
    public void Bare_bang_is_rejected()
    {
        FirstExpectingThrow("!x");
    }

    [Fact]
    public void Plus_token()
    {
        Assert.Equal(Adl2TokenKind.Plus, First("+").Kind);
    }

    [Fact]
    public void Minus_token_when_no_digit_follows()
    {
        Assert.Equal(Adl2TokenKind.Minus, First("-x").Kind);
    }

    [Fact]
    public void Star_token()
    {
        Assert.Equal(Adl2TokenKind.Star, First("*").Kind);
    }

    [Fact]
    public void Range_token()
    {
        Adl2Token t = First("..");
        Assert.Equal(Adl2TokenKind.Range, t.Kind);
        Assert.Equal(2, t.Length);
    }

    [Fact]
    public void Bare_single_dot_is_rejected()
    {
        FirstExpectingThrow(". ");
    }

    [Fact]
    public void Interval_delim_token()
    {
        Assert.Equal(Adl2TokenKind.IntervalDelim, First("|").Kind);
    }

    [Fact]
    public void Unknown_character_is_rejected()
    {
        FirstExpectingThrow("`");
    }

    // ---- Identifiers / keywords --------------------------------------------

    [Fact]
    public void Identifier_simple()
    {
        Adl2Token t = First("magnitude");
        Assert.Equal(Adl2TokenKind.Identifier, t.Kind);
        Assert.Equal("magnitude", t.Text);
    }

    [Fact]
    public void Identifier_with_underscores_and_digits()
    {
        Adl2Token t = First("DV_QUANTITY42");
        Assert.Equal(Adl2TokenKind.Identifier, t.Kind);
        Assert.Equal("DV_QUANTITY42", t.Text);
    }

    [Fact]
    public void Identifier_cannot_start_with_digit()
    {
        Adl2TokenKind[] expected = [Adl2TokenKind.IntegerLiteral, Adl2TokenKind.Identifier, Adl2TokenKind.Eof];
        Assert.Equal(expected, LexNoNewlines("1foo"));
    }

    [Theory]
    [InlineData("archetype")]
    [InlineData("template")]
    [InlineData("template_overlay")]
    [InlineData("operational_template")]
    [InlineData("differential")]
    [InlineData("specialize")]
    [InlineData("language")]
    [InlineData("description")]
    [InlineData("definition")]
    [InlineData("rules")]
    [InlineData("terminology")]
    [InlineData("annotations")]
    [InlineData("concept")]
    [InlineData("existence")]
    [InlineData("cardinality")]
    [InlineData("occurrences")]
    [InlineData("matches")]
    [InlineData("ordered")]
    [InlineData("unordered")]
    [InlineData("unique")]
    [InlineData("use_node")]
    [InlineData("use_archetype")]
    [InlineData("allow_archetype")]
    [InlineData("include")]
    [InlineData("exclude")]
    [InlineData("before")]
    [InlineData("after")]
    [InlineData("then")]
    [InlineData("assert")]
    [InlineData("for_each")]
    [InlineData("in")]
    [InlineData("where")]
    public void Keyword_recognised(string kw)
    {
        Adl2Token t = First(kw);
        Assert.Equal(Adl2TokenKind.Keyword, t.Kind);
        Assert.Equal(kw, t.Value);
    }

    [Fact]
    public void Keywords_are_case_sensitive()
    {
        Assert.Equal(Adl2TokenKind.Identifier, First("Archetype").Kind);
    }

    // ---- Numbers -----------------------------------------------------------

    [Fact]
    public void Integer_literal()
    {
        Adl2Token t = First("42");
        Assert.Equal(Adl2TokenKind.IntegerLiteral, t.Kind);
        Assert.Equal("42", t.Text);
    }

    [Fact]
    public void Negative_integer_in_operand_position()
    {
        Adl2TokenKind[] expected =
            [Adl2TokenKind.LParen, Adl2TokenKind.IntegerLiteral, Adl2TokenKind.RParen, Adl2TokenKind.Eof];
        Assert.Equal(expected, LexNoNewlines("(-7)"));
    }

    [Fact]
    public void Real_literal_dot_form()
    {
        Adl2Token t = First("3.14");
        Assert.Equal(Adl2TokenKind.RealLiteral, t.Kind);
        Assert.Equal("3.14", t.Text);
    }

    [Fact]
    public void Real_literal_exponent_form()
    {
        Adl2Token t = First("6.022e23");
        Assert.Equal(Adl2TokenKind.RealLiteral, t.Kind);
    }

    [Fact]
    public void Range_takes_precedence_over_decimal_point()
    {
        Adl2TokenKind[] expected =
            [Adl2TokenKind.IntegerLiteral, Adl2TokenKind.Range, Adl2TokenKind.IntegerLiteral, Adl2TokenKind.Eof];
        Assert.Equal(expected, LexNoNewlines("0..1"));
    }

    [Fact]
    public void Real_then_range_in_interval()
    {
        Adl2TokenKind[] expected =
        [
            Adl2TokenKind.IntervalDelim, Adl2TokenKind.GreaterEqual,
            Adl2TokenKind.RealLiteral, Adl2TokenKind.Range, Adl2TokenKind.RealLiteral,
            Adl2TokenKind.IntervalDelim, Adl2TokenKind.Eof,
        ];
        Assert.Equal(expected, LexNoNewlines("|>=18.5..30.0|"));
    }

    [Fact]
    public void Exponent_requires_digits()
    {
        FirstExpectingThrow("1e");
    }

    // ---- Strings -----------------------------------------------------------

    [Fact]
    public void String_literal_simple()
    {
        Adl2Token t = First("\"hello\"");
        Assert.Equal(Adl2TokenKind.StringLiteral, t.Kind);
        Assert.Equal("hello", t.Value);
    }

    [Fact]
    public void String_literal_with_escapes()
    {
        Adl2Token t = First("\"a\\nb\\tc\\\"d\"");
        Assert.Equal("a\nb\tc\"d", t.Value);
    }

    [Fact]
    public void String_literal_unterminated_throws()
    {
        FirstExpectingThrow("\"oops");
    }

    [Fact]
    public void String_literal_invalid_escape_throws()
    {
        FirstExpectingThrow("\"\\q\"");
    }

    [Fact]
    public void Comment_marker_inside_string_is_data_not_comment()
    {
        Adl2Token t = First("\"-- not a comment\"");
        Assert.Equal(Adl2TokenKind.StringLiteral, t.Kind);
        Assert.Equal("-- not a comment", t.Value);
    }

    // ---- Regex literals ----------------------------------------------------

    [Fact]
    public void Regex_literal_simple()
    {
        Adl2Token t = First("/[A-Z]+/");
        Assert.Equal(Adl2TokenKind.RegexLiteral, t.Kind);
        Assert.Equal("[A-Z]+", t.Value);
    }

    [Fact]
    public void Regex_literal_with_escaped_slash()
    {
        // Use '[' as the first char so the lexer dispatches to regex
        // rather than path segment scanning.
        Adl2Token t = First("/[a]\\/b/");
        Assert.Equal(Adl2TokenKind.RegexLiteral, t.Kind);
        Assert.Equal("[a]/b", t.Value);
    }

    [Fact]
    public void Regex_literal_unterminated_throws()
    {
        // '[' steers the lexer away from path-segment scanning.
        FirstExpectingThrow("/[oops");
    }

    [Fact]
    public void Regex_literal_cannot_span_lines()
    {
        FirstExpectingThrow("/[abc\ndef/");
    }

    [Fact]
    public void Comment_marker_inside_regex_is_data_not_comment()
    {
        Adl2Token t = First("/-- still regex/");
        Assert.Equal(Adl2TokenKind.RegexLiteral, t.Kind);
    }

    [Fact]
    public void Slash_in_value_position_is_division_not_regex()
    {
        Adl2TokenKind[] expected =
            [Adl2TokenKind.IntegerLiteral, Adl2TokenKind.Slash, Adl2TokenKind.IntegerLiteral, Adl2TokenKind.Eof];
        Assert.Equal(expected, LexNoNewlines("6 / 2"));
    }

    // ---- Terminology codes -------------------------------------------------

    [Fact]
    public void At_code_token()
    {
        Adl2Token t = First("[at0001]");
        Assert.Equal(Adl2TokenKind.AtCode, t.Kind);
        Assert.Equal("at0001", t.Value);
    }

    [Fact]
    public void Ac_code_token()
    {
        Adl2Token t = First("[ac0001]");
        Assert.Equal(Adl2TokenKind.AcCode, t.Kind);
        Assert.Equal("ac0001", t.Value);
    }

    [Fact]
    public void Id_code_token()
    {
        Adl2Token t = First("[id0001]");
        Assert.Equal(Adl2TokenKind.IdCode, t.Kind);
        Assert.Equal("id0001", t.Value);
    }

    [Fact]
    public void At_id_ac_are_disambiguated()
    {
        Assert.Equal(Adl2TokenKind.AtCode, First("[at1]").Kind);
        Assert.Equal(Adl2TokenKind.AcCode, First("[ac1]").Kind);
        Assert.Equal(Adl2TokenKind.IdCode, First("[id1]").Kind);
    }

    [Fact]
    public void Hierarchical_at_code()
    {
        Adl2Token t = First("[at0001.1.2]");
        Assert.Equal(Adl2TokenKind.AtCode, t.Kind);
        Assert.Equal("at0001.1.2", t.Value);
    }

    [Fact]
    public void Bracket_not_terminology_code_is_lbracket()
    {
        Adl2TokenKind[] expected =
            [Adl2TokenKind.LBracket, Adl2TokenKind.Identifier, Adl2TokenKind.RBracket, Adl2TokenKind.Eof];
        Assert.Equal(expected, LexNoNewlines("[foo]"));
    }

    [Fact]
    public void Unterminated_terminology_code_lexes_as_lbracket()
    {
        Adl2TokenKind[] expected = [Adl2TokenKind.LBracket, Adl2TokenKind.Identifier, Adl2TokenKind.Eof];
        Assert.Equal(expected, LexNoNewlines("[at0001"));
    }

    // ---- Path segments -----------------------------------------------------

    [Fact]
    public void Path_segment_without_predicate()
    {
        Adl2Token t = First("/value");
        Assert.Equal(Adl2TokenKind.PathSegment, t.Kind);
        Assert.Equal("value", t.Value);
        Assert.Null(t.EmbeddedNodeId);
    }

    [Fact]
    public void Path_segment_with_id_predicate()
    {
        Adl2Token t = First("/data[id3]");
        Assert.Equal(Adl2TokenKind.PathSegment, t.Kind);
        Assert.Equal("data", t.Value);
        Assert.Equal("id3", t.EmbeddedNodeId);
    }

    [Fact]
    public void Compound_path_three_segments()
    {
        TokenInfo[] all = LexAll("/data[id3]/items[id4]/value");
        TokenInfo[] segs = [.. all.Where(x => x.Kind == Adl2TokenKind.PathSegment)];
        Assert.Equal(3, segs.Length);
        Assert.Equal(("data", "id3"), (segs[0].Text, segs[0].Embedded));
        Assert.Equal(("items", "id4"), (segs[1].Text, segs[1].Embedded));
        Assert.Equal(("value", null), (segs[2].Text, segs[2].Embedded));
    }

    [Fact]
    public void Bare_slash_is_slash_when_in_value_position()
    {
        Adl2TokenKind[] expected =
            [Adl2TokenKind.Identifier, Adl2TokenKind.Slash, Adl2TokenKind.Identifier, Adl2TokenKind.Eof];
        Assert.Equal(expected, LexNoNewlines("a / b"));
    }

    // ---- Archetype HRID literal -------------------------------------------

    [Fact]
    public void Archetype_keyword_then_hrid_literal()
    {
        TokenInfo[] tokens = LexAll("archetype openEHR-EHR-OBSERVATION.blood_pressure.v2.0.0");
        Assert.Equal(Adl2TokenKind.Keyword, tokens[0].Kind);
        Assert.Equal("archetype", tokens[0].Text);
        Adl2TokenKind hridKind = tokens.Where(t => t.Kind != Adl2TokenKind.Newline).Skip(1).First().Kind;
        Assert.Equal(Adl2TokenKind.ArchetypeHridLiteral, hridKind);
    }

    [Fact]
    public void Template_keyword_then_hrid_literal()
    {
        TokenInfo[] tokens = LexAll("template openEHR-EHR-COMPOSITION.encounter.v1.0.0");
        Assert.Equal("template", tokens[0].Text);
        Assert.Equal(Adl2TokenKind.ArchetypeHridLiteral, tokens.Skip(1).First(t => t.Kind != Adl2TokenKind.Newline).Kind);
    }

    [Fact]
    public void Specialize_keyword_then_hrid_literal()
    {
        TokenInfo[] tokens = LexAll("specialize openEHR-EHR-CLUSTER.parent.v1");
        Assert.Equal(Adl2TokenKind.ArchetypeHridLiteral, tokens.Skip(1).First(t => t.Kind != Adl2TokenKind.Newline).Kind);
    }

    [Fact]
    public void Identifier_after_non_archetype_keyword_is_not_hrid()
    {
        TokenInfo[] tokens = LexAll("matches CLUSTER");
        Assert.Equal(Adl2TokenKind.Identifier, tokens.Skip(1).First(t => t.Kind != Adl2TokenKind.Newline).Kind);
    }

    [Fact]
    public void Archetype_hrid_only_emitted_once()
    {
        TokenInfo[] tokens = LexAll("archetype openEHR-EHR-CLUSTER.x.v1\nfoo");
        Adl2TokenKind[] nonWs = [.. tokens.Where(t => t.Kind != Adl2TokenKind.Newline).Select(t => t.Kind)];
        Assert.Equal(Adl2TokenKind.Keyword, nonWs[0]);
        Assert.Equal(Adl2TokenKind.ArchetypeHridLiteral, nonWs[1]);
        Assert.Equal(Adl2TokenKind.Identifier, nonWs[2]);
    }

    // ---- Intervals (depends on Phase 7a Star support) ----------------------

    [Fact]
    public void Interval_zero_to_one()
    {
        Adl2TokenKind[] expected =
        [
            Adl2TokenKind.IntervalDelim, Adl2TokenKind.IntegerLiteral,
            Adl2TokenKind.Range, Adl2TokenKind.IntegerLiteral,
            Adl2TokenKind.IntervalDelim, Adl2TokenKind.Eof,
        ];
        Assert.Equal(expected, LexNoNewlines("|0..1|"));
    }

    [Fact]
    public void Interval_zero_to_star()
    {
        Adl2TokenKind[] expected =
        [
            Adl2TokenKind.IntervalDelim, Adl2TokenKind.IntegerLiteral,
            Adl2TokenKind.Range, Adl2TokenKind.Star,
            Adl2TokenKind.IntervalDelim, Adl2TokenKind.Eof,
        ];
        Assert.Equal(expected, LexNoNewlines("|0..*|"));
    }

    [Fact]
    public void Interval_geq_zero_to_star()
    {
        Adl2TokenKind[] expected =
        [
            Adl2TokenKind.IntervalDelim, Adl2TokenKind.GreaterEqual,
            Adl2TokenKind.IntegerLiteral, Adl2TokenKind.Range,
            Adl2TokenKind.Star, Adl2TokenKind.IntervalDelim,
            Adl2TokenKind.Eof,
        ];
        Assert.Equal(expected, LexNoNewlines("|>=0..*|"));
    }

    [Fact]
    public void Interval_star_to_five()
    {
        Adl2TokenKind[] expected =
        [
            Adl2TokenKind.IntervalDelim, Adl2TokenKind.Star,
            Adl2TokenKind.Range, Adl2TokenKind.IntegerLiteral,
            Adl2TokenKind.IntervalDelim, Adl2TokenKind.Eof,
        ];
        Assert.Equal(expected, LexNoNewlines("|*..5|"));
    }

    [Fact]
    public void Interval_real_bounds()
    {
        Adl2TokenKind[] kinds = LexNoNewlines("|>=18.5..30.0|");
        Assert.Contains(Adl2TokenKind.RealLiteral, kinds);
        Assert.Contains(Adl2TokenKind.GreaterEqual, kinds);
    }

    // ---- Comments / newlines -----------------------------------------------

    [Fact]
    public void Line_comment_skipped()
    {
        Adl2TokenKind[] expected = [Adl2TokenKind.Newline, Adl2TokenKind.Identifier, Adl2TokenKind.Eof];
        Assert.Equal(expected, Lex("-- skip me\nfoo"));
    }

    [Fact]
    public void Newline_emitted_as_token()
    {
        Adl2TokenKind[] expected =
            [Adl2TokenKind.Identifier, Adl2TokenKind.Newline, Adl2TokenKind.Identifier, Adl2TokenKind.Eof];
        Assert.Equal(expected, Lex("a\nb"));
    }

    [Fact]
    public void Crlf_newlines_collapse_into_one_token()
    {
        Adl2TokenKind[] expected =
            [Adl2TokenKind.Identifier, Adl2TokenKind.Newline, Adl2TokenKind.Identifier, Adl2TokenKind.Eof];
        Assert.Equal(expected, Lex("a\r\n\r\nb"));
    }

    // ---- Line / column tracking --------------------------------------------

    [Fact]
    public void Line_column_tracking()
    {
        Adl2Lexer lex = new("foo\n  bar".AsSpan());
        Adl2Token foo = lex.NextToken();
        Adl2Token nl = lex.NextToken();
        Adl2Token bar = lex.NextToken();
        Assert.Equal((1, 1), (foo.Line, foo.Column));
        Assert.Equal(Adl2TokenKind.Newline, nl.Kind);
        Assert.Equal((2, 3), (bar.Line, bar.Column));
        Assert.Equal("bar", bar.Text);
    }

    [Fact]
    public void Error_carries_line_column()
    {
        Adl2LexException ex = Assert.Throws<Adl2LexException>(() =>
        {
            Adl2Lexer lex = new("foo\n  `".AsSpan());
            lex.NextToken();
            lex.NextToken();
            lex.NextToken();
        });
        Assert.Equal(2, ex.Line);
        Assert.Equal(3, ex.Column);
    }

    // ---- ODIN block hand-off ----------------------------------------------

    [Fact]
    public void Language_section_emits_odin_block()
    {
        string src = "language\n    original_language = <[ISO_639-1::en]>\n";
        Adl2TokenKind[] all = Lex(src);
        Adl2TokenKind[] kinds = [.. all.Where(k => k != Adl2TokenKind.Newline)];
        Adl2TokenKind[] expected =
        [
            Adl2TokenKind.Keyword, Adl2TokenKind.Identifier, Adl2TokenKind.Equals,
            Adl2TokenKind.OdinBlock, Adl2TokenKind.Eof,
        ];
        Assert.Equal(expected, kinds);
    }

    [Fact]
    public void Description_section_emits_odin_block_with_nested_braces()
    {
        string src = "description\n    original_author = <\n        [\"name\"] = <\"Alice\">\n    >\n";
        string? captured = null;
        int blocks = 0;
        Adl2Lexer lex = new(src.AsSpan());
        while (true)
        {
            Adl2Token t = lex.NextToken();
            if (t.Kind == Adl2TokenKind.Eof) break;
            if (t.Kind == Adl2TokenKind.OdinBlock)
            {
                blocks++;
                captured = t.Value;
            }
        }
        Assert.Equal(1, blocks);
        Assert.NotNull(captured);
        Assert.Contains("\"Alice\"", captured);
    }

    [Fact]
    public void Definition_section_does_not_trigger_odin_handoff()
    {
        string src = "definition\n    CLUSTER[id1] matches {0..1}\n";
        Adl2TokenKind[] all = Lex(src);
        Adl2TokenKind[] kinds = [.. all.Where(k => k != Adl2TokenKind.Newline)];
        Assert.DoesNotContain(Adl2TokenKind.OdinBlock, kinds);
        Assert.Contains(Adl2TokenKind.IdCode, kinds);
        Assert.Contains(Adl2TokenKind.LBrace, kinds);
        Assert.Contains(Adl2TokenKind.RBrace, kinds);
    }

    [Fact]
    public void Odin_block_ignores_braces_inside_string_literal()
    {
        string src = "annotations\n    items = <\"abc>def\">\n";
        string? capturedValue = null;
        Adl2Lexer lex = new(src.AsSpan());
        while (true)
        {
            Adl2Token t = lex.NextToken();
            if (t.Kind == Adl2TokenKind.Eof) break;
            if (t.Kind == Adl2TokenKind.OdinBlock)
            {
                capturedValue = t.Value;
                break;
            }
        }
        Assert.NotNull(capturedValue);
        Assert.Equal("\"abc>def\"", capturedValue);
    }

    [Fact]
    public void Section_keyword_after_odin_section_clears_flag()
    {
        string src = "language\n    a = <\"x\">\ndefinition\n    CLUSTER[id1] matches {0..1}\n";
        Adl2Lexer lex = new(src.AsSpan());
        int blockCount = 0;
        int lessThanCount = 0;
        while (true)
        {
            Adl2Token t = lex.NextToken();
            if (t.Kind == Adl2TokenKind.Eof) break;
            if (t.Kind == Adl2TokenKind.OdinBlock) blockCount++;
            if (t.Kind == Adl2TokenKind.LessThan) lessThanCount++;
        }
        Assert.Equal(1, blockCount);
        Assert.Equal(0, lessThanCount);
    }

    [Fact]
    public void Multiple_odin_blocks_in_one_section()
    {
        string src = "language\n    a = <\"x\">\n    b = <\"y\">\n";
        List<string> blocks = [];
        Adl2Lexer lex = new(src.AsSpan());
        while (true)
        {
            Adl2Token t = lex.NextToken();
            if (t.Kind == Adl2TokenKind.Eof) break;
            if (t.Kind == Adl2TokenKind.OdinBlock) blocks.Add(t.Value ?? "");
        }
        Assert.Equal(2, blocks.Count);
        Assert.Contains("\"x\"", blocks[0]);
        Assert.Contains("\"y\"", blocks[1]);
    }

    [Fact]
    public void Odin_block_inner_span_reparses_via_odin_parser()
    {
        string src = "language\n    translations = <\n        [\"sl\"] = <\n            language = <\"sl\">\n            author = <[\"name\"] = <\"A\">>\n        >\n    >\n";
        string? outer = null;
        Adl2Lexer lex = new(src.AsSpan());
        while (true)
        {
            Adl2Token t = lex.NextToken();
            if (t.Kind == Adl2TokenKind.Eof) break;
            if (t.Kind == Adl2TokenKind.OdinBlock)
            {
                outer = t.Text;
                break;
            }
        }
        Assert.NotNull(outer);
        OdinValue v = OdinParser.Parse(outer);
        Assert.NotNull(v);
    }

    [Fact]
    public void Large_odin_block_round_trips()
    {
        System.Text.StringBuilder sb = new();
        sb.AppendLine("description");
        sb.AppendLine("    original_author = <");
        for (int i = 0; i < 200; i++)
        {
            sb.AppendLine($"        [\"k{i}\"] = <\"value-{i}\">");
        }
        sb.AppendLine("    >");
        string src = sb.ToString();

        string? blockText = null;
        int blocks = 0;
        Adl2Lexer lex = new(src.AsSpan());
        while (true)
        {
            Adl2Token t = lex.NextToken();
            if (t.Kind == Adl2TokenKind.Eof) break;
            if (t.Kind == Adl2TokenKind.OdinBlock)
            {
                blockText = t.Text;
                blocks++;
            }
        }
        Assert.Equal(1, blocks);
        Assert.NotNull(blockText);
        OdinValue v = OdinParser.Parse(blockText);
        Assert.NotNull(v);
    }

    [Fact]
    public void Unterminated_odin_block_throws()
    {
        DrainExpectingThrow("language\n    a = <unbalanced\n");
    }

    // ---- End-to-end sanity smoke ------------------------------------------

    [Fact]
    public void End_to_end_header_smoke()
    {
        // NOTE: ADL2 archetype metadata blocks like
        // '(adl_version=2.0.6; rm_release=1.1.0)' contain multi-dot
        // version literals that the lexer does not recognise as a
        // single number; the parser is responsible for re-tokenising
        // metadata. The lexer's contract here is: header keyword,
        // then ArchetypeHridLiteral on the next non-trivia token.
        string src = "archetype openEHR-EHR-CLUSTER.blood_pressure.v1.0.0\n";
        Adl2TokenKind[] all = Lex(src);
        Adl2TokenKind[] kinds = [.. all.Where(k => k != Adl2TokenKind.Newline)];
        Assert.Equal(Adl2TokenKind.Keyword, kinds[0]);
        Assert.Equal(Adl2TokenKind.ArchetypeHridLiteral, kinds[1]);
    }

    [Fact]
    public void End_to_end_definition_with_path_and_interval()
    {
        string src = "definition\n    CLUSTER[id1] matches {\n        items cardinality matches {|1..*|}\n    }\n";
        Adl2TokenKind[] all = Lex(src);
        Adl2TokenKind[] kinds = [.. all.Where(k => k != Adl2TokenKind.Newline)];
        Assert.Contains(Adl2TokenKind.IdCode, kinds);
        Assert.Contains(Adl2TokenKind.IntervalDelim, kinds);
        Assert.Contains(Adl2TokenKind.Star, kinds);
    }

    [Fact]
    public void End_to_end_rule_expression_with_path()
    {
        string src = "rules\n    /data[id1]/items[id2]/value/magnitude > 100\n";
        TokenInfo[] tokens = LexAll(src);
        Assert.Contains(tokens, t => t.Kind == Adl2TokenKind.Keyword && t.Text == "rules");
        Assert.Equal(4, tokens.Count(t => t.Kind == Adl2TokenKind.PathSegment));
        Assert.Contains(tokens, t => t.Kind == Adl2TokenKind.GreaterThan);
        Assert.Contains(tokens, t => t.Kind == Adl2TokenKind.IntegerLiteral && t.Text == "100");
    }
}
