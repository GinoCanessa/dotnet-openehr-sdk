namespace DotnetOpenEhr.Aql.Paths;

/// <summary>
/// One step of a parsed archetype path: an attribute name plus an
/// optional bracket predicate. Internal model owned by the path parser
/// and consumed by <see cref="DotnetOpenEhr.Aql.ArchetypePath"/> /
/// <see cref="DotnetOpenEhr.Aql.ArchetypePathResolver"/>.
/// </summary>
internal sealed record ArchetypePathSegment(
    string AttributeName,
    ArchetypePathPredicate? Predicate);

/// <summary>
/// Bracket predicate captured from an archetype path. At least one of
/// <see cref="NodeId"/> or <see cref="Name"/> is non-null when an
/// instance exists. <see cref="NodeId"/> accepts <c>atN</c>, <c>idN</c>,
/// and <c>acN</c> codes as well as full archetype HRIDs such as
/// <c>openEHR-EHR-OBSERVATION.blood_pressure.v2</c>.
/// </summary>
internal sealed record ArchetypePathPredicate(string? NodeId, string? Name);
