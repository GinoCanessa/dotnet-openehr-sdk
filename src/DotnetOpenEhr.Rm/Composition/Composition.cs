using System.Text.Json.Serialization;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Text;

namespace DotnetOpenEhr.Rm.Composition;

// SPEC: EHR Information Model.html#_composition_class (Section 4.2.1).
/// <summary>Root archetypable container for a recorded openEHR clinical event.</summary>
public sealed class Composition : Locatable
{
    [JsonPropertyName("language")]
    public CodePhrase Language { get; set; } = new();

    [JsonPropertyName("territory")]
    public CodePhrase Territory { get; set; } = new();

    [JsonPropertyName("category")]
    public DvCodedText Category { get; set; } = new();

    [JsonPropertyName("composer")]
    public PartyProxy? Composer { get; set; }

    [JsonPropertyName("context")]
    public EventContext? Context { get; set; }

    [JsonPropertyName("content")]
    public IList<ContentItem>? Content { get; set; }
}

// SPEC: EHR Information Model.html#_event_context_class (Section 4.2.2).
/// <summary>Documentation of the clinical session in which a composition was authored.</summary>
public sealed class EventContext
{
    [JsonPropertyName("start_time")]
    public DvDateTime StartTime { get; set; } = new();

    [JsonPropertyName("end_time")]
    public DvDateTime? EndTime { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("setting")]
    public DvCodedText Setting { get; set; } = new();

    [JsonPropertyName("other_context")]
    public ItemStructure? OtherContext { get; set; }

    [JsonPropertyName("health_care_facility")]
    public PartyIdentified? HealthCareFacility { get; set; }

    [JsonPropertyName("participations")]
    public IList<Participation>? Participations { get; set; }
}
