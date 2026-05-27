using System.Text.Json.Serialization;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes;
using DotnetOpenEhr.Rm.DataTypes.Basic;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Encapsulated;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.DataTypes.Uri;
using DotnetOpenEhr.Rm.Demographic;
using DotnetOpenEhr.Rm.Ehr;
using DotnetOpenEhr.Rm.Support;

namespace DotnetOpenEhr.Serialization.Json;

/// <summary>
/// AOT-safe <see cref="JsonSerializerContext"/> covering the public
/// openEHR Reference Model surface. Every polymorphic base is listed so
/// the source generator emits the metadata table needed for
/// discriminator-based <c>_type</c> resolution at runtime without
/// reflection. Property-name policy is <c>snake_case_lower</c>;
/// <c>null</c> properties are omitted on write to keep canonical JSON
/// clean.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    AllowOutOfOrderMetadataProperties = true,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(Composition))]
[JsonSerializable(typeof(EventContext))]
[JsonSerializable(typeof(Ehr))]
[JsonSerializable(typeof(EhrStatus))]
[JsonSerializable(typeof(EhrAccess))]
[JsonSerializable(typeof(VersionedComposition))]
[JsonSerializable(typeof(OriginalVersion))]
[JsonSerializable(typeof(AuditDetails))]
[JsonSerializable(typeof(Attestation))]
// Polymorphic bases — listing the base ensures STJ emits the
// discriminator metadata for all [JsonDerivedType] subtypes.
[JsonSerializable(typeof(Locatable))]
[JsonSerializable(typeof(DataValue))]
[JsonSerializable(typeof(DvOrdered))]
[JsonSerializable(typeof(DvQuantified))]
[JsonSerializable(typeof(DvAmount))]
[JsonSerializable(typeof(DvAbsoluteQuantity))]
[JsonSerializable(typeof(DvTemporal))]
[JsonSerializable(typeof(DvEncapsulated))]
[JsonSerializable(typeof(ContentItem))]
[JsonSerializable(typeof(Entry))]
[JsonSerializable(typeof(CareEntry))]
[JsonSerializable(typeof(DataStructure))]
[JsonSerializable(typeof(ItemStructure))]
[JsonSerializable(typeof(Event))]
[JsonSerializable(typeof(Item))]
[JsonSerializable(typeof(PartyProxy))]
[JsonSerializable(typeof(ObjectId))]
[JsonSerializable(typeof(UidBasedId))]
[JsonSerializable(typeof(ObjectRef))]
[JsonSerializable(typeof(Party))]
[JsonSerializable(typeof(Actor))]
// Concrete leaf roots that may also appear at the top of a payload.
[JsonSerializable(typeof(Section))]
[JsonSerializable(typeof(Observation))]
[JsonSerializable(typeof(Evaluation))]
[JsonSerializable(typeof(Instruction))]
[JsonSerializable(typeof(DotnetOpenEhr.Rm.Composition.Action))]
[JsonSerializable(typeof(AdminEntry))]
[JsonSerializable(typeof(Activity))]
[JsonSerializable(typeof(InstructionDetails))]
[JsonSerializable(typeof(IsmTransition))]
[JsonSerializable(typeof(History))]
[JsonSerializable(typeof(PointEvent))]
[JsonSerializable(typeof(IntervalEvent))]
[JsonSerializable(typeof(ItemTree))]
[JsonSerializable(typeof(ItemList))]
[JsonSerializable(typeof(ItemSingle))]
[JsonSerializable(typeof(ItemTable))]
[JsonSerializable(typeof(Cluster))]
[JsonSerializable(typeof(Element))]
[JsonSerializable(typeof(DvText))]
[JsonSerializable(typeof(DvCodedText))]
[JsonSerializable(typeof(CodePhrase))]
[JsonSerializable(typeof(TermMapping))]
[JsonSerializable(typeof(DvQuantity))]
[JsonSerializable(typeof(DvCount))]
[JsonSerializable(typeof(DvProportion))]
[JsonSerializable(typeof(DvOrdinal))]
[JsonSerializable(typeof(DvScale))]
[JsonSerializable(typeof(ReferenceRange))]
[JsonSerializable(typeof(DvDate))]
[JsonSerializable(typeof(DvTime))]
[JsonSerializable(typeof(DvDateTime))]
[JsonSerializable(typeof(DvDuration))]
[JsonSerializable(typeof(DvMultimedia))]
[JsonSerializable(typeof(DvParsable))]
[JsonSerializable(typeof(DvUri))]
[JsonSerializable(typeof(DvEhrUri))]
[JsonSerializable(typeof(DvBoolean))]
[JsonSerializable(typeof(DvIdentifier))]
[JsonSerializable(typeof(DvState))]
[JsonSerializable(typeof(HierObjectId))]
[JsonSerializable(typeof(ObjectVersionId))]
[JsonSerializable(typeof(ArchetypeId))]
[JsonSerializable(typeof(TemplateId))]
[JsonSerializable(typeof(TerminologyId))]
[JsonSerializable(typeof(GenericId))]
[JsonSerializable(typeof(PartyRef))]
[JsonSerializable(typeof(PartyIdentified))]
[JsonSerializable(typeof(PartyRelated))]
[JsonSerializable(typeof(PartySelf))]
[JsonSerializable(typeof(Participation))]
[JsonSerializable(typeof(Link))]
[JsonSerializable(typeof(Archetyped))]
[JsonSerializable(typeof(FeederAudit))]
[JsonSerializable(typeof(FeederAuditDetails))]
[JsonSerializable(typeof(Person))]
[JsonSerializable(typeof(Organisation))]
[JsonSerializable(typeof(Group))]
[JsonSerializable(typeof(Agent))]
[JsonSerializable(typeof(Role))]
[JsonSerializable(typeof(Address))]
[JsonSerializable(typeof(Contact))]
[JsonSerializable(typeof(PartyIdentity))]
[JsonSerializable(typeof(Capability))]
public sealed partial class OpenEhrJsonContext : JsonSerializerContext
{
}
