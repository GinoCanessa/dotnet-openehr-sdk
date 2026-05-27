using System.Text.Json.Serialization;

namespace DotnetOpenEhr.Rm.Common;

// SPEC: Common Information Model.html#_audit_details_class (Section 4.3.6).
/// <summary>Audit trail metadata captured for every committed change.</summary>
public class AuditDetails
{
    [JsonPropertyName("system_id")]
    public string SystemId { get; set; } = string.Empty;

    [JsonPropertyName("time_committed")]
    public DataTypes.DateTime.DvDateTime TimeCommitted { get; set; } = new();

    [JsonPropertyName("change_type")]
    public DataTypes.Text.DvCodedText ChangeType { get; set; } = new();

    [JsonPropertyName("description")]
    public DataTypes.Text.DvText? Description { get; set; }

    [JsonPropertyName("committer")]
    public PartyProxy Committer { get; set; } = new PartyIdentified();
}

// SPEC: Common Information Model.html#_attestation_class (Section 4.3.7).
/// <summary>Formal record that a party attested to the truth of a piece of EHR content.</summary>
public sealed class Attestation : AuditDetails
{
    [JsonPropertyName("attested_view")]
    public DataTypes.Encapsulated.DvMultimedia? AttestedView { get; set; }

    [JsonPropertyName("proof")]
    public string? Proof { get; set; }

    [JsonPropertyName("items")]
    public IReadOnlyList<DataTypes.Uri.DvEhrUri>? Items { get; set; }

    [JsonPropertyName("reason")]
    public DataTypes.Text.DvText Reason { get; set; } = new();

    [JsonPropertyName("is_pending")]
    public bool IsPending { get; set; }
}

// SPEC: Common Information Model.html#_original_version_class (Section 6.5.3).
/// <summary>
/// Concrete VERSION subtype describing the original creation of a piece
/// of versioned content. Generic in the spec; this SDK models the
/// payload as the strongly-typed root <see cref="Locatable"/>.
/// </summary>
public sealed class OriginalVersion
{
    [JsonPropertyName("uid")]
    public Support.ObjectVersionId Uid { get; set; } = new();

    [JsonPropertyName("preceding_version_uid")]
    public Support.ObjectVersionId? PrecedingVersionUid { get; set; }

    [JsonPropertyName("other_input_version_uids")]
    public IReadOnlyList<Support.ObjectVersionId>? OtherInputVersionUids { get; set; }

    [JsonPropertyName("lifecycle_state")]
    public DataTypes.Text.DvCodedText LifecycleState { get; set; } = new();

    [JsonPropertyName("attestations")]
    public IReadOnlyList<Attestation>? Attestations { get; set; }

    [JsonPropertyName("commit_audit")]
    public AuditDetails CommitAudit { get; set; } = new();

    [JsonPropertyName("contribution")]
    public Support.ObjectRef Contribution { get; set; } = new();

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    [JsonPropertyName("data")]
    public Locatable? Data { get; set; }
}
