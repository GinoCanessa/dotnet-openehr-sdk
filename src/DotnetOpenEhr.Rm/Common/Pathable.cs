using System.Text.Json.Serialization;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.Demographic;
using DotnetOpenEhr.Rm.Ehr;

namespace DotnetOpenEhr.Rm.Common;

// SPEC: Common Information Model.html#_pathable_class (Section 3.2.1).
/// <summary>
/// Abstract base class of every node in the openEHR Reference Model
/// that can be located by path. In this SDK <c>Pathable</c> carries
/// only the shape; the path-evaluation helpers live in
/// <c>DotnetOpenEhr.Aql.ArchetypePathResolver</c> (one-shot) and
/// <c>DotnetOpenEhr.Aql.ArchetypePath</c> (pre-compiled).
/// </summary>
public abstract class Pathable
{
}

// SPEC: Common Information Model.html#_locatable_class (Section 3.2.2).
/// <summary>
/// Abstract base of every openEHR RM class that is archetypable.
/// Carries a runtime name, an archetype node id, and the optional
/// archetype details, links, feeder audit and uid.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(Cluster),                "CLUSTER")]
[JsonDerivedType(typeof(Element),                "ELEMENT")]
[JsonDerivedType(typeof(ItemTree),               "ITEM_TREE")]
[JsonDerivedType(typeof(ItemList),               "ITEM_LIST")]
[JsonDerivedType(typeof(ItemSingle),             "ITEM_SINGLE")]
[JsonDerivedType(typeof(ItemTable),              "ITEM_TABLE")]
[JsonDerivedType(typeof(History),                "HISTORY")]
[JsonDerivedType(typeof(PointEvent),             "POINT_EVENT")]
[JsonDerivedType(typeof(IntervalEvent),          "INTERVAL_EVENT")]
[JsonDerivedType(typeof(Composition.Composition), "COMPOSITION")]
[JsonDerivedType(typeof(Section),                "SECTION")]
[JsonDerivedType(typeof(Observation),            "OBSERVATION")]
[JsonDerivedType(typeof(Evaluation),             "EVALUATION")]
[JsonDerivedType(typeof(Instruction),            "INSTRUCTION")]
[JsonDerivedType(typeof(Composition.Action),     "ACTION")]
[JsonDerivedType(typeof(AdminEntry),             "ADMIN_ENTRY")]
[JsonDerivedType(typeof(Activity),               "ACTIVITY")]
[JsonDerivedType(typeof(EhrStatus),              "EHR_STATUS")]
[JsonDerivedType(typeof(Person),                 "PERSON")]
[JsonDerivedType(typeof(Organisation),           "ORGANISATION")]
[JsonDerivedType(typeof(Group),                  "GROUP")]
[JsonDerivedType(typeof(Agent),                  "AGENT")]
[JsonDerivedType(typeof(Role),                   "ROLE")]
[JsonDerivedType(typeof(Address),                "ADDRESS")]
[JsonDerivedType(typeof(Contact),                "CONTACT")]
[JsonDerivedType(typeof(PartyIdentity),          "PARTY_IDENTITY")]
[JsonDerivedType(typeof(Capability),             "CAPABILITY")]
public abstract class Locatable : Pathable
{
    [JsonPropertyName("name")]
    public DataTypes.Text.DvText Name { get; set; } = new();

    [JsonPropertyName("archetype_node_id")]
    public string ArchetypeNodeId { get; set; } = string.Empty;

    [JsonPropertyName("uid")]
    public Support.UidBasedId? Uid { get; set; }

    [JsonPropertyName("links")]
    public IReadOnlyList<Link>? Links { get; set; }

    [JsonPropertyName("archetype_details")]
    public Archetyped? ArchetypeDetails { get; set; }

    [JsonPropertyName("feeder_audit")]
    public FeederAudit? FeederAudit { get; set; }
}

// SPEC: Common Information Model.html#_link_class (Section 3.2.4).
/// <summary>Logical relationship between two archetyped structures.</summary>
public sealed class Link
{
    [JsonPropertyName("meaning")]
    public DataTypes.Text.DvText Meaning { get; set; } = new();

    [JsonPropertyName("type")]
    public DataTypes.Text.DvText Type { get; set; } = new();

    [JsonPropertyName("target")]
    public DataTypes.Uri.DvEhrUri Target { get; set; } = new();
}

// SPEC: Common Information Model.html#_archetyped_class (Section 3.2.3).
/// <summary>Archetyping metadata attached at archetype-root points.</summary>
public sealed class Archetyped
{
    [JsonPropertyName("archetype_id")]
    public Support.ArchetypeId ArchetypeId { get; set; } = new();

    [JsonPropertyName("template_id")]
    public Support.TemplateId? TemplateId { get; set; }

    [JsonPropertyName("rm_version")]
    public string RmVersion { get; set; } = string.Empty;
}

// SPEC: Common Information Model.html#_feeder_audit_class (Section 3.2.5).
/// <summary>Audit trail recording the origin of feeder-system data.</summary>
public sealed class FeederAudit
{
    [JsonPropertyName("originating_system_audit")]
    public FeederAuditDetails OriginatingSystemAudit { get; set; } = new();

    [JsonPropertyName("originating_system_item_ids")]
    public IReadOnlyList<DataTypes.Basic.DvIdentifier>? OriginatingSystemItemIds { get; set; }

    [JsonPropertyName("feeder_system_audit")]
    public FeederAuditDetails? FeederSystemAudit { get; set; }

    [JsonPropertyName("feeder_system_item_ids")]
    public IReadOnlyList<DataTypes.Basic.DvIdentifier>? FeederSystemItemIds { get; set; }

    [JsonPropertyName("original_content")]
    public DataTypes.DataValue? OriginalContent { get; set; }
}

// SPEC: Common Information Model.html#_feeder_audit_details_class (Section 3.2.6).
/// <summary>Per-system audit metadata embedded inside a feeder audit.</summary>
public sealed class FeederAuditDetails
{
    [JsonPropertyName("system_id")]
    public string SystemId { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public PartyIdentified? Location { get; set; }

    [JsonPropertyName("subject")]
    public PartyProxy? Subject { get; set; }

    [JsonPropertyName("provider")]
    public PartyIdentified? Provider { get; set; }

    [JsonPropertyName("time")]
    public DataTypes.DateTime.DvDateTime? Time { get; set; }

    [JsonPropertyName("version_id")]
    public string? VersionId { get; set; }
}
