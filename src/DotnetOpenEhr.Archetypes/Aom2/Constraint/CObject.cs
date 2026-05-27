using DotnetOpenEhr.Foundation;

namespace DotnetOpenEhr.Archetypes.Aom2.Constraint;

// SPEC: AOM2.html#_c_object_class — abstract base of any node that
// constrains an RM object instance.

/// <summary>
/// Abstract base of any constraint on an RM object instance.
/// </summary>
public abstract class CObject : ArchetypeConstraint
{
    public string RmTypeName { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public Interval<int>? Occurrences { get; set; }
    public int? SiblingOrder { get; set; }
}
