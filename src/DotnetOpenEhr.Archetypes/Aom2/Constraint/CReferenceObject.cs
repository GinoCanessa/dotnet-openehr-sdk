namespace DotnetOpenEhr.Archetypes.Aom2.Constraint;

// SPEC: AOM2.html — reference object hierarchy. CReferenceObject and its
// concrete subtypes describe constraint nodes that refer to another
// archetype, an internal node, or proxy another structure.

/// <summary>
/// Abstract base for constraint nodes that reference another structure
/// (an archetype, an internal node, or a proxy).
/// </summary>
public abstract class CReferenceObject : CObject
{
}

/// <summary>
/// An <c>ARCHETYPE_SLOT</c> — a slot in an archetype that other
/// archetypes plug into, optionally constrained by include/exclude
/// assertions.
/// </summary>
public sealed class ArchetypeSlot : CReferenceObject
{
    public List<Assertion> Includes { get; set; } = [];
    public List<Assertion> Excludes { get; set; } = [];
    public bool IsClosed { get; set; }
}

/// <summary>
/// A reference to another node within the same archetype.
/// </summary>
public sealed class ArchetypeInternalRef : CReferenceObject
{
    public string TargetPath { get; set; } = string.Empty;
}

/// <summary>
/// A reference to the root of another archetype.
/// </summary>
public sealed class CArchetypeRoot : CComplexObject
{
    public string ArchetypeRef { get; set; } = string.Empty;
}

/// <summary>
/// A proxy node that refers to another <see cref="CComplexObject"/> by
/// path for re-use within the same archetype.
/// </summary>
public sealed class CComplexObjectProxy : CReferenceObject
{
    public string TargetPath { get; set; } = string.Empty;
}
