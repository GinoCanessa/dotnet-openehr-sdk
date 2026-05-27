namespace DotnetOpenEhr.Archetypes.Aom2.Constraint;

// SPEC: AOM2.html#_archetype_constraint_class — abstract base of every
// constraint node in the AOM2 definition tree.

/// <summary>
/// Abstract base of every constraint node (<c>C_OBJECT</c>,
/// <c>C_ATTRIBUTE</c>, <c>C_OBJECT_TUPLE</c>, …).
/// </summary>
public abstract class ArchetypeConstraint : ArchetypeModelObject
{
    /// <summary>
    /// Dotted archetype path to this node, e.g. <c>/data[at0001]/items[at0002]</c>.
    /// </summary>
    public string Path { get; set; } = string.Empty;
}
