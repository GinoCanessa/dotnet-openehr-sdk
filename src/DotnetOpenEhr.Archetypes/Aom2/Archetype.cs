using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using DotnetOpenEhr.Archetypes.Aom2.Resource;
using DotnetOpenEhr.Archetypes.Aom2.Terminology;
using DotnetOpenEhr.Archetypes.Identification;

namespace DotnetOpenEhr.Archetypes.Aom2;

// SPEC: AOM2.html#_archetype_class — abstract root of every archetype.
// Concrete subtypes (this file): AuthoredArchetype, Template,
// TemplateOverlay, OperationalTemplate. OperationalTemplate is
// non-sealed and has a protected internal constructor so the
// DotnetOpenEhr.Templates package (Phase 8a) can subclass it as sealed
// and add OPT-specific members.

/// <summary>
/// Abstract base of every openEHR archetype.
/// </summary>
public abstract class Archetype : AuthoredResource
{
    public ArchetypeHRID ArchetypeId { get; set; } = null!;
    public ArchetypeHRID? ParentArchetypeId { get; set; }
    public bool IsTemplate { get; set; }
    public bool IsDifferential { get; set; }
    public CComplexObject Definition { get; set; } = new();
    public ArchetypeTerminology Terminology { get; set; } = new();
    public RulesSection? Rules { get; set; }
    public new ResourceAnnotations? Annotations { get; set; }
}

/// <summary>
/// A standalone archetype authored by an end user (the everyday case).
/// </summary>
public sealed class AuthoredArchetype : Archetype
{
}

/// <summary>
/// A template archetype that composes one or more standalone
/// archetypes into a higher-level model.
/// </summary>
public sealed class Template : Archetype
{
}

/// <summary>
/// An overlay applied within a <see cref="Template"/>.
/// </summary>
public sealed class TemplateOverlay : Archetype
{
}

/// <summary>
/// An operational template (OPT) — the flattened, fully-resolved
/// archetype tree produced from a <see cref="Template"/>.
/// </summary>
/// <remarks>
/// Declared here as a non-sealed class with a
/// <see langword="protected internal"/> constructor so the
/// <c>DotnetOpenEhr.Templates</c> package (Phase 8a) can subclass it as
/// <see langword="sealed"/> and add OPT-specific members. Direct
/// instantiation from outside the assembly is not supported; consumers
/// should use the templates package once it ships.
/// </remarks>
public class OperationalTemplate : Archetype
{
    protected internal OperationalTemplate()
    {
    }
}
