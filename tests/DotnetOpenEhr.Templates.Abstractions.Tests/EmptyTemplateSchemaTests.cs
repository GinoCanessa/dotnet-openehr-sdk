using DotnetOpenEhr.Templates.Abstractions;
using Xunit;

namespace DotnetOpenEhr.Templates.Abstractions.Tests;

/// <summary>
/// Behaviour tests for <see cref="EmptyTemplateSchema"/> and the
/// supporting DTOs.
/// </summary>
public sealed class EmptyTemplateSchemaTests
{
    [Fact]
    public void Constructor_StoresTemplateId()
    {
        EmptyTemplateSchema schema = new("vitals");
        Assert.Equal("vitals", schema.TemplateId);
    }

    [Fact]
    public void Constructor_RejectsNullTemplateId()
    {
        Assert.Throws<ArgumentNullException>(() => new EmptyTemplateSchema(null!));
    }

    [Fact]
    public void Nodes_IsEmpty()
    {
        EmptyTemplateSchema schema = new("vitals");
        Assert.Empty(schema.Nodes);
    }

    [Fact]
    public void TryResolveType_AlwaysReturnsFalse()
    {
        EmptyTemplateSchema schema = new("vitals");
        bool ok = schema.TryResolveType(
            "vitals/category|code".AsSpan(),
            out TemplateRmTypeResolution resolution);
        Assert.False(ok);
        Assert.Equal(default, resolution);
    }

    [Fact]
    public void TemplateOccurrence_HasUnboundedSentinel()
    {
        Assert.Equal(int.MaxValue, TemplateOccurrence.Unbounded);
        TemplateOccurrence occ = new(0, TemplateOccurrence.Unbounded);
        Assert.Equal(0, occ.Min);
        Assert.Equal(int.MaxValue, occ.Max);
    }

    [Fact]
    public void TemplateNode_RecordEqualityIsValueBased()
    {
        TemplateNode a = new("/x", "tpl/x", "DV_TEXT", new TemplateOccurrence(0, 1));
        TemplateNode b = new("/x", "tpl/x", "DV_TEXT", new TemplateOccurrence(0, 1));
        TemplateNode c = new("/x", "tpl/x", "DV_TEXT", new TemplateOccurrence(1, 1));
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void TemplateRmTypeResolution_RecordEqualityIsValueBased()
    {
        TemplateRmTypeResolution a = new("DV_QUANTITY", true);
        TemplateRmTypeResolution b = new("DV_QUANTITY", true);
        TemplateRmTypeResolution c = new("DV_QUANTITY", false);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
