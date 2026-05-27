using System.Text.Json.Serialization;

namespace DotnetOpenEhr.Rm.Support;

// SPEC: Support Information Model.html#_identifiers (Section 5.3) and
// canonical openEHR BASE Identification package. The local "Support
// Information Model.html" describes terminology services; the
// identifier classes themselves are referenced from there and Common IM.
/// <summary>
/// Abstract ancestor of every openEHR identifier object. Carries the
/// canonical identifier <c>value</c> string.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(HierObjectId),    "HIER_OBJECT_ID")]
[JsonDerivedType(typeof(ObjectVersionId), "OBJECT_VERSION_ID")]
[JsonDerivedType(typeof(ArchetypeId),     "ARCHETYPE_ID")]
[JsonDerivedType(typeof(TemplateId),      "TEMPLATE_ID")]
[JsonDerivedType(typeof(TerminologyId),   "TERMINOLOGY_ID")]
public abstract class ObjectId
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    public override string ToString() => Value;
}

/// <summary>
/// Abstract intermediate between <see cref="ObjectId"/> and
/// <see cref="HierObjectId"/> / <see cref="ObjectVersionId"/>.
/// Added by spec ticket SPEC-239.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(HierObjectId),    "HIER_OBJECT_ID")]
[JsonDerivedType(typeof(ObjectVersionId), "OBJECT_VERSION_ID")]
public abstract class UidBasedId : ObjectId
{
}

// SPEC: Support Information Model.html#_identifiers (HIER_OBJECT_ID).
/// <summary>
/// Hierarchical object identifier: <c>{root}::{extension}</c> form.
/// </summary>
public sealed class HierObjectId : UidBasedId
{
}

// SPEC: Support Information Model.html#_identifiers (OBJECT_VERSION_ID).
/// <summary>
/// Globally unique version identifier:
/// <c>{object_id}::{creating_system_id}::{version_tree_id}</c>.
/// </summary>
public sealed class ObjectVersionId : UidBasedId
{
}

// SPEC: Support Information Model.html#_identifiers (ARCHETYPE_ID).
/// <summary>Canonical, parseable openEHR archetype identifier.</summary>
public sealed class ArchetypeId : ObjectId
{
}

// SPEC: Support Information Model.html#_identifiers (TEMPLATE_ID).
/// <summary>Canonical openEHR template identifier.</summary>
public sealed class TemplateId : ObjectId
{
}

// SPEC: Support Information Model.html#_identifiers (TERMINOLOGY_ID).
/// <summary>Identifier of a terminology, e.g. <c>SNOMED-CT</c>.</summary>
public sealed class TerminologyId : ObjectId
{
}

// SPEC: Support Information Model.html#_identifiers (OBJECT_REF).
/// <summary>
/// Reference to an object inside an openEHR or other namespace, by
/// <c>id_namespace</c> + <c>type</c> + <c>id</c>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(PartyRef), "PARTY_REF")]
public class ObjectRef
{
    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public ObjectId Id { get; set; } = new HierObjectId();
}

// SPEC: Support Information Model.html#_identifiers (PARTY_REF).
/// <summary>
/// Object reference whose <c>type</c> is constrained to a demographic
/// party type (PERSON, ORGANISATION, GROUP, AGENT, ROLE, ACTOR or PARTY).
/// </summary>
public sealed class PartyRef : ObjectRef
{
}
