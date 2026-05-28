using System.Text.Json;
using DotnetOpenEhr.Rm.Composition;
using Xunit;

namespace DotnetOpenEhr.Serialization.Json.Flat.Tests;

/// <summary>
/// Data-driven verification of the lossless-catalogue: for every
/// fixture in the <c>schemaless-roundtrip</c> bucket, parsing and
/// re-serialising is byte-equivalent after canonical key ordering.
/// The schema-driven round-trip is exercised by
/// <see cref="FlatSchemaDrivenRoundTripTests"/>.
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
}
