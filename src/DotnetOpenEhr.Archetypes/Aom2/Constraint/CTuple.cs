namespace DotnetOpenEhr.Archetypes.Aom2.Constraint;

// SPEC: AOM2.html — tuple constraints. CAttributeTuple ties together a
// set of CAttribute paths whose value combinations are constrained by
// the children CObjectTuples.

/// <summary>
/// One row in a <see cref="CAttributeTuple"/>: a list of
/// <see cref="CObject"/>s, one per member attribute in declaration
/// order.
/// </summary>
public sealed class CObjectTuple : ArchetypeModelObject
{
    public List<CObject> Members { get; set; } = [];
}

/// <summary>
/// A tuple constraint that ties together a set of attribute paths whose
/// combinations of values are enumerated by <see cref="Children"/>.
/// </summary>
public sealed class CAttributeTuple : ArchetypeConstraint
{
    public List<CAttribute> Members { get; set; } = [];
    public List<CObjectTuple> Children { get; set; } = [];
}
