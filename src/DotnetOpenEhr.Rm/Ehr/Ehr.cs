using System.Text.Json.Serialization;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Support;

namespace DotnetOpenEhr.Rm.Ehr;

// SPEC: EHR Information Model.html#_ehr_class (Section 3.2.1).
/// <summary>Root container of a single openEHR Electronic Health Record.</summary>
public sealed class Ehr
{
    [JsonPropertyName("system_id")]
    public HierObjectId SystemId { get; set; } = new();

    [JsonPropertyName("ehr_id")]
    public HierObjectId EhrId { get; set; } = new();

    [JsonPropertyName("time_created")]
    public DvDateTime TimeCreated { get; set; } = new();

    [JsonPropertyName("ehr_status")]
    public ObjectRef EhrStatus { get; set; } = new();

    [JsonPropertyName("ehr_access")]
    public ObjectRef EhrAccess { get; set; } = new();

    [JsonPropertyName("compositions")]
    public IList<ObjectRef>? Compositions { get; set; }

    [JsonPropertyName("directory")]
    public ObjectRef? Directory { get; set; }

    [JsonPropertyName("contributions")]
    public IList<ObjectRef>? Contributions { get; set; }
}

// SPEC: EHR Information Model.html#_ehr_status_class (Section 3.2.2).
/// <summary>Status / consent metadata of an EHR, archetypable per server policy.</summary>
public sealed class EhrStatus : Locatable
{
    [JsonPropertyName("subject")]
    public PartyProxy Subject { get; set; } = new PartyIdentified();

    [JsonPropertyName("is_queryable")]
    public bool IsQueryable { get; set; } = true;

    [JsonPropertyName("is_modifiable")]
    public bool IsModifiable { get; set; } = true;

    [JsonPropertyName("other_details")]
    public DataStructures.ItemStructure? OtherDetails { get; set; }
}

// SPEC: EHR Information Model.html#_ehr_access_class (Section 3.2.3).
/// <summary>Access-control settings of an EHR. Concrete subclass per implementation.</summary>
public sealed class EhrAccess
{
    [JsonPropertyName("settings")]
    public string? Settings { get; set; }
}

// SPEC: EHR Information Model.html#_versioned_composition_class (Section 4.2.3).
/// <summary>Versioned container around a single composition's history.</summary>
public sealed class VersionedComposition
{
    [JsonPropertyName("uid")]
    public HierObjectId Uid { get; set; } = new();

    [JsonPropertyName("owner_id")]
    public ObjectRef OwnerId { get; set; } = new();

    [JsonPropertyName("time_created")]
    public DvDateTime TimeCreated { get; set; } = new();
}
