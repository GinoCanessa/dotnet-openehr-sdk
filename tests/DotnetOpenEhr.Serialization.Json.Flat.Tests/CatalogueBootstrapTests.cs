using Xunit;

namespace DotnetOpenEhr.Serialization.Json.Flat.Tests;

/// <summary>
/// One-shot manifest bootstrap. Runs the schemaless parser against
/// every embedded FLAT fixture and rewrites
/// <c>Fixtures/Flat/lossless-catalogue.json</c> with the actual
/// templateId + unresolved-paths captured from each run. Disabled by
/// default — enable by setting the environment variable
/// <c>OPENEHR_FLAT_BOOTSTRAP=1</c>.
/// </summary>
/// <remarks>
/// This test exists so the manifest is the empirical source-of-truth
/// for what the schemaless parser actually leaves unresolved, rather
/// than a hand-predicted list. After bootstrapping, the manifest is
/// checked in and <see cref="FlatRoundTripTests"/> verifies the
/// running parser still produces those same unresolved sets.
/// </remarks>
public sealed class CatalogueBootstrapTests
{
    [Fact(Explicit = true)]
    public void BootstrapManifest_When_Env_Var_Set()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("OPENEHR_FLAT_BOOTSTRAP"),
                "1",
                StringComparison.Ordinal))
        {
            return; // No-op in normal test runs.
        }

        LosslessCatalogue catalogue = CatalogueLoader.Load();

        List<CatalogueEntry> snapshot = [.. catalogue.Fixtures];
        foreach (CatalogueEntry entry in snapshot)
        {
            if (string.Equals(entry.Bucket, "schemaless-roundtrip", StringComparison.Ordinal))
            {
                // Re-evaluate templateId from the actual fixture so we
                // catch any manifest drift.
                byte[] data = FixtureLoader.Load(entry.File);
                IReadOnlyList<KeyValuePair<FlatPath, System.Text.Json.JsonElement>> parsed =
                    FlatJsonReader.Read(data);
                string templateId = string.Empty;
                foreach (KeyValuePair<FlatPath, System.Text.Json.JsonElement> kvp in parsed)
                {
                    string head = kvp.Key.TemplateId;
                    if (!string.IsNullOrEmpty(head) && !string.Equals(head, "ctx", StringComparison.Ordinal))
                    {
                        templateId = head;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(templateId) && parsed.Count > 0)
                {
                    templateId = parsed[0].Key.TemplateId;
                }
                entry.UnresolvedPaths.Clear();
                CatalogueLoader.UpdateEntry(catalogue, entry.File, templateId, entry.Bucket, []);
                continue;
            }

            if (string.Equals(entry.Bucket, "schema-required", StringComparison.Ordinal))
            {
                byte[] data = FixtureLoader.Load(entry.File);
                string templateId;
                List<string> unresolved = [];
                try
                {
                    OpenEhrFlatJson.ParseComposition(data);
                    templateId = entry.TemplateId; // parse succeeded — wrong bucket.
                    throw new InvalidOperationException(
                        $"Fixture '{entry.File}' is in schema-required bucket but schemaless parse succeeded.");
                }
                catch (FlatSchemaRequiredException ex)
                {
                    templateId = ex.TemplateId;
                    unresolved.AddRange(ex.UnresolvedPaths);
                }
                unresolved.Sort(StringComparer.Ordinal);
                CatalogueLoader.UpdateEntry(catalogue, entry.File, templateId, entry.Bucket, unresolved);
            }
        }

        CatalogueLoader.Save(catalogue);
    }
}
