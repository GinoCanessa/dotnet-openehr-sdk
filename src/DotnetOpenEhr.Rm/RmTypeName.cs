using System.Diagnostics.CodeAnalysis;

namespace DotnetOpenEhr.Rm;

/// <summary>
/// Registry mapping openEHR canonical <c>RM_TYPE_NAME</c> (UPPER_SNAKE)
/// strings to the C# concrete classes that implement them, and vice-versa.
/// Populated eagerly from a hand-maintained table — no reflection at
/// runtime, no source generators.
/// </summary>
public static class RmTypeName
{
    private static readonly Dictionary<string, Type> SystemTypeByRmNameStore;
    private static readonly Dictionary<Type, string> RmNameBySystemTypeStore;

    static RmTypeName()
    {
        (string RmName, Type SystemType)[] entries =
        [
            // Data Types — Text
            ("DV_TEXT",                typeof(DataTypes.Text.DvText)),
            ("DV_CODED_TEXT",          typeof(DataTypes.Text.DvCodedText)),
            ("CODE_PHRASE",            typeof(DataTypes.Text.CodePhrase)),
            ("TERM_MAPPING",           typeof(DataTypes.Text.TermMapping)),

            // Data Types — Quantity
            ("DV_QUANTITY",            typeof(DataTypes.Quantity.DvQuantity)),
            ("DV_COUNT",               typeof(DataTypes.Quantity.DvCount)),
            ("DV_PROPORTION",          typeof(DataTypes.Quantity.DvProportion)),
            ("DV_ORDINAL",             typeof(DataTypes.Quantity.DvOrdinal)),
            ("DV_SCALE",               typeof(DataTypes.Quantity.DvScale)),
            ("REFERENCE_RANGE",        typeof(DataTypes.Quantity.ReferenceRange)),

            // Data Types — Date/Time
            ("DV_DATE",                typeof(DataTypes.DateTime.DvDate)),
            ("DV_TIME",                typeof(DataTypes.DateTime.DvTime)),
            ("DV_DATE_TIME",           typeof(DataTypes.DateTime.DvDateTime)),
            ("DV_DURATION",            typeof(DataTypes.DateTime.DvDuration)),

            // Data Types — Encapsulated
            ("DV_MULTIMEDIA",          typeof(DataTypes.Encapsulated.DvMultimedia)),
            ("DV_PARSABLE",            typeof(DataTypes.Encapsulated.DvParsable)),

            // Data Types — Uri
            ("DV_URI",                 typeof(DataTypes.Uri.DvUri)),
            ("DV_EHR_URI",             typeof(DataTypes.Uri.DvEhrUri)),

            // Data Types — Basic
            ("DV_BOOLEAN",             typeof(DataTypes.Basic.DvBoolean)),
            ("DV_IDENTIFIER",          typeof(DataTypes.Basic.DvIdentifier)),
            ("DV_STATE",               typeof(DataTypes.Basic.DvState)),

            // Data Structures
            ("CLUSTER",                typeof(DataStructures.Cluster)),
            ("ELEMENT",                typeof(DataStructures.Element)),
            ("ITEM_TREE",              typeof(DataStructures.ItemTree)),
            ("ITEM_LIST",              typeof(DataStructures.ItemList)),
            ("ITEM_SINGLE",            typeof(DataStructures.ItemSingle)),
            ("ITEM_TABLE",             typeof(DataStructures.ItemTable)),
            ("HISTORY",                typeof(DataStructures.History)),
            ("POINT_EVENT",            typeof(DataStructures.PointEvent)),
            ("INTERVAL_EVENT",         typeof(DataStructures.IntervalEvent)),

            // Composition
            ("COMPOSITION",            typeof(Composition.Composition)),
            ("EVENT_CONTEXT",          typeof(Composition.EventContext)),
            ("SECTION",                typeof(Composition.Section)),
            ("OBSERVATION",            typeof(Composition.Observation)),
            ("EVALUATION",             typeof(Composition.Evaluation)),
            ("INSTRUCTION",            typeof(Composition.Instruction)),
            ("ACTION",                 typeof(Composition.Action)),
            ("ADMIN_ENTRY",            typeof(Composition.AdminEntry)),
            ("ACTIVITY",               typeof(Composition.Activity)),
            ("INSTRUCTION_DETAILS",    typeof(Composition.InstructionDetails)),
            ("ISM_TRANSITION",         typeof(Composition.IsmTransition)),

            // EHR
            ("EHR",                    typeof(Ehr.Ehr)),
            ("EHR_STATUS",             typeof(Ehr.EhrStatus)),
            ("EHR_ACCESS",             typeof(Ehr.EhrAccess)),
            ("VERSIONED_COMPOSITION",  typeof(Ehr.VersionedComposition)),

            // Demographic
            ("PERSON",                 typeof(Demographic.Person)),
            ("ORGANISATION",           typeof(Demographic.Organisation)),
            ("GROUP",                  typeof(Demographic.Group)),
            ("AGENT",                  typeof(Demographic.Agent)),
            ("ROLE",                   typeof(Demographic.Role)),
            ("ADDRESS",                typeof(Demographic.Address)),
            ("CONTACT",                typeof(Demographic.Contact)),
            ("PARTY_IDENTITY",         typeof(Demographic.PartyIdentity)),
            ("CAPABILITY",             typeof(Demographic.Capability)),

            // Common
            ("LINK",                   typeof(Common.Link)),
            ("ARCHETYPED",             typeof(Common.Archetyped)),
            ("FEEDER_AUDIT",           typeof(Common.FeederAudit)),
            ("FEEDER_AUDIT_DETAILS",   typeof(Common.FeederAuditDetails)),
            ("PARTY_IDENTIFIED",       typeof(Common.PartyIdentified)),
            ("PARTY_RELATED",          typeof(Common.PartyRelated)),
            ("PARTICIPATION",          typeof(Common.Participation)),
            ("AUDIT_DETAILS",          typeof(Common.AuditDetails)),
            ("ATTESTATION",            typeof(Common.Attestation)),
            ("ORIGINAL_VERSION",       typeof(Common.OriginalVersion)),

            // Identification (Support package — SPEC: support is split across
            // Common IM cross-references + canonical openEHR BASE Identification;
            // the local "Support Information Model.html" covers terminology
            // services only)
            ("HIER_OBJECT_ID",         typeof(Support.HierObjectId)),
            ("OBJECT_VERSION_ID",      typeof(Support.ObjectVersionId)),
            ("ARCHETYPE_ID",           typeof(Support.ArchetypeId)),
            ("TEMPLATE_ID",            typeof(Support.TemplateId)),
            ("TERMINOLOGY_ID",         typeof(Support.TerminologyId)),
            ("OBJECT_REF",             typeof(Support.ObjectRef)),
            ("PARTY_REF",              typeof(Support.PartyRef)),
        ];

        SystemTypeByRmNameStore = new Dictionary<string, Type>(entries.Length, StringComparer.Ordinal);
        RmNameBySystemTypeStore = new Dictionary<Type, string>(entries.Length);

        foreach ((string rmName, Type systemType) in entries)
        {
            SystemTypeByRmNameStore.Add(rmName, systemType);
            RmNameBySystemTypeStore.Add(systemType, rmName);
        }
    }

    /// <summary>All canonical openEHR <c>RM_TYPE_NAME</c> strings known to the SDK.</summary>
    public static IReadOnlyCollection<string> AllRmNames => SystemTypeByRmNameStore.Keys;

    /// <summary>All C# concrete types in <c>DotnetOpenEhr.Rm</c> that map to a canonical RM name.</summary>
    public static IReadOnlyCollection<Type> AllSystemTypes => RmNameBySystemTypeStore.Keys;

    /// <summary>
    /// Resolve a canonical openEHR <c>RM_TYPE_NAME</c> (e.g. <c>"DV_QUANTITY"</c>)
    /// to the concrete C# type that implements it.
    /// </summary>
    public static bool TryGet(string rmName, [NotNullWhen(true)] out Type? type)
        => SystemTypeByRmNameStore.TryGetValue(rmName, out type);

    /// <summary>
    /// Resolve a C# concrete type defined in <c>DotnetOpenEhr.Rm</c> to its
    /// canonical openEHR <c>RM_TYPE_NAME</c>.
    /// </summary>
    public static bool TryGet(Type type, [NotNullWhen(true)] out string? rmName)
        => RmNameBySystemTypeStore.TryGetValue(type, out rmName);
}
