using DotnetOpenEhr.Archetypes.Aom2.Terminology;

namespace DotnetOpenEhr.Archetypes.Aom2;

// SPEC: AOM2.html#_resource_annotations_class — keyed dictionary of
// archetype path → annotation object (which itself is a keyed bag of
// translation-keyed text terms).

/// <summary>
/// A free-form annotation attached to a single archetype path.
/// </summary>
public sealed class ArchetypeAnnotation
{
    /// <summary>
    /// Per-language, per-key annotation text:
    /// <c>language → key → ArchetypeTerm</c>.
    /// </summary>
    public Dictionary<string, Dictionary<string, ArchetypeTerm>> Items { get; set; } = [];
}

/// <summary>
/// Container for archetype-level annotations, keyed by archetype path.
/// </summary>
public sealed class ResourceAnnotations
{
    public Dictionary<string, ArchetypeAnnotation> Items { get; set; } = [];
}
