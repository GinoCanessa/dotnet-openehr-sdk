using DotnetOpenEhr.Rm.Composition;
using Xunit;

namespace DotnetOpenEhr.Serialization.Json.Tests;

/// <summary>
/// Smoke-level shape tests for the <see cref="OpenEhrJson"/> façade.
/// </summary>
public sealed class FacadeShapeTests
{
    [Fact]
    public void ParseComposition_OnEmptyObject_ReturnsConstructedRoot()
    {
        const string json = """{"_type":"COMPOSITION","name":{"_type":"DV_TEXT","value":"Empty"},"archetype_node_id":"openEHR-EHR-COMPOSITION.encounter.v1","language":{"terminology_id":{"value":"ISO_639-1"},"code_string":"en"},"territory":{"terminology_id":{"value":"ISO_3166-1"},"code_string":"US"},"category":{"_type":"DV_CODED_TEXT","value":"event","defining_code":{"terminology_id":{"value":"openehr"},"code_string":"433"}},"composer":{"_type":"PARTY_SELF"}}""";
        Composition? c = OpenEhrJson.ParseComposition(json);
        Assert.NotNull(c);
        Assert.Equal("Empty", c!.Name.Value);
        Assert.Equal("openEHR-EHR-COMPOSITION.encounter.v1", c.ArchetypeNodeId);
    }

    [Fact]
    public async Task SerializeToStream_And_ParseAsync_FromStream_RoundTrip()
    {
        const string json = """{"_type":"COMPOSITION","name":{"_type":"DV_TEXT","value":"Async"},"archetype_node_id":"openEHR-EHR-COMPOSITION.report.v1","language":{"terminology_id":{"value":"ISO_639-1"},"code_string":"en"},"territory":{"terminology_id":{"value":"ISO_3166-1"},"code_string":"US"},"category":{"_type":"DV_CODED_TEXT","value":"event","defining_code":{"terminology_id":{"value":"openehr"},"code_string":"433"}},"composer":{"_type":"PARTY_SELF"}}""";
        Composition? c = OpenEhrJson.ParseComposition(json);
        Assert.NotNull(c);

        using MemoryStream output = new();
        OpenEhrJson.Serialize(output, c!);
        output.Position = 0;

        Composition? back = await OpenEhrJson.ParseCompositionAsync(output, TestContext.Current.CancellationToken);
        Assert.NotNull(back);
        Assert.Equal("Async", back!.Name.Value);
    }
}
