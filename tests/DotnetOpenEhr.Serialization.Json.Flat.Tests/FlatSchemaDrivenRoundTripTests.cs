using System.IO;
using System.Text;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes;
using DotnetOpenEhr.Rm.DataTypes.Basic;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Serialization.Json;
using DotnetOpenEhr.Templates;
using Xunit;

namespace DotnetOpenEhr.Serialization.Json.Flat.Tests;

/// <summary>
/// End-to-end schema-driven FLAT round-trip over hand-authored
/// OPT2 + canonical Composition pairs. For every pair under
/// <c>Fixtures/FlatSchemaDriven/</c>:
/// <list type="number">
///   <item>parse OPT2 → <see cref="OperationalTemplate"/></item>
///   <item>parse canonical composition.json → <see cref="Composition"/></item>
///   <item>FLAT-serialise the Composition using the template as schema</item>
///   <item>FLAT-parse the resulting bytes back using the same schema</item>
///   <item>structurally compare original vs round-tripped Compositions</item>
/// </list>
/// Also asserts catalogue invariants: zero <c>schema-required</c>
/// entries remain, and every archived openfhir fixture still exists on
/// disk for provenance.
/// </summary>
public sealed class FlatSchemaDrivenRoundTripTests
{
    private static readonly LosslessCatalogue Catalogue = CatalogueLoader.Load();

    public static IEnumerable<TheoryDataRow<string, string>> SchemaDrivenPairs()
    {
        foreach (CatalogueEntry e in Catalogue.Fixtures)
        {
            if (string.Equals(e.Bucket, "schema-driven-roundtrip", StringComparison.Ordinal))
            {
                // e.File is "FlatSchemaDriven/<folder>"; strip the prefix.
                const string prefix = "FlatSchemaDriven/";
                string folder = e.File.StartsWith(prefix, StringComparison.Ordinal)
                    ? e.File.Substring(prefix.Length)
                    : e.File;
                yield return new TheoryDataRow<string, string>(folder, e.TemplateId);
            }
        }
    }

    [Theory]
    [MemberData(nameof(SchemaDrivenPairs))]
    public void SchemaDriven_RoundTrip_PreservesStructure(string folder, string expectedTemplateId)
    {
        // 1. OPT2 → schema
        string opt2 = SchemaDrivenFixtureLoader.LoadText(folder, "template.opt2");
        OperationalTemplate template = Opt2Parser.Parse(opt2);
        Assert.Equal(expectedTemplateId, template.TemplateId);

        // 2. canonical composition.json → Composition
        byte[] canonicalBytes = SchemaDrivenFixtureLoader.Load(folder, "composition.json");
        Composition? original = OpenEhrJson.ParseComposition(canonicalBytes);
        Assert.NotNull(original);

        // 3. RM → FLAT (schema-driven)
        byte[] flatBytes = OpenEhrFlatJson.Serialize(original!, template);
        Assert.NotEmpty(flatBytes);

        // Sanity: FLAT bytes must be a non-empty JSON object with at
        // least one path rooted at the template id.
        string flatText = Encoding.UTF8.GetString(flatBytes);
        Assert.Contains($"\"{expectedTemplateId}/", flatText, StringComparison.Ordinal);

        // 4. FLAT → RM (schema-driven)
        Composition? roundtripped = OpenEhrFlatJson.ParseComposition(flatBytes, template);
        Assert.NotNull(roundtripped);

        // 5. Structural compare. Use a strict shape walker that
        // verifies content tree depth + scalar values at the leaves —
        // not byte-equality of canonical JSON, because the FLAT path
        // does not preserve every metadata field (e.g. composer
        // identifiers, encoding, archetype_details).
        AssertStructurallyEquivalent(original!, roundtripped!);
    }

    [Fact]
    public void Catalogue_HasNo_SchemaRequired_Entries()
    {
        IEnumerable<string> schemaRequired = Catalogue.Fixtures
            .Where(f => string.Equals(f.Bucket, "schema-required", StringComparison.Ordinal))
            .Select(f => f.File);

        Assert.Empty(schemaRequired);
    }

    [Fact]
    public void ArchivedOpenfhirFixtures_StillExistOnDisk()
    {
        string archive = Path.Combine(CatalogueLoader.SourceDir, "openfhir-archive");
        Assert.True(Directory.Exists(archive),
            $"Expected openfhir archive directory at: {archive}");

        string[] expected =
        [
            "stu3_blood_pressure_flat.json",
            "blood_pressure_flat.json",
            "news2_encounter_parent_flat.json",
            "medication_order_flat.json",
            "growth_chart_flat.json",
            "kds_prozedur_flat.json",
            "kds_person_flat.json",
            "kds_diagnose_composition_flat.json",
            "kds_fall_einfach_flat.json",
            "kds_laborbericht_flat.json",
            "kds_medikationseintrag_flat.json",
            "kds_medikamentenverabreichungen_flat.json",
            "studienteilnahme_flat.json",
        ];

        foreach (string file in expected)
        {
            string path = Path.Combine(archive, file);
            Assert.True(File.Exists(path), $"Archived fixture missing: {path}");
        }
    }

    // ----- structural comparator -----

    private static void AssertStructurallyEquivalent(Composition expected, Composition actual)
    {
        IList<ContentItem> expectedContent = expected.Content ?? [];
        IList<ContentItem> actualContent = actual.Content ?? [];
        Assert.Equal(expectedContent.Count, actualContent.Count);
        for (int i = 0; i < expectedContent.Count; i++)
        {
            AssertContentItem(expectedContent[i], actualContent[i], $"content[{i}]");
        }
    }

    private static void AssertContentItem(ContentItem expected, ContentItem actual, string path)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        AssertLocatable(expected, actual, path);

        switch (expected)
        {
            case Section es when actual is Section asec:
                IList<ContentItem> ei = es.Items ?? [];
                IList<ContentItem> ai = asec.Items ?? [];
                Assert.Equal(ei.Count, ai.Count);
                for (int i = 0; i < ei.Count; i++)
                {
                    AssertContentItem(ei[i], ai[i], $"{path}/items[{i}]");
                }
                break;

            case Observation eo when actual is Observation ao:
                AssertHistory(eo.Data, ao.Data, $"{path}/data");
                break;

            case Evaluation ee when actual is Evaluation ae:
                AssertItemStructure(ee.Data, ae.Data, $"{path}/data");
                break;

            case AdminEntry _:
                // AdminEntry data not required by current fixtures.
                break;

            default:
                throw new InvalidOperationException(
                    $"{path}: unsupported ContentItem subtype {expected.GetType().Name}");
        }
    }

    private static void AssertHistory(History? expected, History? actual, string path)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        IList<Event> ee = expected!.Events ?? [];
        IList<Event> ae = actual!.Events ?? [];
        Assert.Equal(ee.Count, ae.Count);
        for (int i = 0; i < ee.Count; i++)
        {
            AssertEvent(ee[i], ae[i], $"{path}/events[{i}]");
        }
    }

    private static void AssertEvent(Event expected, Event actual, string path)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        AssertLocatable(expected, actual, path);
        AssertItemStructure(expected.Data, actual.Data, $"{path}/data");
    }

    private static void AssertItemStructure(ItemStructure? expected, ItemStructure? actual, string path)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected!.GetType(), actual!.GetType());
        AssertLocatable(expected, actual, path);

        IReadOnlyList<Item> expectedItems = GetItemStructureItems(expected);
        IReadOnlyList<Item> actualItems = GetItemStructureItems(actual);
        Assert.Equal(expectedItems.Count, actualItems.Count);
        for (int i = 0; i < expectedItems.Count; i++)
        {
            AssertItem(expectedItems[i], actualItems[i], $"{path}/items[{i}]");
        }
    }

    private static IReadOnlyList<Item> GetItemStructureItems(ItemStructure s) => s switch
    {
        ItemTree t => (IReadOnlyList<Item>)(t.Items?.ToList() ?? []),
        ItemList l => (IReadOnlyList<Item>)(l.Items?.Cast<Item>().ToList() ?? []),
        ItemSingle one => one.Item is null ? [] : [one.Item],
        _ => throw new InvalidOperationException($"Unsupported ItemStructure: {s.GetType().Name}"),
    };

    private static void AssertItem(Item expected, Item actual, string path)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        AssertLocatable(expected, actual, path);

        switch (expected)
        {
            case Cluster ec when actual is Cluster ac:
                Assert.Equal(ec.Items.Count, ac.Items.Count);
                for (int i = 0; i < ec.Items.Count; i++)
                {
                    AssertItem(ec.Items[i], ac.Items[i], $"{path}/items[{i}]");
                }
                break;

            case Element ee when actual is Element ae:
                AssertDataValue(ee.Value, ae.Value, $"{path}/value");
                break;

            default:
                throw new InvalidOperationException(
                    $"{path}: unsupported Item subtype {expected.GetType().Name}");
        }
    }

    private static void AssertDataValue(DataValue? expected, DataValue? actual, string path)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected!.GetType(), actual!.GetType());

        switch (expected)
        {
            case DvQuantity eq when actual is DvQuantity aq:
                Assert.Equal(eq.Magnitude, aq.Magnitude);
                Assert.Equal(eq.Units, aq.Units);
                if (eq.Precision is int p)
                {
                    Assert.Equal(p, aq.Precision);
                }
                break;

            case DvCount ec when actual is DvCount ac:
                Assert.Equal(ec.Magnitude, ac.Magnitude);
                break;

            case DvBoolean eb when actual is DvBoolean ab:
                Assert.Equal(eb.Value, ab.Value);
                break;

            case DvCodedText edct when actual is DvCodedText adct:
                Assert.Equal(edct.Value, adct.Value);
                Assert.NotNull(edct.DefiningCode);
                Assert.NotNull(adct.DefiningCode);
                Assert.Equal(edct.DefiningCode!.CodeString, adct.DefiningCode!.CodeString);
                Assert.Equal(edct.DefiningCode.TerminologyId.Value, adct.DefiningCode.TerminologyId.Value);
                break;

            case DvText et when actual is DvText at:
                Assert.Equal(et.Value, at.Value);
                break;

            default:
                throw new InvalidOperationException(
                    $"{path}: unsupported DataValue subtype {expected.GetType().Name}");
        }
    }

    private static void AssertLocatable(Locatable expected, Locatable actual, string path)
    {
        Assert.Equal(expected.ArchetypeNodeId, actual.ArchetypeNodeId);
        Assert.NotNull(expected.Name);
        Assert.NotNull(actual.Name);
        Assert.Equal(expected.Name!.Value, actual.Name!.Value);
    }
}

internal static class SchemaDrivenFixtureLoader
{
    private const string Prefix = "DotnetOpenEhr.Serialization.Json.Flat.Tests.Fixtures.FlatSchemaDriven.";

    public static byte[] Load(string folder, string fileName)
    {
        string resource = Prefix + folder + "." + fileName;
        using Stream s = typeof(SchemaDrivenFixtureLoader).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException(
                $"Embedded fixture '{resource}' missing. Check csproj EmbeddedResource glob.");
        using MemoryStream ms = new();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public static string LoadText(string folder, string fileName)
        => Encoding.UTF8.GetString(Load(folder, fileName));
}
