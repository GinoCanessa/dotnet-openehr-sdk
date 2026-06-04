using DotnetOpenEhr.Aql;
using DotnetOpenEhr.Aql.Paths;
using Xunit;

namespace DotnetOpenEhr.Aql.Tests.Paths;

/// <summary>
/// Grammar pins for <see cref="ArchetypePathParser"/>. Every accepted
/// shape and every rejection has at least one named fact. The negative
/// cases double as cross-dialect drift guards — most notably the
/// SQL-style <c>''</c>-doubled-quote rejection.
/// </summary>
public class ArchetypePathParserTests
{
    [Fact]
    public void Parse_rejects_empty_path()
    {
        ArchetypePathParseException ex = Assert.Throws<ArchetypePathParseException>(
            () => ArchetypePathParser.Parse(""));
        Assert.Equal(1, ex.Position);
    }

    [Fact]
    public void Parse_slash_only_returns_empty_segments()
    {
        ArchetypePathSegment[] segments = ArchetypePathParser.Parse("/");
        Assert.Empty(segments);
    }

    [Fact]
    public void Parse_simple_attribute_chain()
    {
        ArchetypePathSegment[] segments = ArchetypePathParser.Parse("data/items/value");
        Assert.Equal(3, segments.Length);
        Assert.Equal("data", segments[0].AttributeName);
        Assert.Null(segments[0].Predicate);
        Assert.Equal("items", segments[1].AttributeName);
        Assert.Equal("value", segments[2].AttributeName);
    }

    [Fact]
    public void Parse_leading_slash_is_inert()
    {
        ArchetypePathSegment[] withSlash = ArchetypePathParser.Parse("/data/items");
        ArchetypePathSegment[] withoutSlash = ArchetypePathParser.Parse("data/items");
        Assert.Equal(withoutSlash.Length, withSlash.Length);
        for (int i = 0; i < withSlash.Length; i++)
        {
            Assert.Equal(withoutSlash[i].AttributeName, withSlash[i].AttributeName);
            Assert.Equal(withoutSlash[i].Predicate, withSlash[i].Predicate);
        }
    }

    [Fact]
    public void Parse_node_id_predicate()
    {
        ArchetypePathSegment[] segments = ArchetypePathParser.Parse("items[at0006]");
        Assert.Single(segments);
        Assert.Equal("items", segments[0].AttributeName);
        ArchetypePathPredicate predicate = segments[0].Predicate!;
        Assert.Equal("at0006", predicate.NodeId);
        Assert.Null(predicate.Name);
    }

    [Fact]
    public void Parse_archetype_hrid_predicate()
    {
        ArchetypePathSegment[] segments = ArchetypePathParser.Parse(
            "content[openEHR-EHR-OBSERVATION.blood_pressure.v2]");
        Assert.Single(segments);
        Assert.Equal("content", segments[0].AttributeName);
        Assert.Equal("openEHR-EHR-OBSERVATION.blood_pressure.v2", segments[0].Predicate!.NodeId);
        Assert.Null(segments[0].Predicate!.Name);
    }

    [Fact]
    public void Parse_name_only_predicate()
    {
        ArchetypePathSegment[] segments = ArchetypePathParser.Parse("items['Systolic']");
        Assert.Single(segments);
        Assert.Equal("items", segments[0].AttributeName);
        Assert.Null(segments[0].Predicate!.NodeId);
        Assert.Equal("Systolic", segments[0].Predicate!.Name);
    }

    [Fact]
    public void Parse_combined_node_id_and_name_predicate()
    {
        ArchetypePathSegment[] segments = ArchetypePathParser.Parse("items[at0006, 'Systolic']");
        Assert.Single(segments);
        Assert.Equal("at0006", segments[0].Predicate!.NodeId);
        Assert.Equal("Systolic", segments[0].Predicate!.Name);
    }

    [Fact]
    public void Parse_backslash_escaped_quote_in_name_predicate()
    {
        ArchetypePathSegment[] segments = ArchetypePathParser.Parse(@"items['It\'s']");
        Assert.Equal("It's", segments[0].Predicate!.Name);
    }

    [Fact]
    public void Parse_rejects_sql_style_doubled_quote_in_name_predicate()
    {
        // 'It''s' must be rejected — the second '' closes the literal and
        // leaves 's'] dangling, which is not a valid continuation.
        Assert.Throws<ArchetypePathParseException>(
            () => ArchetypePathParser.Parse("items['It''s']"));
    }

    [Fact]
    public void Parse_whitespace_inside_predicate_is_tolerated()
    {
        ArchetypePathSegment[] segments = ArchetypePathParser.Parse("items[ at0006 , 'Systolic' ]");
        Assert.Equal("at0006", segments[0].Predicate!.NodeId);
        Assert.Equal("Systolic", segments[0].Predicate!.Name);
    }

    [Fact]
    public void Parse_rejects_unterminated_predicate()
    {
        ArchetypePathParseException ex = Assert.Throws<ArchetypePathParseException>(
            () => ArchetypePathParser.Parse("items[at0006"));
        // Position is the 1-based location of the opening '['.
        Assert.Equal(6, ex.Position);
    }

    [Fact]
    public void Parse_rejects_double_slash()
    {
        Assert.Throws<ArchetypePathParseException>(
            () => ArchetypePathParser.Parse("data//items"));
    }

    [Fact]
    public void Parse_rejects_empty_segment_after_slash()
    {
        Assert.Throws<ArchetypePathParseException>(
            () => ArchetypePathParser.Parse("data/"));
    }

    [Fact]
    public void Parse_rejects_function_call_syntax()
    {
        Assert.Throws<ArchetypePathParseException>(
            () => ArchetypePathParser.Parse("foo()"));
    }

    [Fact]
    public void TryParse_returns_false_with_error_message_on_invalid_input()
    {
        bool ok = ArchetypePathParser.TryParse(
            "items[at0006",
            out ArchetypePathSegment[]? segments,
            out string? error);
        Assert.False(ok);
        Assert.Null(segments);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void TryParse_returns_true_with_valid_input()
    {
        bool ok = ArchetypePathParser.TryParse(
            "data/items[at0006]",
            out ArchetypePathSegment[]? segments,
            out string? error);
        Assert.True(ok);
        Assert.NotNull(segments);
        Assert.Equal(2, segments!.Length);
        Assert.Null(error);
    }

    // ---- M13: node id well-formedness --------------------------------

    [Fact]
    public void Parse_rejects_trailing_dot_in_node_id()
    {
        ArchetypePathParseException ex = Assert.Throws<ArchetypePathParseException>(
            () => ArchetypePathParser.Parse("data[at0006.]"));
        Assert.Contains("must not end with", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_rejects_double_dot_in_node_id()
    {
        ArchetypePathParseException ex = Assert.Throws<ArchetypePathParseException>(
            () => ArchetypePathParser.Parse("data[at..0006]"));
        Assert.Contains("'..'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_rejects_leading_dash_in_node_id()
    {
        ArchetypePathParseException ex = Assert.Throws<ArchetypePathParseException>(
            () => ArchetypePathParser.Parse("data[-at0006]"));
        Assert.Contains("must not start with", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_rejects_leading_dot_in_node_id()
    {
        ArchetypePathParseException ex = Assert.Throws<ArchetypePathParseException>(
            () => ArchetypePathParser.Parse("data[.at0006]"));
        Assert.Contains("must not start with", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_rejects_double_dash_in_node_id()
    {
        ArchetypePathParseException ex = Assert.Throws<ArchetypePathParseException>(
            () => ArchetypePathParser.Parse("data[at0006--xyz]"));
        Assert.Contains("'--'", ex.Message, StringComparison.Ordinal);
    }
}
