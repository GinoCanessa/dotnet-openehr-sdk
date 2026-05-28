using System.Text;
using System.Text.Json;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Support;
using DotnetOpenEhr.Templates.Abstractions;
using Xunit;

namespace DotnetOpenEhr.Serialization.Json.Flat.Tests;

public sealed class FlatSchemaDrivenSafetyTests
{
    [Fact]
    public void SchemaDrivenSerialize_UnsupportedContentItem_ThrowsWithPathAndRmType()
    {
        EmptyTemplateSchema schema = new("safety");
        Composition composition = CreateComposition(
            new Instruction
            {
                Name = new DvText("Instruction"),
                ArchetypeNodeId = "openEHR-EHR-INSTRUCTION.test.v1",
                Narrative = new DvText("take once daily"),
            });
        using MemoryStream output = new();

        NotSupportedException ex = Assert.Throws<NotSupportedException>(
            () => FlatJsonWriter.Write(output, composition, schema));

        Assert.Contains("safety/content:0", ex.Message, StringComparison.Ordinal);
        Assert.Contains("INSTRUCTION", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void SchemaDrivenSerialize_UnsupportedDataValue_ThrowsWithPathAndRmType()
    {
        EmptyTemplateSchema schema = new("safety");
        Composition composition = CreateComposition(
            new Evaluation
            {
                Name = new DvText("Evaluation"),
                ArchetypeNodeId = "openEHR-EHR-EVALUATION.test.v1",
                Data = new ItemTree
                {
                    Name = new DvText("Tree"),
                    ArchetypeNodeId = "at0001",
                    Items =
                    [
                        new Element
                        {
                            Name = new DvText("Ratio"),
                            ArchetypeNodeId = "at0002",
                            Value = new DvProportion
                            {
                                Numerator = 1,
                                Denominator = 2,
                                Type = 2,
                            },
                        },
                    ],
                },
            });

        NotSupportedException ex = Assert.Throws<NotSupportedException>(
            () => OpenEhrFlatJson.Serialize(composition, schema));

        Assert.Contains("safety/content:0/data/items:0/value", ex.Message, StringComparison.Ordinal);
        Assert.Contains("DV_PROPORTION", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SchemaDrivenSerialize_UnsupportedMetadata_ThrowsWithPath()
    {
        EmptyTemplateSchema schema = new("safety");
        Composition composition = CreateComposition(
            new Evaluation
            {
                Name = new DvText("Evaluation"),
                ArchetypeNodeId = "openEHR-EHR-EVALUATION.test.v1",
                Provider = new PartyIdentified { Name = "Dr Test" },
                Data = new ItemTree(),
            });

        NotSupportedException ex = Assert.Throws<NotSupportedException>(
            () => OpenEhrFlatJson.Serialize(composition, schema));

        Assert.Contains("safety/content:0/provider", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Entry.Provider", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseComposition_RejectsNegativeRepeatIndex_WithJsonException()
    {
        JsonException ex = Assert.Throws<JsonException>(
            () => ParseWithEmptySchema("""{"safety/content:-1/name|value":"bad"}"""));

        Assert.Contains("safety/content:-1/name|value", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseComposition_RejectsHugeRepeatIndex_WithJsonException()
    {
        JsonException ex = Assert.Throws<JsonException>(
            () => ParseWithEmptySchema("""{"safety/content:4097/name|value":"bad"}"""));

        Assert.Contains("safety/content:4097/name|value", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseComposition_RejectsSparseRepeatIndex_WithJsonException()
    {
        JsonException ex = Assert.Throws<JsonException>(
            () => ParseWithEmptySchema("""{"safety/content:129/name|value":"bad"}"""));

        Assert.Contains("safety/content:129/name|value", ex.Message, StringComparison.Ordinal);
    }

    private static Composition CreateComposition(ContentItem contentItem)
        => new()
        {
            Name = new DvText("Safety composition"),
            ArchetypeNodeId = "openEHR-EHR-COMPOSITION.safety.v1",
            Uid = new HierObjectId { Value = "safety-composition" },
            Content = [contentItem],
        };

    private static void ParseWithEmptySchema(string json)
    {
        EmptyTemplateSchema schema = new("safety");
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        OpenEhrFlatJson.ParseComposition(bytes, schema);
    }
}
