using System.Text.Json.Serialization;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.Basic;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Foundation;

namespace DotnetOpenEhr.Rm.Demographic;

// SPEC: Demographic Information Model.html#_party_class (Section 4.2.1).
/// <summary>Abstract demographic party (real-world person, organisation, etc.).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(Person),       "PERSON")]
[JsonDerivedType(typeof(Organisation), "ORGANISATION")]
[JsonDerivedType(typeof(Group),        "GROUP")]
[JsonDerivedType(typeof(Agent),        "AGENT")]
[JsonDerivedType(typeof(Role),         "ROLE")]
public abstract class Party : Locatable
{
    [JsonPropertyName("identities")]
    public IList<PartyIdentity> Identities { get; set; } = [];

    [JsonPropertyName("contacts")]
    public IList<Contact>? Contacts { get; set; }

    [JsonPropertyName("details")]
    public ItemStructure? Details { get; set; }

    [JsonPropertyName("reverse_relationships")]
    public IList<Support.ObjectRef>? ReverseRelationships { get; set; }
}

// SPEC: Demographic Information Model.html#_actor_class (Section 4.2.2).
/// <summary>Abstract acting demographic party: PERSON, ORGANISATION, GROUP, AGENT.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(Person),       "PERSON")]
[JsonDerivedType(typeof(Organisation), "ORGANISATION")]
[JsonDerivedType(typeof(Group),        "GROUP")]
[JsonDerivedType(typeof(Agent),        "AGENT")]
public abstract class Actor : Party
{
    [JsonPropertyName("roles")]
    public IList<Support.PartyRef>? Roles { get; set; }

    [JsonPropertyName("languages")]
    public IList<DvText>? Languages { get; set; }
}

// SPEC: Demographic Information Model.html#_person_class.
/// <summary>A real-world person.</summary>
public sealed class Person : Actor { }

// SPEC: Demographic Information Model.html#_organisation_class.
/// <summary>A legal or commercial organisation.</summary>
public sealed class Organisation : Actor { }

// SPEC: Demographic Information Model.html#_group_class.
/// <summary>A non-legal group of people.</summary>
public sealed class Group : Actor { }

// SPEC: Demographic Information Model.html#_agent_class.
/// <summary>A non-human actor (software agent, robot, device).</summary>
public sealed class Agent : Actor { }

// SPEC: Demographic Information Model.html#_role_class (Section 4.2.7).
/// <summary>A role played by an <see cref="Actor"/>.</summary>
public sealed class Role : Party
{
    [JsonPropertyName("time_validity")]
    public Interval<DataTypes.DateTime.DvDateTime>? TimeValidity { get; set; }

    [JsonPropertyName("performer")]
    public Support.PartyRef Performer { get; set; } = new();

    [JsonPropertyName("capabilities")]
    public IList<Capability>? Capabilities { get; set; }
}

// SPEC: Demographic Information Model.html#_address_class.
/// <summary>Structured address detail held on a <see cref="Contact"/>.</summary>
public sealed class Address : Locatable
{
    [JsonPropertyName("details")]
    public ItemStructure Details { get; set; } = new ItemTree();
}

// SPEC: Demographic Information Model.html#_contact_class.
/// <summary>Contact-channel data attached to a <see cref="Party"/>.</summary>
public sealed class Contact : Locatable
{
    [JsonPropertyName("time_validity")]
    public Interval<DataTypes.DateTime.DvDateTime>? TimeValidity { get; set; }

    [JsonPropertyName("addresses")]
    public IList<Address> Addresses { get; set; } = [];
}

// SPEC: Demographic Information Model.html#_party_identity_class.
/// <summary>Identity claim of a <see cref="Party"/> (e.g. legal name).</summary>
public sealed class PartyIdentity : Locatable
{
    [JsonPropertyName("details")]
    public ItemStructure Details { get; set; } = new ItemTree();
}

// SPEC: Demographic Information Model.html#_capability_class.
/// <summary>Capability granted by a <see cref="Role"/>.</summary>
public sealed class Capability : Locatable
{
    [JsonPropertyName("time_validity")]
    public Interval<DataTypes.DateTime.DvDateTime>? TimeValidity { get; set; }

    [JsonPropertyName("credentials")]
    public ItemStructure Credentials { get; set; } = new ItemTree();
}
