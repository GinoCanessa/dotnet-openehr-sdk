namespace DotnetOpenEhr.Archetypes.Aom2.Resource;

// SPEC: Resource Model.html — RevisionHistory, RevisionHistoryItem,
// AuditDetails (BASE Release-1.2.0).

/// <summary>
/// One audit record (committer, time, optional change type and
/// description) within a <see cref="RevisionHistoryItem"/>.
/// </summary>
public sealed class AuditDetails
{
    public string SystemId { get; set; } = string.Empty;
    public string CommitterName { get; set; } = string.Empty;
    public string TimeCommitted { get; set; } = string.Empty;
    public string? ChangeType { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// A single revision-history entry: one version id plus the audit
/// records that accompany it.
/// </summary>
public sealed class RevisionHistoryItem
{
    public string VersionId { get; set; } = string.Empty;
    public List<AuditDetails> Audits { get; set; } = [];
}

/// <summary>
/// Append-only revision history attached to an
/// <see cref="AuthoredResource"/>.
/// </summary>
public sealed class RevisionHistory
{
    public List<RevisionHistoryItem> Items { get; set; } = [];
}
