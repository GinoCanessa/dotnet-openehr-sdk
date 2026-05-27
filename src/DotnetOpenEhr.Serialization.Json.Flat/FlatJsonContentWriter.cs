using System.Globalization;
using System.Text.Json;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes;
using DotnetOpenEhr.Rm.DataTypes.Basic;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
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
/// MVP scope (Phase 8d subset, documented in plan deviation): covers
/// SECTION / OBSERVATION / EVALUATION / ADMIN_ENTRY content items;
/// HISTORY + POINT_EVENT (event time + data); ITEM_TREE / ITEM_LIST /
/// ITEM_SINGLE; CLUSTER nesting; ELEMENT values of DV_TEXT,
/// DV_CODED_TEXT, DV_QUANTITY, DV_COUNT, DV_BOOLEAN, DV_DATE_TIME,
/// DV_DATE, DV_TIME, DV_DURATION; plus per-locatable <c>name|value</c>
/// and a <c>_archetype_node_id</c> sidecar so the parser can rebuild
/// the same RM shape.
///
/// Out-of-scope for Phase 8d (will be added when fixtures need them):
/// INSTRUCTION/ACTION sub-structures, ITEM_TABLE, INTERVAL_EVENT
/// extras (width/sample_count/math_function), DV_PROPORTION/DV_ORDINAL/
/// DV_SCALE/DV_IDENTIFIER, encapsulated data values, feeder audits,
/// other_participations, links.
/// </remarks>
internal static class FlatJsonContentWriter
{
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
            // Instruction / Action are intentionally out-of-scope for
            // the Phase 8d MVP; the visible tree is still emitted via
            // the locatable header so partial round-trip remains
            // possible if a future phase fills in the gap.
            default:
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
            // ItemTable is out-of-scope for Phase 8d MVP.
            default:
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
                // Out-of-scope value types are silently dropped at MVP
                // scope. Listed in the class-level remark for visibility.
                break;
        }
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
