namespace DotnetOpenEhr.Bmm;

/// <summary>
/// Root of a parsed BMM (Basic Meta-Model) schema. Mirrors the
/// <c>BMM_SCHEMA</c> meta-type in the BMM 2.x specification.
/// </summary>
/// <remarks>
/// Scope: only the metadata + <c>packages</c> + <c>class_definitions</c>
/// productions used by the openEHR RM family BMMs are materialised. See
/// <see cref="BmmParser"/> for the explicit list.
/// </remarks>
public sealed class BmmModel
{
    public BmmModel(
        string name,
        string version,
        string? rmPublisher,
        string? rmRelease,
        IReadOnlyDictionary<string, BmmPackage> packages,
        IReadOnlyDictionary<string, BmmClass> classDefinitions)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(classDefinitions);

        Name = name;
        Version = version;
        RmPublisher = rmPublisher;
        RmRelease = rmRelease;
        Packages = packages;
        ClassDefinitions = classDefinitions;
    }

    /// <summary>
    /// Logical schema name — the BMM <c>model_name</c> attribute. May
    /// differ from the file name on disk.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The BMM <c>bmm_version</c> attribute value (e.g. <c>"2.1"</c>).
    /// </summary>
    public string Version { get; }

    public string? RmPublisher { get; }

    public string? RmRelease { get; }

    public IReadOnlyDictionary<string, BmmPackage> Packages { get; }

    /// <summary>
    /// Flat dictionary of every class declared in this schema, keyed by
    /// class name. Case-sensitive — use <see cref="GetClass"/> for the
    /// case-insensitive convenience accessor.
    /// </summary>
    public IReadOnlyDictionary<string, BmmClass> ClassDefinitions { get; }

    /// <summary>
    /// Case-insensitive class lookup. BMM class names are conventionally
    /// upper-case, but model sources are not always rigorous.
    /// </summary>
    public BmmClass? GetClass(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (ClassDefinitions.TryGetValue(name, out BmmClass? exact))
        {
            return exact;
        }
        foreach (KeyValuePair<string, BmmClass> kvp in ClassDefinitions)
        {
            if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }
        return null;
    }
}
