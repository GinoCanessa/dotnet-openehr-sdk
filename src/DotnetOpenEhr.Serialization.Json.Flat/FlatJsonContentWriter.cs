using System.Globalization;
using System.Text.Json;
using DotnetOpenEhr.Rm;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes;
using DotnetOpenEhr.Rm.DataTypes.Basic;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Support;
using DotnetOpenEhr.Templates.Abstractions;

namespace DotnetOpenEhr.Serialization.Json.Flat;

/// <summary>
/// Schema-aware companion to <see cref="FlatJsonWriter"/>: walks the
/// archetypable content side of a <see cref="Composition"/> and emits
/// FLAT keys for every reachable leaf. Activated through
/// <see cref="OpenEhrFlatJson.Serialize(Composition, ITemplateSchema)"/>.
/// </summary>
/// <remarks>
/// Path encoding follows the same lower-cased RM-attribute convention
/// the <see cref="ITemplateSchema"/> implementation populates: each
/// collection child carries a <c>:N</c> repeat index, single-valued
/// children just append the attribute name. The schema is consulted
/// for informational consistency only; the actual concrete RM type of
/// each emitted value is read from the live object graph so the
/// writer does not require the schema to be exhaustive.
///
/// MVP scope: covers
/// SECTION / OBSERVATION / EVALUATION / ADMIN_ENTRY content items;
/// HISTORY + POINT_EVENT (event time + data); ITEM_TREE / ITEM_LIST /
/// ITEM_SINGLE; CLUSTER nesting; ELEMENT values of DV_TEXT,
/// DV_CODED_TEXT, DV_QUANTITY, DV_COUNT, DV_BOOLEAN, DV_DATE_TIME,
/// DV_DATE, DV_TIME, DV_DURATION; plus per-locatable <c>name|value</c>
/// and a <c>_archetype_node_id</c> sidecar so the parser can rebuild
/// the same RM shape.
///
/// Out-of-scope for the MVP (will be added when fixtures need them):
/// INSTRUCTION/ACTION sub-structures, ITEM_TABLE, INTERVAL_EVENT
/// extras (width/sample_count/math_function), DV_PROPORTION/DV_ORDINAL/
/// DV_SCALE/DV_IDENTIFIER, encapsulated data values, feeder audits,
/// other_participations, links.
/// </remarks>
internal static class FlatJsonContentWriter
{
    internal static void EnsureCanWrite(Composition composition, ITemplateSchema schema)
    {
        string templateId = schema.TemplateId;
        ValidateLocatableMetadata(templateId, composition, checkUid: false);
        if (composition.Context is not null)
        {
            ValidateEventContextMetadata($"{templateId}/context", composition.Context);
        }

        if (composition.Content is null || composition.Content.Count == 0)
        {
            return;
        }

        for (int i = 0; i < composition.Content.Count; i++)
        {
            ContentItem? item = composition.Content[i];
            if (item is null) continue;
            string path = $"{templateId}/content:{i.ToString(CultureInfo.InvariantCulture)}";
            ValidateContentItem(path, item);
        }
    }

    internal static void WriteContent(Utf8JsonWriter writer, Composition composition, ITemplateSchema schema)
    {
        string templateId = schema.TemplateId;
        if (composition.Content is null || composition.Content.Count == 0)
        {
            return;
        }

        for (int i = 0; i < composition.Content.Count; i++)
        {
            ContentItem? item = composition.Content[i];
            if (item is null) continue;
            string path = $"{templateId}/content:{i.ToString(CultureInfo.InvariantCulture)}";
            WriteContentItem(writer, path, item);
        }
    }

    private static void WriteContentItem(Utf8JsonWriter writer, string path, ContentItem item)
    {
        WriteLocatableHeader(writer, path, item);

        switch (item)
        {
            case Section s:
                WriteSection(writer, path, s);
                break;
            case Observation o:
                WriteEntryHeader(writer, path, o);
                WriteHistory(writer, $"{path}/data", o.Data);
                if (o.State is not null)
                {
                    WriteHistory(writer, $"{path}/state", o.State);
                }
                break;
            case Evaluation e:
                WriteEntryHeader(writer, path, e);
                WriteItemStructure(writer, $"{path}/data", e.Data);
                break;
            case AdminEntry a:
                WriteEntryHeader(writer, path, a);
                WriteItemStructure(writer, $"{path}/data", a.Data);
                break;
            default:
                ThrowUnsupported(path, item);
                break;
        }
    }

    private static void WriteSection(Utf8JsonWriter writer, string path, Section section)
    {
        if (section.Items is null || section.Items.Count == 0) return;
        for (int i = 0; i < section.Items.Count; i++)
        {
            ContentItem child = section.Items[i];
            if (child is null) continue;
            string childPath = $"{path}/items:{i.ToString(CultureInfo.InvariantCulture)}";
            WriteContentItem(writer, childPath, child);
        }
    }

    private static void WriteEntryHeader(Utf8JsonWriter writer, string path, Entry entry)
    {
        WriteCodePhrase(writer, $"{path}/language", entry.Language);
        WriteCodePhrase(writer, $"{path}/encoding", entry.Encoding);
    }

    private static void WriteHistory(Utf8JsonWriter writer, string path, History history)
    {
        WriteLocatableHeader(writer, path, history);
        WriteDvDateTime(writer, $"{path}/origin", history.Origin);
        if (history.Events is null || history.Events.Count == 0) return;
        for (int i = 0; i < history.Events.Count; i++)
        {
            Event ev = history.Events[i];
            if (ev is null) continue;
            string evPath = $"{path}/events:{i.ToString(CultureInfo.InvariantCulture)}";
            WriteEvent(writer, evPath, ev);
        }
    }

    private static void WriteEvent(Utf8JsonWriter writer, string path, Event ev)
    {
        WriteLocatableHeader(writer, path, ev);
        WriteDvDateTime(writer, $"{path}/time", ev.Time);
        WriteItemStructure(writer, $"{path}/data", ev.Data);
    }

    private static void WriteItemStructure(Utf8JsonWriter writer, string path, ItemStructure structure)
    {
        WriteLocatableHeader(writer, path, structure);
        switch (structure)
        {
            case ItemTree tree:
                WriteItemCollection(writer, path, tree.Items);
                break;
            case ItemList list:
                WriteItemCollection(writer, path, list.Items);
                break;
            case ItemSingle single:
                WriteItem(writer, $"{path}/item", single.Item);
                break;
            default:
                ThrowUnsupported(path, structure);
                break;
        }
    }

    private static void WriteItemCollection<T>(Utf8JsonWriter writer, string path, IList<T>? items)
        where T : Item
    {
        if (items is null || items.Count == 0) return;
        for (int i = 0; i < items.Count; i++)
        {
            T child = items[i];
            if (child is null) continue;
            string childPath = $"{path}/items:{i.ToString(CultureInfo.InvariantCulture)}";
            WriteItem(writer, childPath, child);
        }
    }

    private static void WriteItem(Utf8JsonWriter writer, string path, Item item)
    {
        WriteLocatableHeader(writer, path, item);
        switch (item)
        {
            case Element el:
                if (el.Value is not null)
                {
                    WriteDataValue(writer, $"{path}/value", el.Value);
                }
                if (el.NullFlavour is not null)
                {
                    WriteDvCodedText(writer, $"{path}/null_flavour", el.NullFlavour);
                }
                break;
            case Cluster cl:
                if (cl.Items is { Count: > 0 })
                {
                    for (int i = 0; i < cl.Items.Count; i++)
                    {
                        Item child = cl.Items[i];
                        if (child is null) continue;
                        string childPath = $"{path}/items:{i.ToString(CultureInfo.InvariantCulture)}";
                        WriteItem(writer, childPath, child);
                    }
                }
                break;
            default:
                ThrowUnsupported(path, item);
                break;
        }
    }

    private static void WriteDataValue(Utf8JsonWriter writer, string path, DataValue value)
    {
        switch (value)
        {
            case DvQuantity q:
                writer.WriteNumber($"{path}|magnitude", q.Magnitude);
                if (!string.IsNullOrEmpty(q.Units))
                {
                    writer.WriteString($"{path}|units", q.Units);
                }
                if (q.Precision is int prec)
                {
                    writer.WriteNumber($"{path}|precision", prec);
                }
                break;
            case DvCount c:
                writer.WriteNumber($"{path}|magnitude", c.Magnitude);
                break;
            case DvBoolean b:
                writer.WriteBoolean($"{path}|value", b.Value);
                break;
            case DvCodedText ct:
                WriteDvCodedText(writer, path, ct);
                break;
            case DvText t:
                if (!string.IsNullOrEmpty(t.Value))
                {
                    writer.WriteString($"{path}|value", t.Value);
                }
                break;
            case DvDateTime dt:
                writer.WriteString($"{path}|value", dt.Value.OriginalLexicalForm);
                break;
            case DvDate d:
                writer.WriteString($"{path}|value", d.Value.OriginalLexicalForm);
                break;
            case DvTime tm:
                writer.WriteString($"{path}|value", tm.Value.OriginalLexicalForm);
                break;
            case DvDuration dur:
                writer.WriteString($"{path}|value", dur.Value.OriginalLexicalForm);
                break;
            default:
                ThrowUnsupported(path, value);
                break;
        }
    }

    private static void ValidateContentItem(string path, ContentItem item)
    {
        ValidateLocatableMetadata(path, item, checkUid: true);
        switch (item)
        {
            case Section section:
                ValidateSection(path, section);
                break;
            case Observation observation:
                ValidateEntryMetadata(path, observation);
                ValidateHistory($"{path}/data", observation.Data);
                if (observation.State is not null)
                {
                    ValidateHistory($"{path}/state", observation.State);
                }
                break;
            case Evaluation evaluation:
                ValidateEntryMetadata(path, evaluation);
                ValidateItemStructure($"{path}/data", evaluation.Data);
                break;
            case AdminEntry adminEntry:
                ValidateEntryMetadata(path, adminEntry);
                ValidateItemStructure($"{path}/data", adminEntry.Data);
                break;
            default:
                ThrowUnsupported(path, item);
                break;
        }
    }

    private static void ValidateSection(string path, Section section)
    {
        if (section.Items is null || section.Items.Count == 0) return;
        for (int i = 0; i < section.Items.Count; i++)
        {
            ContentItem child = section.Items[i];
            if (child is null) continue;
            string childPath = $"{path}/items:{i.ToString(CultureInfo.InvariantCulture)}";
            ValidateContentItem(childPath, child);
        }
    }

    private static void ValidateHistory(string path, History history)
    {
        ValidateLocatableMetadata(path, history, checkUid: true);
        if (history.Events is null || history.Events.Count == 0) return;
        for (int i = 0; i < history.Events.Count; i++)
        {
            Event ev = history.Events[i];
            if (ev is null) continue;
            string eventPath = $"{path}/events:{i.ToString(CultureInfo.InvariantCulture)}";
            ValidateEvent(eventPath, ev);
        }
    }

    private static void ValidateEvent(string path, Event ev)
    {
        ValidateLocatableMetadata(path, ev, checkUid: true);
        ValidateItemStructure($"{path}/data", ev.Data);
    }

    private static void ValidateItemStructure(string path, ItemStructure structure)
    {
        ValidateLocatableMetadata(path, structure, checkUid: true);
        switch (structure)
        {
            case ItemTree tree:
                ValidateItemCollection(path, tree.Items);
                break;
            case ItemList list:
                ValidateItemCollection(path, list.Items);
                break;
            case ItemSingle single:
                ValidateItem($"{path}/item", single.Item);
                break;
            default:
                ThrowUnsupported(path, structure);
                break;
        }
    }

    private static void ValidateItemCollection<T>(string path, IList<T>? items)
        where T : Item
    {
        if (items is null || items.Count == 0) return;
        for (int i = 0; i < items.Count; i++)
        {
            T child = items[i];
            if (child is null) continue;
            string childPath = $"{path}/items:{i.ToString(CultureInfo.InvariantCulture)}";
            ValidateItem(childPath, child);
        }
    }

    private static void ValidateItem(string path, Item item)
    {
        ValidateLocatableMetadata(path, item, checkUid: true);
        switch (item)
        {
            case Element element:
                if (element.Value is not null)
                {
                    ValidateDataValue($"{path}/value", element.Value);
                }
                if (HasNonDefaultDvText(element.NullReason))
                {
                    ThrowUnsupportedMetadata($"{path}/null_reason", "Element.NullReason", element.NullReason!);
                }
                break;
            case Cluster cluster:
                ValidateItemCollection(path, cluster.Items);
                break;
            default:
                ThrowUnsupported(path, item);
                break;
        }
    }

    private static void ValidateDataValue(string path, DataValue value)
    {
        switch (value)
        {
            case DvQuantity:
            case DvCount:
            case DvBoolean:
            case DvCodedText:
            case DvText:
            case DvDateTime:
            case DvDate:
            case DvTime:
            case DvDuration:
                return;
            default:
                ThrowUnsupported(path, value);
                return;
        }
    }

    private static void ValidateLocatableMetadata(string path, Locatable locatable, bool checkUid)
    {
        if (checkUid && locatable.Uid is not null && HasNonDefaultObjectId(locatable.Uid))
        {
            ThrowUnsupportedMetadata($"{path}/_uid", "Locatable.Uid", locatable.Uid);
        }
        if (locatable.Links is { Count: > 0 })
        {
            ThrowUnsupportedMetadata($"{path}/links", "Locatable.Links", locatable.Links);
        }
        if (HasNonDefaultArchetyped(locatable.ArchetypeDetails))
        {
            ThrowUnsupportedMetadata($"{path}/archetype_details", "Locatable.ArchetypeDetails", locatable.ArchetypeDetails!);
        }
        if (HasNonDefaultFeederAudit(locatable.FeederAudit))
        {
            ThrowUnsupportedMetadata($"{path}/feeder_audit", "Locatable.FeederAudit", locatable.FeederAudit!);
        }
    }

    private static void ValidateEventContextMetadata(string path, EventContext context)
    {
        if (HasNonDefaultItemStructure(context.OtherContext))
        {
            ThrowUnsupportedMetadata($"{path}/other_context", "EventContext.OtherContext", context.OtherContext!);
        }
        if (context.Participations is { Count: > 0 })
        {
            ThrowUnsupportedMetadata($"{path}/participations", "EventContext.Participations", context.Participations);
        }
    }

    private static void ValidateEntryMetadata(string path, Entry entry)
    {
        if (HasNonDefaultPartyProxy(entry.Provider))
        {
            ThrowUnsupportedMetadata($"{path}/provider", "Entry.Provider", entry.Provider!);
        }
        if (entry.OtherParticipations is { Count: > 0 })
        {
            ThrowUnsupportedMetadata($"{path}/other_participations", "Entry.OtherParticipations", entry.OtherParticipations);
        }
        if (HasNonDefaultObjectRef(entry.WorkflowId))
        {
            ThrowUnsupportedMetadata($"{path}/workflow_id", "Entry.WorkflowId", entry.WorkflowId!);
        }

        if (entry is CareEntry careEntry)
        {
            if (HasNonDefaultItemStructure(careEntry.Protocol))
            {
                ThrowUnsupportedMetadata($"{path}/protocol", "CareEntry.Protocol", careEntry.Protocol!);
            }
            if (HasNonDefaultObjectRef(careEntry.GuidelineId))
            {
                ThrowUnsupportedMetadata($"{path}/guideline_id", "CareEntry.GuidelineId", careEntry.GuidelineId!);
            }
        }
    }

    private static bool HasNonDefaultItemStructure(ItemStructure? structure)
    {
        if (structure is null) return false;
        if (HasNonDefaultLocatableShape(structure)) return true;
        return structure switch
        {
            ItemTree tree => tree.Items is { Count: > 0 },
            ItemList list => list.Items is { Count: > 0 },
            ItemSingle single => HasNonDefaultItem(single.Item),
            ItemTable table => table.Rows is { Count: > 0 },
            _ => true,
        };
    }

    private static bool HasNonDefaultItem(Item? item)
    {
        if (item is null) return false;
        if (HasNonDefaultLocatableShape(item)) return true;
        return item switch
        {
            Element element => HasNonDefaultDataValue(element.Value)
                || HasNonDefaultDvText(element.NullFlavour)
                || HasNonDefaultDvText(element.NullReason),
            Cluster cluster => cluster.Items is { Count: > 0 },
            _ => true,
        };
    }

    private static bool HasNonDefaultLocatableShape(Locatable locatable)
        => HasNonDefaultDvText(locatable.Name)
            || !string.IsNullOrEmpty(locatable.ArchetypeNodeId)
            || HasNonDefaultObjectId(locatable.Uid)
            || locatable.Links is { Count: > 0 }
            || HasNonDefaultArchetyped(locatable.ArchetypeDetails)
            || HasNonDefaultFeederAudit(locatable.FeederAudit);

    private static bool HasNonDefaultDataValue(DataValue? value)
    {
        if (value is null) return false;
        return value switch
        {
            DvQuantity quantity => quantity.Magnitude != 0d
                || !string.IsNullOrEmpty(quantity.Units)
                || quantity.Precision is not null,
            DvCount count => count.Magnitude != 0,
            DvBoolean boolean => boolean.Value,
            DvCodedText codedText => HasNonDefaultDvText(codedText),
            DvText text => HasNonDefaultDvText(text),
            DvDateTime dateTime => !string.Equals(dateTime.Value.OriginalLexicalForm, "0001-01-01T00", StringComparison.Ordinal),
            DvDate date => !string.Equals(date.Value.OriginalLexicalForm, "0001-01-01", StringComparison.Ordinal),
            DvTime time => !string.Equals(time.Value.OriginalLexicalForm, "00", StringComparison.Ordinal),
            DvDuration duration => !string.Equals(duration.Value.OriginalLexicalForm, "P0D", StringComparison.Ordinal),
            DvProportion proportion => proportion.Numerator != 0d
                || proportion.Denominator != 0d
                || proportion.Type != 0
                || proportion.Precision is not null,
            DvOrdinal ordinal => ordinal.Value != 0 || HasNonDefaultDvText(ordinal.Symbol),
            DvScale scale => scale.Value != 0d || HasNonDefaultDvText(scale.Symbol),
            DvIdentifier identifier => HasNonDefaultIdentifier(identifier),
            DvState state => state.IsTerminal || HasNonDefaultDvText(state.Value),
            _ => true,
        };
    }

    private static bool HasNonDefaultDvText(DvText? text)
    {
        if (text is null) return false;
        return !string.IsNullOrEmpty(text.Value)
            || text.Hyperlink is not null && !string.IsNullOrEmpty(text.Hyperlink.Value)
            || !string.IsNullOrEmpty(text.Formatting)
            || text.Mappings is { Count: > 0 }
            || HasNonDefaultCodePhrase(text.Language)
            || HasNonDefaultCodePhrase(text.Encoding)
            || text is DvCodedText codedText && HasNonDefaultCodePhrase(codedText.DefiningCode);
    }

    private static bool HasNonDefaultCodePhrase(CodePhrase? codePhrase)
        => codePhrase is not null
            && (!string.IsNullOrEmpty(codePhrase.TerminologyId.Value)
                || !string.IsNullOrEmpty(codePhrase.CodeString)
                || !string.IsNullOrEmpty(codePhrase.PreferredTerm));

    private static bool HasNonDefaultIdentifier(DvIdentifier? identifier)
        => identifier is not null
            && (!string.IsNullOrEmpty(identifier.Id)
                || !string.IsNullOrEmpty(identifier.Issuer)
                || !string.IsNullOrEmpty(identifier.Assigner)
                || !string.IsNullOrEmpty(identifier.Type));

    private static bool HasNonDefaultPartyProxy(PartyProxy? party)
    {
        if (party is null) return false;
        if (HasNonDefaultObjectRef(party.ExternalRef)) return true;
        return party switch
        {
            PartyRelated related => !string.IsNullOrEmpty(related.Name)
                || related.Identifiers is { Count: > 0 }
                || HasNonDefaultDvText(related.Relationship),
            PartyIdentified identified => !string.IsNullOrEmpty(identified.Name)
                || identified.Identifiers is { Count: > 0 },
            PartySelf => false,
            _ => true,
        };
    }

    private static bool HasNonDefaultArchetyped(Archetyped? archetyped)
        => archetyped is not null
            && (HasNonDefaultObjectId(archetyped.ArchetypeId)
                || HasNonDefaultObjectId(archetyped.TemplateId)
                || !string.IsNullOrEmpty(archetyped.RmVersion));

    private static bool HasNonDefaultFeederAudit(FeederAudit? feederAudit)
        => feederAudit is not null
            && (HasNonDefaultFeederAuditDetails(feederAudit.OriginatingSystemAudit)
                || feederAudit.OriginatingSystemItemIds is { Count: > 0 }
                || HasNonDefaultFeederAuditDetails(feederAudit.FeederSystemAudit)
                || feederAudit.FeederSystemItemIds is { Count: > 0 }
                || HasNonDefaultDataValue(feederAudit.OriginalContent));

    private static bool HasNonDefaultFeederAuditDetails(FeederAuditDetails? details)
        => details is not null
            && (!string.IsNullOrEmpty(details.SystemId)
                || HasNonDefaultPartyProxy(details.Location)
                || HasNonDefaultPartyProxy(details.Subject)
                || HasNonDefaultPartyProxy(details.Provider)
                || HasNonDefaultDataValue(details.Time)
                || !string.IsNullOrEmpty(details.VersionId));

    private static bool HasNonDefaultObjectRef(ObjectRef? objectRef)
        => objectRef is not null
            && (!string.IsNullOrEmpty(objectRef.Namespace)
                || !string.IsNullOrEmpty(objectRef.Type)
                || HasNonDefaultObjectId(objectRef.Id));

    private static bool HasNonDefaultObjectId(ObjectId? objectId)
        => objectId is not null
            && (!string.IsNullOrEmpty(objectId.Value)
                || objectId is GenericId genericId && !string.IsNullOrEmpty(genericId.Scheme));

    private static void ThrowUnsupported(string path, object value)
    {
        Type systemType = value.GetType();
        string rmType = RmTypeName.TryGet(systemType, out string? resolved)
            ? resolved
            : systemType.Name;
        throw new NotSupportedException(
            $"Schema-driven FLAT serialization cannot write path '{path}' because RM type '{rmType}' is not supported (system type '{systemType.FullName}').");
    }

    private static void ThrowUnsupportedMetadata(string path, string propertyName, object value)
    {
        Type systemType = value.GetType();
        string rmType = RmTypeName.TryGet(systemType, out string? resolved)
            ? resolved
            : systemType.Name;
        throw new NotSupportedException(
            $"Schema-driven FLAT serialization cannot write unsupported metadata '{propertyName}' at path '{path}' (RM type '{rmType}', system type '{systemType.FullName}').");
    }

    private static void WriteDvCodedText(Utf8JsonWriter writer, string path, DvCodedText value)
    {
        if (!string.IsNullOrEmpty(value.DefiningCode.CodeString))
        {
            writer.WriteString($"{path}|code", value.DefiningCode.CodeString);
        }
        if (!string.IsNullOrEmpty(value.Value))
        {
            writer.WriteString($"{path}|value", value.Value);
        }
        if (!string.IsNullOrEmpty(value.DefiningCode.TerminologyId.Value))
        {
            writer.WriteString($"{path}|terminology", value.DefiningCode.TerminologyId.Value);
        }
    }

    private static void WriteCodePhrase(Utf8JsonWriter writer, string path, CodePhrase value)
    {
        if (!string.IsNullOrEmpty(value.TerminologyId.Value))
        {
            writer.WriteString($"{path}|terminology", value.TerminologyId.Value);
        }
        if (!string.IsNullOrEmpty(value.CodeString))
        {
            writer.WriteString($"{path}|code", value.CodeString);
        }
    }

    private static void WriteDvDateTime(Utf8JsonWriter writer, string key, DvDateTime value)
    {
        writer.WriteString(key, value.Value.OriginalLexicalForm);
    }

    /// <summary>
    /// Emits the two locatable-shape sidecars used by the parser to
    /// restore the RM-level name and archetype_node_id on each node:
    /// <c>{path}/name|value</c> and <c>{path}/_archetype_node_id</c>.
    /// </summary>
    private static void WriteLocatableHeader(Utf8JsonWriter writer, string path, Locatable locatable)
    {
        if (locatable.Name is not null && !string.IsNullOrEmpty(locatable.Name.Value))
        {
            writer.WriteString($"{path}/name|value", locatable.Name.Value);
        }
        if (!string.IsNullOrEmpty(locatable.ArchetypeNodeId))
        {
            writer.WriteString($"{path}/_archetype_node_id", locatable.ArchetypeNodeId);
        }
    }
}
