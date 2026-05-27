using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Reflection;
using DotnetOpenEhr.Bmm;

namespace DotnetOpenEhr.Bmm.Rm;

/// <summary>
/// Loader for the canonical openEHR Reference Model BMM schemas bundled
/// with this package. The set of embedded files is fixed at build time
/// and is sourced verbatim from
/// <see href="https://github.com/openEHR/specifications-ITS-BMM">openEHR/specifications-ITS-BMM</see>
/// (see <c>THIRD_PARTY_NOTICES.md</c> for the exact commit SHA and the
/// per-file mapping).
/// </summary>
/// <remarks>
/// The first call to <see cref="LoadDefault"/> performs all parsing and
/// caches the result. Subsequent calls return the same instance.
/// </remarks>
public static class OpenEhrRmBmm
{
    /// <summary>
    /// Logical model name on the merged <see cref="BmmModel"/> returned
    /// by <see cref="LoadDefault"/>. Picked to avoid colliding with any
    /// individual schema's <c>schema_name</c>.
    /// </summary>
    public const string CombinedModelName = "openehr_rm_combined";

    private static readonly Assembly s_assembly = typeof(OpenEhrRmBmm).Assembly;

    private static readonly Lazy<BmmModel> s_cached =
        new(LoadInternal, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly FrozenSet<string> s_embeddedFileNames =
        new[]
        {
            "openehr_base_120.bmm",
            "openehr_base_base_types_120.bmm",
            "openehr_base_foundation_types_120.bmm",
            "openehr_base_resource_120.bmm",
            "openehr_rm_110.bmm",
            "openehr_rm_data_types_110.bmm",
            "openehr_rm_demographic_110.bmm",
            "openehr_rm_ehr_110.bmm",
            "openehr_rm_ehr_extract_110.bmm",
            "openehr_rm_structures_110.bmm",
        }
        .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Canonical embedded BMM file names, in declaration order.
    /// Diagnostic-only — the merge order in <see cref="LoadDefault"/>
    /// is irrelevant to the result because schemas are disjoint by
    /// class name.
    /// </summary>
    public static IReadOnlyCollection<string> EmbeddedFileNames => s_embeddedFileNames;

    /// <summary>
    /// Loads every embedded openEHR RM BMM schema, parses each into a
    /// <see cref="BmmModel"/>, and returns a single merged model whose
    /// <see cref="BmmModel.ClassDefinitions"/> is the union of all class
    /// definitions across schemas and whose
    /// <see cref="BmmModel.Packages"/> is the union of all top-level
    /// packages.
    /// </summary>
    /// <remarks>
    /// Cached on first call. Subsequent calls are O(1).
    /// </remarks>
    public static BmmModel LoadDefault() => s_cached.Value;

    private static BmmModel LoadInternal()
    {
        Dictionary<string, BmmClass> mergedClasses = new(StringComparer.Ordinal);
        Dictionary<string, BmmPackage> mergedPackages = new(StringComparer.Ordinal);
        string? bmmVersion = null;
        string? rmPublisher = null;
        string? rmRelease = null;

        foreach (string fileName in s_embeddedFileNames)
        {
            string source = ReadEmbeddedFile(fileName);
            BmmModel model;
            try
            {
                model = BmmParser.Parse(source);
            }
            catch (BmmParseException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to parse embedded BMM resource '{fileName}': {ex.Message}",
                    ex);
            }

            bmmVersion ??= model.Version;
            rmPublisher ??= model.RmPublisher;
            rmRelease ??= model.RmRelease;

            foreach (KeyValuePair<string, BmmClass> kvp in model.ClassDefinitions)
            {
                // Last writer wins; in practice no class is declared in
                // more than one openEHR RM schema. Defensive against an
                // accidental duplicate by overwriting.
                mergedClasses[kvp.Key] = kvp.Value;
            }
            foreach (KeyValuePair<string, BmmPackage> kvp in model.Packages)
            {
                mergedPackages[kvp.Key] = kvp.Value;
            }
        }

        return new BmmModel(
            name: CombinedModelName,
            version: bmmVersion ?? "unknown",
            rmPublisher: rmPublisher,
            rmRelease: rmRelease,
            packages: mergedPackages,
            classDefinitions: mergedClasses);
    }

    private static string ReadEmbeddedFile(string fileName)
    {
        string resourceName = $"DotnetOpenEhr.Bmm.Rm.Resources.{fileName}";
        using Stream? stream = s_assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException(
                $"Embedded BMM resource '{resourceName}' not found. Check that the file is included as an EmbeddedResource in the csproj.");
        }
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
