using System.IO;
using DotnetOpenEhr.Rm.Composition;
using Xunit;

namespace DotnetOpenEhr.Serialization.Json.Flat.Tests;

/// <summary>
/// B4 — pin coverage for the 13 archived openfhir FLAT fixtures by
/// asserting that schemaless parse throws
/// <see cref="FlatSchemaRequiredException"/> with a non-empty
/// <see cref="FlatSchemaRequiredException.UnresolvedPaths"/> list and
/// a <see cref="FlatSchemaRequiredException.TemplateId"/> that matches
/// the catalogue entry.
/// </summary>
public sealed class OpenfhirArchiveTests
{
    private static readonly LosslessCatalogue Catalogue = CatalogueLoader.Load();

    public static IEnumerable<TheoryDataRow<string, string>> SchemaRequiredFixtures()
    {
        foreach (CatalogueEntry e in Catalogue.Fixtures)
        {
            if (string.Equals(e.Bucket, "schema-required", StringComparison.Ordinal))
            {
                yield return new TheoryDataRow<string, string>(e.File, e.TemplateId);
            }
        }
    }

    [Theory]
    [MemberData(nameof(SchemaRequiredFixtures))]
    public void SchemalessParse_ThrowsSchemaRequired_NamingUnresolvedPaths(string fixture, string expectedTemplateId)
    {
        string path = Path.Combine(CatalogueLoader.SourceDir, fixture);
        Assert.True(File.Exists(path), $"Archived fixture missing: {path}");

        byte[] utf8Json = File.ReadAllBytes(path);

        FlatSchemaRequiredException ex = Assert.Throws<FlatSchemaRequiredException>(
            () => OpenEhrFlatJson.ParseComposition(utf8Json));

        Assert.Equal(expectedTemplateId, ex.TemplateId);
        Assert.NotEmpty(ex.UnresolvedPaths);
    }
}
