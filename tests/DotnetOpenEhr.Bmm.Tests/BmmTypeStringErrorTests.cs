using DotnetOpenEhr.Bmm;
using Xunit;

namespace DotnetOpenEhr.Bmm.Tests;

/// <summary>
/// M15 — negative-path coverage for the BMM type-string parser. Drives
/// the parser through the public <see cref="BmmParser"/> surface using
/// a tiny inline model.
/// </summary>
public sealed class BmmTypeStringErrorTests
{
    private static BmmType TypeOf(string typeExpression)
    {
        string src = $$"""
            bmm_version = <"2.1">
            model_name = <"t">
            class_definitions = <
                ["X"] = <
                    properties = <
                        ["p"] = <
                            type = <"{{typeExpression}}">
                        >
                    >
                >
            >
            """;
        BmmModel model = BmmParser.Parse(src);
        return model.ClassDefinitions["X"].Properties["p"].Type;
    }

    [Fact]
    public void Unterminated_generic_throws_FormatException()
    {
        // The parser wraps invalid type strings in BmmParseException
        // (the parser's own exception type), which derives from
        // FormatException is NOT the contract — we just need the parser
        // to surface a parse exception rather than producing a partial
        // type.
        BmmParseException ex = Assert.Throws<BmmParseException>(() => TypeOf("List<DV_TEXT"));
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public void Trailing_content_after_generic_throws()
    {
        Assert.Throws<BmmParseException>(() => TypeOf("List<DV_TEXT>extra"));
    }

    [Fact]
    public void Missing_comma_between_type_args_throws()
    {
        Assert.Throws<BmmParseException>(() => TypeOf("Hash<String DV_TEXT>"));
    }

    [Fact]
    public void Whitespace_inside_generic_is_tolerated()
    {
        // Positive — confirms the parser tolerates whitespace around
        // generic delimiters.
        BmmContainerType t = Assert.IsType<BmmContainerType>(TypeOf("Hash< String , DV_TEXT >"));
        Assert.Equal(2, t.TypeArguments.Count);
        Assert.Equal("String", t.TypeArguments[0].TypeName);
        Assert.Equal("DV_TEXT", t.TypeArguments[1].TypeName);
    }

    [Fact]
    public void Qualified_name_parses_to_simple_type()
    {
        // Positive — a dotted qualified name is treated as a single
        // simple-type token (the parser doesn't attempt to split on '.').
        BmmSimpleType t = Assert.IsType<BmmSimpleType>(TypeOf("org.openehr.Foo"));
        Assert.Equal("org.openehr.Foo", t.TypeName);
    }
}
