using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotnetOpenEhr.Serialization.Json.Flat.Tests;

/// <summary>
/// Strongly-typed model of <c>Fixtures/Flat/lossless-catalogue.json</c>:
/// a per-fixture record describing the bucket it belongs to and the
/// list of paths that the schemaless parser leaves unresolved.
/// </summary>
internal sealed record LosslessCatalogue
{
    [JsonPropertyName("fixtures")]
    public List<CatalogueEntry> Fixtures { get; init; } = [];

    public CatalogueEntry GetByFile(string file)
        => Fixtures.First(f => string.Equals(f.File, file, StringComparison.Ordinal));
}

internal sealed record CatalogueEntry
{
    [JsonPropertyName("file")]
    public string File { get; init; } = string.Empty;

    [JsonPropertyName("templateId")]
    public string TemplateId { get; init; } = string.Empty;

    [JsonPropertyName("bucket")]
    public string Bucket { get; init; } = string.Empty;

    [JsonPropertyName("unresolvedPaths")]
    public List<string> UnresolvedPaths { get; init; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LosslessCatalogue))]
internal sealed partial class CatalogueSerializationContext : JsonSerializerContext
{
}

internal static class CatalogueLoader
{
    public const string FileName = "lossless-catalogue.json";

    /// <summary>Source-tree path to the Fixtures/Flat directory. Set
    /// from <c>AssemblyMetadata("FixturesSourceDir", ...)</c> in the
    /// csproj so the bootstrap test can write the canonical manifest
    /// back to disk when invoked.</summary>
    public static string SourceDir { get; } = ResolveSourceDir();

    public static LosslessCatalogue Load()
    {
        string path = Path.Combine(SourceDir, FileName);
        byte[] bytes = File.ReadAllBytes(path);
        return JsonSerializer.Deserialize(bytes, CatalogueSerializationContext.Default.LosslessCatalogue)
            ?? throw new InvalidOperationException("Catalogue deserialised to null.");
    }

    [SuppressMessage("Performance", "CA1869:Cache and reuse 'JsonSerializerOptions'",
        Justification = "Single call site; readability over micro-optimisation.")]
    public static void Save(LosslessCatalogue catalogue)
    {
        string path = Path.Combine(SourceDir, FileName);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            catalogue, CatalogueSerializationContext.Default.LosslessCatalogue);
        File.WriteAllBytes(path, bytes);
    }

    public static void UpdateEntry(
        LosslessCatalogue catalogue,
        string file,
        string templateId,
        string bucket,
        IReadOnlyList<string> unresolvedPaths)
    {
        int idx = catalogue.Fixtures.FindIndex(f => string.Equals(f.File, file, StringComparison.Ordinal));
        if (idx < 0)
        {
            catalogue.Fixtures.Add(new CatalogueEntry
            {
                File = file,
                TemplateId = templateId,
                Bucket = bucket,
                UnresolvedPaths = [.. unresolvedPaths],
            });
            return;
        }
        catalogue.Fixtures[idx] = new CatalogueEntry
        {
            File = file,
            TemplateId = templateId,
            Bucket = bucket,
            UnresolvedPaths = [.. unresolvedPaths],
        };
    }

    private static string ResolveSourceDir()
    {
        IEnumerable<AssemblyMetadataAttribute> meta = typeof(CatalogueLoader).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>();
        foreach (AssemblyMetadataAttribute attr in meta)
        {
            if (string.Equals(attr.Key, "FixturesSourceDir", StringComparison.Ordinal)
                && !string.IsNullOrEmpty(attr.Value))
            {
                return attr.Value;
            }
        }
        throw new InvalidOperationException(
            "AssemblyMetadata 'FixturesSourceDir' is missing — check the test project's csproj.");
    }
}
