using System.Text.Json.Serialization;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Text;

namespace DotnetOpenEhr.Rm.Composition;

// SPEC: EHR Information Model.html#_content_item_class (Section 5.2.1).
/// <summary>Abstract base of items appearing as <see cref="Composition.Content"/>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(Section),     "SECTION")]
[JsonDerivedType(typeof(Observation), "OBSERVATION")]
[JsonDerivedType(typeof(Evaluation),  "EVALUATION")]
[JsonDerivedType(typeof(Instruction), "INSTRUCTION")]
[JsonDerivedType(typeof(Action),      "ACTION")]
[JsonDerivedType(typeof(AdminEntry),  "ADMIN_ENTRY")]
public abstract class ContentItem : Locatable
{
}

// SPEC: EHR Information Model.html#_section_class (Section 5.2.2).
/// <summary>Logical sectioning node used to organise composition content.</summary>
public sealed class Section : ContentItem
{
    [JsonPropertyName("items")]
    public IList<ContentItem>? Items { get; set; }
}

// SPEC: EHR Information Model.html#_entry_class (Section 6.2.1).
/// <summary>Abstract base for all clinical entries.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(Observation), "OBSERVATION")]
[JsonDerivedType(typeof(Evaluation),  "EVALUATION")]
[JsonDerivedType(typeof(Instruction), "INSTRUCTION")]
[JsonDerivedType(typeof(Action),      "ACTION")]
[JsonDerivedType(typeof(AdminEntry),  "ADMIN_ENTRY")]
public abstract class Entry : ContentItem
{
    [JsonPropertyName("language")]
    public CodePhrase Language { get; set; } = new();

    [JsonPropertyName("encoding")]
    public CodePhrase Encoding { get; set; } = new();

    [JsonPropertyName("subject")]
    public PartyProxy Subject { get; set; } = new PartyIdentified();

    [JsonPropertyName("provider")]
    public PartyProxy? Provider { get; set; }

    [JsonPropertyName("other_participations")]
    public IList<Participation>? OtherParticipations { get; set; }

    [JsonPropertyName("work_flow_id")]
    public Support.ObjectRef? WorkflowId { get; set; }
}

// SPEC: EHR Information Model.html#_care_entry_class (Section 6.2.2).
/// <summary>Abstract base for entries carrying clinical care information.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(Observation), "OBSERVATION")]
[JsonDerivedType(typeof(Evaluation),  "EVALUATION")]
[JsonDerivedType(typeof(Instruction), "INSTRUCTION")]
[JsonDerivedType(typeof(Action),      "ACTION")]
public abstract class CareEntry : Entry
{
    [JsonPropertyName("protocol")]
    public ItemStructure? Protocol { get; set; }

    [JsonPropertyName("guideline_id")]
    public Support.ObjectRef? GuidelineId { get; set; }
}

// SPEC: EHR Information Model.html#_observation_class (Section 6.2.3).
/// <summary>Recorded observation, carrying data and optional state histories.</summary>
public sealed class Observation : CareEntry
{
    [JsonPropertyName("data")]
    public History Data { get; set; } = new();

    [JsonPropertyName("state")]
    public History? State { get; set; }
}

// SPEC: EHR Information Model.html#_evaluation_class (Section 6.2.4).
/// <summary>Recorded clinical assessment / opinion.</summary>
public sealed class Evaluation : CareEntry
{
    [JsonPropertyName("data")]
    public ItemStructure Data { get; set; } = new ItemTree();
}

// SPEC: EHR Information Model.html#_instruction_class (Section 6.2.5).
/// <summary>Recorded instruction or order, made up of one or more activities.</summary>
public sealed class Instruction : CareEntry
{
    [JsonPropertyName("narrative")]
    public DvText Narrative { get; set; } = new();

    [JsonPropertyName("expiry_time")]
    public DvDateTime? ExpiryTime { get; set; }

    [JsonPropertyName("wf_definition")]
    public DataTypes.Encapsulated.DvParsable? WfDefinition { get; set; }

    [JsonPropertyName("activities")]
    public IList<Activity>? Activities { get; set; }
}

// SPEC: EHR Information Model.html#_action_class (Section 6.2.6).
/// <summary>Recorded ad-hoc or instruction-driven clinical action.</summary>
public sealed class Action : CareEntry
{
    [JsonPropertyName("time")]
    public DvDateTime Time { get; set; } = new();

    [JsonPropertyName("ism_transition")]
    public IsmTransition IsmTransition { get; set; } = new();

    [JsonPropertyName("instruction_details")]
    public InstructionDetails? InstructionDetails { get; set; }

    [JsonPropertyName("description")]
    public ItemStructure Description { get; set; } = new ItemTree();
}

// SPEC: EHR Information Model.html#_admin_entry_class (Section 6.2.7).
/// <summary>Administrative information entry, e.g. registration data.</summary>
public sealed class AdminEntry : Entry
{
    [JsonPropertyName("data")]
    public ItemStructure Data { get; set; } = new ItemTree();
}

// SPEC: EHR Information Model.html#_activity_class (Section 6.2.8).
/// <summary>Single activity inside an <see cref="Instruction"/>.</summary>
public sealed class Activity : Locatable
{
    [JsonPropertyName("description")]
    public ItemStructure Description { get; set; } = new ItemTree();

    [JsonPropertyName("timing")]
    public DataTypes.Encapsulated.DvParsable? Timing { get; set; }

    [JsonPropertyName("action_archetype_id")]
    public string ActionArchetypeId { get; set; } = string.Empty;
}

// SPEC: EHR Information Model.html#_instruction_details_class (Section 6.2.9).
/// <summary>Link from an <see cref="Action"/> back to the originating instruction activity.</summary>
public sealed class InstructionDetails
{
    [JsonPropertyName("instruction_id")]
    public Support.ObjectRef InstructionId { get; set; } = new();

    [JsonPropertyName("activity_id")]
    public string ActivityId { get; set; } = string.Empty;

    [JsonPropertyName("wf_details")]
    public ItemStructure? WfDetails { get; set; }
}

// SPEC: EHR Information Model.html#_ism_transition_class (Section 6.2.10).
/// <summary>Instruction State Machine transition recorded on an <see cref="Action"/>.</summary>
public sealed class IsmTransition
{
    [JsonPropertyName("current_state")]
    public DvCodedText CurrentState { get; set; } = new();

    [JsonPropertyName("transition")]
    public DvCodedText? Transition { get; set; }

    [JsonPropertyName("careflow_step")]
    public DvCodedText? CareflowStep { get; set; }

    [JsonPropertyName("reason")]
    public IList<DvText>? Reason { get; set; }
}
