using System.Text.Json;
using DotnetOpenEhr.Rm.Composition;
using Xunit;

namespace DotnetOpenEhr.Serialization.Json.Flat.Tests;

/// <summary>
/// Data-driven verification of the lossless-catalogue: for every
/// fixture in the <c>schemaless-roundtrip</c> bucket, parsing and
/// re-serialising is byte-equivalent after canonical key ordering.
/// For every fixture in the <c>schema-required</c> bucket, schemaless
/// parse throws <see cref="FlatSchemaRequiredException"/> with the
/// exact unresolved-path list captured in the manifest.
/// </summary>
public sealed class FlatRoundTripTests
{
    private static readonly LosslessCatalogue Catalogue = CatalogueLoader.Load();

    public static IEnumerable<TheoryDataRow<string>> SchemalessRoundTripFixtures()
    {
        foreach (CatalogueEntry e in Catalogue.Fixtures)
        {
            if (string.Equals(e.Bucket, "schemaless-roundtrip", StringComparison.Ordinal))
            {
                yield return new TheoryDataRow<string>(e.File);
            }
        }
    }

    public static IEnumerable<TheoryDataRow<string>> SchemaRequiredFixtures()
    {
        foreach (CatalogueEntry e in Catalogue.Fixtures)
        {
            if (string.Equals(e.Bucket, "schema-required", StringComparison.Ordinal))
            {
                yield return new TheoryDataRow<string>(e.File);
            }
        }
    }

    [Theory]
    [MemberData(nameof(SchemalessRoundTripFixtures))]
    public void SchemalessRoundTrip_IsByteEquivalent_AfterCanonicalKeyOrdering(string fixture)
    {
        CatalogueEntry entry = Catalogue.GetByFile(fixture);

        byte[] original = FixtureLoader.Load(fixture);
        IReadOnlyList<KeyValuePair<FlatPath, JsonElement>> originalEntries = FlatJsonReader.Read(original);
        byte[] originalCanonical = FlatJsonWriter.WriteCanonical(originalEntries);

        Composition? parsed = OpenEhrFlatJson.ParseComposition(original);
        Assert.NotNull(parsed);

        byte[] reemitted = OpenEhrFlatJson.Serialize(parsed!, entry.TemplateId);
        IReadOnlyList<KeyValuePair<FlatPath, JsonElement>> reemittedEntries = FlatJsonReader.Read(reemitted);
        byte[] reemittedCanonical = FlatJsonWriter.WriteCanonical(reemittedEntries);

        Assert.Equal(originalCanonical, reemittedCanonical);
    }

    [Theory]
    [MemberData(nameof(SchemaRequiredFixtures))]
    public void SchemalessParse_OnSchemaRequiredFixture_ThrowsWithExpectedPaths(string fixture)
    {
        CatalogueEntry entry = Catalogue.GetByFile(fixture);
        byte[] data = FixtureLoader.Load(fixture);

        FlatSchemaRequiredException ex = Assert.Throws<FlatSchemaRequiredException>(
            () => OpenEhrFlatJson.ParseComposition(data));

        Assert.Equal(entry.TemplateId, ex.TemplateId);

        List<string> expected = [.. entry.UnresolvedPaths];
        List<string> actual = [.. ex.UnresolvedPaths];
        expected.Sort(StringComparer.Ordinal);
        actual.Sort(StringComparer.Ordinal);

        Assert.Equal(expected, actual);
    }
}
