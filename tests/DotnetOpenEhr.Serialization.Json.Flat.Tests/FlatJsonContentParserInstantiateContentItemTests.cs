using System.Text.Json;
using DotnetOpenEhr.Templates.Abstractions;
using Xunit;

namespace DotnetOpenEhr.Serialization.Json.Flat.Tests;

/// <summary>
/// H6 — FlatJsonContentParser.InstantiateContentItem throws
/// JsonException when the schema resolves a content slot to an
/// unsupported RM type (notably INSTRUCTION/ACTION, which the FLAT
/// writer does not implement yet). Previously the unknown branch
/// silently downgraded to a Section, corrupting the shape.
/// </summary>
public sealed class FlatJsonContentParserInstantiateContentItemTests
{
    [Fact]
    public void UnknownRmType_ThrowsJsonException_NamingPathAndType()
    {
        // Schema deliberately resolves the content slot to INSTRUCTION
        // so InstantiateContentItem hits its unknown-branch throw.
        InstructionResolvingSchema schema = new("encounter");
        byte[] flat = System.Text.Encoding.UTF8.GetBytes(
            "{\"encounter/content:0/_archetype_node_id\": \"at0001\"}");

        JsonException ex = Assert.Throws<JsonException>(
            () => OpenEhrFlatJson.ParseComposition(flat, schema));

        Assert.Contains("INSTRUCTION", ex.Message, StringComparison.Ordinal);
        Assert.Contains("encounter/content", ex.Message, StringComparison.Ordinal);
    }

    private sealed class InstructionResolvingSchema : ITemplateSchema
    {
        public InstructionResolvingSchema(string templateId)
        {
            TemplateId = templateId;
        }

        public string TemplateId { get; }
        public IReadOnlyCollection<TemplateNode> Nodes { get; } = [];

        public bool TryResolveType(ReadOnlySpan<char> flatPath, out TemplateRmTypeResolution resolution)
        {
            resolution = new TemplateRmTypeResolution("INSTRUCTION", IsPolymorphic: true);
            return true;
        }
    }
}
