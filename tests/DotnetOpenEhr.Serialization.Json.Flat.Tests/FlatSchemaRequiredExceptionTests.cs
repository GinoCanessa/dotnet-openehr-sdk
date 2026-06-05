using Xunit;

namespace DotnetOpenEhr.Serialization.Json.Flat.Tests;

/// <summary>
/// M23 — pin <see cref="FlatSchemaRequiredException"/>'s message
/// format and property surface so a downstream diagnostic UI can
/// rely on the shape.
/// </summary>
public sealed class FlatSchemaRequiredExceptionTests
{
    [Fact]
    public void Message_NamesTemplate_FirstFiveUnresolved_AndOverflowSuffix()
    {
        string[] paths =
        [
            "tid/a/x|magnitude",
            "tid/a/y|value",
            "tid/b/z|code",
            "tid/c/q|terminology",
            "tid/d/r",
            "tid/e/s",
            "tid/f/t",
        ];

        FlatSchemaRequiredException ex = new("tid", paths);

        Assert.Contains("'tid'", ex.Message, StringComparison.Ordinal);
        // Lists exactly the first 5.
        for (int i = 0; i < 5; i++)
        {
            Assert.Contains(paths[i], ex.Message, StringComparison.Ordinal);
        }
        // Overflow suffix names the remaining count.
        Assert.Contains("(+2 more)", ex.Message, StringComparison.Ordinal);
        // The last two paths are NOT listed verbatim.
        Assert.DoesNotContain(paths[5], ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(paths[6], ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Message_NamesTemplate_FewerThanFiveUnresolved_NoSuffix()
    {
        string[] paths = ["tid/a|x", "tid/b|y"];

        FlatSchemaRequiredException ex = new("tid", paths);

        foreach (string p in paths)
        {
            Assert.Contains(p, ex.Message, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("more", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateId_And_UnresolvedPaths_AreSurfaced()
    {
        string[] paths = ["tid/foo"];
        FlatSchemaRequiredException ex = new("tid", paths);

        Assert.Equal("tid", ex.TemplateId);
        Assert.Equal(paths, ex.UnresolvedPaths);
    }
}
