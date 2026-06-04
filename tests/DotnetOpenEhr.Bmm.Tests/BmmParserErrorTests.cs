using DotnetOpenEhr.Bmm;
using Xunit;

namespace DotnetOpenEhr.Bmm.Tests;

/// <summary>
/// Negative tests: malformed BMM input must produce
/// <see cref="BmmParseException"/> with usable line/column diagnostics.
/// </summary>
public class BmmParserErrorTests
{
    [Fact]
    public void Missing_bmm_version_throws_with_path()
    {
        const string src = """
            model_name = <"x">
            """;
        BmmParseException ex = Assert.Throws<BmmParseException>(() => BmmParser.Parse(src));
        Assert.Contains("bmm_version", ex.Message);
        Assert.Contains("bmm_version", ex.Path ?? string.Empty);
    }

    /// <summary>
    /// B3 — every BmmParseException must report a real source line/column,
    /// not the legacy (0, 0) placeholder.
    /// </summary>
    [Fact]
    public void Missing_bmm_version_reports_line_and_column()
    {
        const string src = """
            model_name = <"x">
            """;
        BmmParseException ex = Assert.Throws<BmmParseException>(() => BmmParser.Parse(src));
        Assert.True(ex.Line >= 1, $"expected ex.Line >= 1, got {ex.Line}");
        Assert.True(ex.Column >= 1, $"expected ex.Column >= 1, got {ex.Column}");
    }

    /// <summary>
    /// B3 — the position should point at the offending property
    /// (the malformed value), not (0, 0).
    /// </summary>
    [Fact]
    public void Property_without_type_reports_position_of_offending_property()
    {
        const string src = """
            bmm_version = <"2.1">
            model_name = <"x">
            class_definitions = <
                ["FOO"] = <
                    properties = <
                        ["bar"] = <
                            name = <"bar">
                        >
                    >
                >
            >
            """;
        BmmParseException ex = Assert.Throws<BmmParseException>(() => BmmParser.Parse(src));
        Assert.True(ex.Line >= 1, $"expected ex.Line >= 1, got {ex.Line}");
        Assert.True(ex.Column >= 1, $"expected ex.Column >= 1, got {ex.Column}");
    }

    [Fact]
    public void Missing_model_name_throws()
    {
        const string src = """
            bmm_version = <"2.1">
            """;
        BmmParseException ex = Assert.Throws<BmmParseException>(() => BmmParser.Parse(src));
        Assert.Contains("model_name", ex.Message);
    }

    [Fact]
    public void Property_without_type_throws_with_dotted_path()
    {
        const string src = """
            bmm_version = <"2.1">
            model_name = <"x">
            class_definitions = <
                ["FOO"] = <
                    properties = <
                        ["bar"] = <
                            name = <"bar">
                        >
                    >
                >
            >
            """;
        BmmParseException ex = Assert.Throws<BmmParseException>(() => BmmParser.Parse(src));
        Assert.NotNull(ex.Path);
        Assert.Contains("class_definitions.FOO.properties.bar", ex.Path!);
        Assert.Contains("type", ex.Message);
    }

    [Fact]
    public void Underlying_odin_error_is_wrapped_with_position()
    {
        // Unclosed block - the ODIN parser will throw and we should wrap.
        const string src = """
            bmm_version = <"2.1
            """;
        BmmParseException ex = Assert.Throws<BmmParseException>(() => BmmParser.Parse(src));
        Assert.NotNull(ex.InnerException);
        Assert.True(ex.Line >= 1);
        Assert.True(ex.Column >= 1);
    }

    [Fact]
    public void Invalid_type_expression_reports_path()
    {
        const string src = """
            bmm_version = <"2.1">
            model_name = <"x">
            class_definitions = <
                ["FOO"] = <
                    properties = <
                        ["bar"] = <
                            type = <"List<,>">
                        >
                    >
                >
            >
            """;
        BmmParseException ex = Assert.Throws<BmmParseException>(() => BmmParser.Parse(src));
        Assert.NotNull(ex.Path);
        Assert.Contains("class_definitions.FOO.properties.bar.type", ex.Path!);
    }
}
