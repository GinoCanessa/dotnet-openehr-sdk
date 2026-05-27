using DotnetOpenEhr.Archetypes.Aom2;

namespace DotnetOpenEhr.Archetypes.Aom2.Resource;

// SPEC: Resource Model.html (BASE Release-1.2.0) — AuthoredResource and
// related metadata classes.

/// <summary>
/// Translation metadata for a single non-original language carried by an
/// <see cref="AuthoredResource"/>.
/// </summary>
public sealed class TranslationDetails
{
    public string Language { get; set; } = string.Empty;
    public Dictionary<string, string> Author { get; set; } = [];
    public List<string>? Accreditation { get; set; }
    public Dictionary<string, string>? OtherDetails { get; set; }
    public string? VersionLastTranslated { get; set; }
}

/// <summary>
/// Per-language description of a resource (purpose, use, misuse, etc.).
/// </summary>
public sealed class ResourceDescriptionItem
{
    public string Language { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public List<string>? Keywords { get; set; }
    public string? Use { get; set; }
    public string? Misuse { get; set; }
    public string? Copyright { get; set; }
    public Dictionary<string, string>? OriginalResourceUri { get; set; }
    public Dictionary<string, string>? OtherDetails { get; set; }
}

/// <summary>
/// Multi-language description of a resource, with per-language detail
/// items keyed by language tag.
/// </summary>
public sealed class ResourceDescription
{
    public Dictionary<string, string> OriginalAuthor { get; set; } = [];
    public List<string>? OtherContributors { get; set; }
    public string LifecycleState { get; set; } = string.Empty;
    public Dictionary<string, ResourceDescriptionItem> Details { get; set; } = [];
    public string? ResourcePackageUri { get; set; }
    public Dictionary<string, string>? OtherDetails { get; set; }
    public string? Copyright { get; set; }
    public Dictionary<string, string>? Licence { get; set; }
    public List<string>? IpAcknowledgements { get; set; }
    public List<string>? References { get; set; }
    public List<string>? ConformsTo { get; set; }
}

/// <summary>
/// Abstract root of every authored openEHR knowledge resource
/// (Archetype, Template, Operational Template).
/// </summary>
/// <remarks>
/// The openEHR specification declares Archetype as inheriting both
/// <c>AUTHORED_RESOURCE</c> and <c>ARCHETYPE_MODEL_OBJECT</c>. C# only
/// supports single inheritance, so we collapse those into a single
/// chain: <see cref="ArchetypeModelObject"/> → <see cref="AuthoredResource"/>
/// → <c>Archetype</c>. All three sets of members remain available on a
/// concrete <c>Archetype</c> instance.
/// </remarks>
public abstract class AuthoredResource : ArchetypeModelObject
{
    public string OriginalLanguage { get; set; } = string.Empty;
    public Dictionary<string, TranslationDetails>? Translations { get; set; }
    public ResourceDescription Description { get; set; } = new();
    public RevisionHistory? RevisionHistory { get; set; }
    public bool IsControlled { get; set; }
    public string Uid { get; set; } = string.Empty;
    public ArchetypeModelObject? Annotations { get; set; }
}
