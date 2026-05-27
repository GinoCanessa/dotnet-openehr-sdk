using DotnetOpenEhr.Foundation;

namespace DotnetOpenEhr.Archetypes.Aom2.Constraint;

// SPEC: AOM2.html — attribute constraint hierarchy. CAttribute is
// abstract; CSingleAttribute / CMultipleAttribute are the two concrete
// shapes.

/// <summary>
/// Abstract base of attribute constraints (a constraint on an RM
/// attribute slot).
/// </summary>
public abstract class CAttribute : ArchetypeConstraint
{
    public string RmAttributeName { get; set; } = string.Empty;
    public Interval<int>? Existence { get; set; }
    public List<CObject> Children { get; set; } = [];
}

/// <summary>
/// Constraint on a single-valued attribute.
/// </summary>
public sealed class CSingleAttribute : CAttribute
{
}

/// <summary>
/// Constraint on a multi-valued attribute (with optional cardinality).
/// </summary>
public sealed class CMultipleAttribute : CAttribute
{
    public Cardinality? Cardinality { get; set; }
}
