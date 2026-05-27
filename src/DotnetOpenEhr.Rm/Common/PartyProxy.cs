using System.Text.Json.Serialization;

namespace DotnetOpenEhr.Rm.Common;

// SPEC: Common Information Model.html#_party_proxy_class (Section 4.3.1).
/// <summary>
/// Abstract proxy description of a party (subject, performer, etc),
/// optionally linked to richer demographic data via <c>external_ref</c>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(PartyIdentified), "PARTY_IDENTIFIED")]
[JsonDerivedType(typeof(PartyRelated),    "PARTY_RELATED")]
public abstract class PartyProxy
{
    [JsonPropertyName("external_ref")]
    public Support.PartyRef? ExternalRef { get; set; }
}

// SPEC: Common Information Model.html#_party_identified_class (Section 4.3.3).
/// <summary>
/// Proxy data for an identified party other than the subject of the
/// record. Carries human-readable name and/or formal identifiers and/or
/// an external reference.
/// </summary>
public class PartyIdentified : PartyProxy
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("identifiers")]
    public IReadOnlyList<DataTypes.Basic.DvIdentifier>? Identifiers { get; set; }
}

// SPEC: Common Information Model.html#_party_related_class (Section 4.3.4).
/// <summary>
/// Proxy type for identifying a party and its relationship to the subject
/// of the record.
/// </summary>
public sealed class PartyRelated : PartyIdentified
{
    [JsonPropertyName("relationship")]
    public DataTypes.Text.DvCodedText Relationship { get; set; } = new();
}

// SPEC: Common Information Model.html#_participation_class (Section 4.3.5).
/// <summary>Participation of a party in an activity.</summary>
public sealed class Participation
{
    [JsonPropertyName("function")]
    public DataTypes.Text.DvText Function { get; set; } = new();

    [JsonPropertyName("mode")]
    public DataTypes.Text.DvCodedText? Mode { get; set; }

    [JsonPropertyName("performer")]
    public PartyProxy Performer { get; set; } = new PartyIdentified();

    [JsonPropertyName("time")]
    public Foundation.Interval<DataTypes.Quantity.DvOrdered>? Time { get; set; }
}
