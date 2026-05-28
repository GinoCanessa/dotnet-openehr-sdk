using System.Text.Json;
using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataTypes.Basic;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Support;
using DotnetOpenEhr.Templates.Abstractions;

namespace DotnetOpenEhr.Serialization.Json.Flat;

/// <summary>
/// Top-level façade for the FLAT openEHR JSON dialect. Provides
/// schemaless and schema-driven Composition parsing, plus FLAT
/// serialisation that mirrors the canonical
/// <c>OpenEhrJson</c> façade.
/// </summary>
/// <remarks>
/// Schemaless mode handles Composition root metadata fields and
/// <c>context/*</c> only: any clinical content path (under an archetype
/// root) cannot be resolved without an OPT and triggers
/// <see cref="FlatSchemaRequiredException"/>. Schema-driven mode
/// delegates type resolution to an <see cref="ITemplateSchema"/>; the
/// production OPT-backed schema arrives with DotnetOpenEhr.Templates.
/// </remarks>
public static class OpenEhrFlatJson
{
    /// <summary>
    /// Parses a FLAT openEHR JSON document into a strongly-typed
    /// <see cref="Composition"/> using the schemaless resolution rules.
    /// </summary>
    /// <exception cref="FlatSchemaRequiredException">
    /// Thrown when one or more paths cannot be resolved without a
    /// template schema. The exception carries the unresolved paths.
    /// </exception>
    public static Composition? ParseComposition(ReadOnlySpan<byte> utf8Json)
    {
        IReadOnlyList<KeyValuePair<FlatPath, JsonElement>> entries = FlatJsonReader.Read(utf8Json);
        return ParseEntries(entries, schema: null);
    }

    /// <summary>
    /// Parses a FLAT openEHR JSON document into a strongly-typed
    /// <see cref="Composition"/> using the supplied template schema for
    /// polymorphism resolution. Falls back to the schemaless rules on a
    /// per-path basis when the schema returns <c>false</c>.
    /// </summary>
    public static Composition? ParseComposition(ReadOnlySpan<byte> utf8Json, ITemplateSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        IReadOnlyList<KeyValuePair<FlatPath, JsonElement>> entries = FlatJsonReader.Read(utf8Json);
        return ParseEntries(entries, schema);
    }

    /// <summary>
    /// Serialises <paramref name="composition"/> to FLAT JSON, producing
    /// keys rooted at <paramref name="templateId"/>.
    /// </summary>
    public static byte[] Serialize(Composition composition, string templateId)
        => FlatJsonWriter.Write(composition, templateId);

    /// <summary>
    /// Schema-driven serialisation: emits Composition metadata + the
    /// full archetype-content tree using <paramref name="schema"/> as
    /// the FLAT-path root authority.
    /// </summary>
    public static byte[] Serialize(Composition composition, ITemplateSchema schema)
        => FlatJsonWriter.Write(composition, schema);

    private static Composition? ParseEntries(
        IReadOnlyList<KeyValuePair<FlatPath, JsonElement>> entries,
        ITemplateSchema? schema)
    {
        if (entries.Count == 0) return null;

        // When a schema is supplied, use its TemplateId verbatim as the
        // root path segment (so the schema's TryResolveType index keys
        // line up). Otherwise fall back to the heuristic of "first
        // non-ctx path head".
        string templateId;
        if (schema is not null && !string.IsNullOrEmpty(schema.TemplateId))
        {
            templateId = schema.TemplateId;
        }
        else
        {
            templateId = entries[0].Key.TemplateId;
            foreach (KeyValuePair<FlatPath, JsonElement> entry in entries)
            {
                string head = entry.Key.TemplateId;
                if (!string.IsNullOrEmpty(head) && !string.Equals(head, "ctx", StringComparison.Ordinal))
                {
                    templateId = head;
                    break;
                }
            }
        }
        if (string.IsNullOrEmpty(templateId))
        {
            throw new JsonException("FLAT document must contain at least one path segment (template id).");
        }

        Composition composition = new();
        List<string> unresolved = [];

        foreach (KeyValuePair<FlatPath, JsonElement> entry in entries)
        {
            string head = entry.Key.TemplateId;
            if (!string.Equals(head, templateId, StringComparison.Ordinal))
            {
                // Non-template prefixes (e.g. ehrbase "ctx" defaults)
                // and any cross-template mixing are out of the
                // schemaless scope. Mark them unresolved.
                unresolved.Add(entry.Key.OriginalForm);
                continue;
            }

            // Metadata-applier first; it covers the Composition root
            // properties and EventContext, which the schema-driven
            // content walker does not own.
            if (TryApplyMetadataEntry(composition, templateId, entry))
            {
                continue;
            }

            // Schema-driven mode: hand the entry to the content parser.
            if (schema is not null
                && FlatJsonContentParser.TryApplyContentEntry(composition, templateId, entry, schema))
            {
                continue;
            }

            unresolved.Add(entry.Key.OriginalForm);
        }

        if (unresolved.Count > 0)
        {
            throw new FlatSchemaRequiredException(templateId, unresolved);
        }

        if (composition.Name is null || string.IsNullOrEmpty(composition.Name.Value))
        {
            composition.Name = new DvText(templateId);
        }

        if (string.IsNullOrEmpty(composition.ArchetypeNodeId))
        {
            composition.ArchetypeNodeId = $"openEHR-EHR-COMPOSITION.{templateId}.v1";
        }

        return composition;
    }

    private static bool TryApplyMetadataEntry(
        Composition composition,
        string templateId,
        KeyValuePair<FlatPath, JsonElement> entry)
    {
        string fullPath = entry.Key.OriginalForm;
        string attribute = entry.Key.Attribute; // includes leading '|' or empty

        // Strip the "<templateId>" prefix; the next char must be '/' or '|'.
        if (fullPath.Length <= templateId.Length) return false;
        char delim = fullPath[templateId.Length];
        if (delim != '/' && delim != '|') return false;
        string tail = fullPath.Substring(templateId.Length + 1);
        if (delim == '|')
        {
            // Path was just "<templateId>|attr" — no body. Reject.
            return false;
        }

        string body = attribute.Length == 0
            ? tail
            : tail.Substring(0, tail.Length - attribute.Length);

        return body switch
        {
            "name" => TrySetRootName(composition, attribute, entry.Value),
            "_archetype_node_id" => TrySetRootArchetypeNodeId(composition, attribute, entry.Value),
            "category" => TrySetDvCodedText(composition.Category, attribute, entry.Value),
            "language" => TrySetCodePhrase(composition.Language, attribute, entry.Value),
            "territory" => TrySetCodePhrase(composition.Territory, attribute, entry.Value),
            "composer" => TrySetComposerAttr(composition, attribute, entry.Value),
            "_uid" => TrySetUid(composition, attribute, entry.Value),
            "context/start_time" => TrySetContextDateTime(composition, forStart: true, attribute, entry.Value),
            "context/_end_time" => TrySetContextDateTime(composition, forStart: false, attribute, entry.Value),
            "context/location" => TrySetContextLocation(composition, attribute, entry.Value),
            "context/setting" => TrySetDvCodedText(EnsureContext(composition).Setting, attribute, entry.Value),
            "context/_health_care_facility" => TrySetHealthCareFacilityAttr(composition, attribute, entry.Value),
            _ => false,
        };
    }

    private static bool TrySetRootName(Composition composition, string attribute, JsonElement value)
    {
        if (!string.Equals(attribute, "|value", StringComparison.Ordinal)) return false;
        composition.Name = new DvText(value.GetString() ?? string.Empty);
        return true;
    }

    private static bool TrySetRootArchetypeNodeId(Composition composition, string attribute, JsonElement value)
    {
        if (attribute.Length != 0) return false;
        composition.ArchetypeNodeId = value.GetString() ?? string.Empty;
        return true;
    }

    private static EventContext EnsureContext(Composition composition)
        => composition.Context ??= new EventContext();

    private static bool TrySetDvCodedText(DvCodedText target, string attribute, JsonElement value)
    {
        switch (attribute)
        {
            case "|code":
                target.DefiningCode.CodeString = value.GetString() ?? string.Empty;
                return true;
            case "|value":
                target.Value = value.GetString() ?? string.Empty;
                return true;
            case "|terminology":
                target.DefiningCode.TerminologyId.Value = value.GetString() ?? string.Empty;
                return true;
            default:
                return false;
        }
    }

    private static bool TrySetCodePhrase(CodePhrase target, string attribute, JsonElement value)
    {
        switch (attribute)
        {
            case "|code":
                target.CodeString = value.GetString() ?? string.Empty;
                return true;
            case "|terminology":
                target.TerminologyId.Value = value.GetString() ?? string.Empty;
                return true;
            default:
                return false;
        }
    }

    private static bool TrySetComposerAttr(Composition composition, string attribute, JsonElement value)
    {
        PartyIdentified identified = composition.Composer as PartyIdentified ?? new PartyIdentified();
        composition.Composer = identified;
        return TrySetPartyIdentifiedAttr(identified, attribute, value);
    }

    private static bool TrySetHealthCareFacilityAttr(Composition composition, string attribute, JsonElement value)
    {
        EventContext ctx = EnsureContext(composition);
        ctx.HealthCareFacility ??= new PartyIdentified();
        return TrySetPartyIdentifiedAttr(ctx.HealthCareFacility, attribute, value);
    }

    private static bool TrySetPartyIdentifiedAttr(PartyIdentified party, string attribute, JsonElement value)
    {
        switch (attribute)
        {
            case "|name":
                party.Name = value.GetString();
                return true;
            case "|id":
            case "|id_namespace":
            case "|id_assigner":
            case "|id_type":
                ApplyIdentifierAttr(party, attribute, value);
                return true;
            default:
                return false;
        }
    }

    private static void ApplyIdentifierAttr(PartyIdentified party, string attribute, JsonElement value)
    {
        List<DvIdentifier> ids = party.Identifiers as List<DvIdentifier>
            ?? [.. party.Identifiers ?? []];
        if (ids.Count == 0) ids.Add(new DvIdentifier());
        DvIdentifier first = ids[0];
        switch (attribute)
        {
            case "|id": first.Id = value.GetString() ?? string.Empty; break;
            case "|id_namespace": first.Issuer = value.GetString(); break;
            case "|id_assigner": first.Assigner = value.GetString(); break;
            case "|id_type": first.Type = value.GetString(); break;
        }
        party.Identifiers = ids;
    }

    private static bool TrySetUid(Composition composition, string attribute, JsonElement value)
    {
        if (attribute.Length != 0) return false;
        composition.Uid = new HierObjectId { Value = value.GetString() ?? string.Empty };
        return true;
    }

    private static bool TrySetContextDateTime(Composition composition, bool forStart, string attribute, JsonElement value)
    {
        if (attribute.Length != 0) return false;
        EventContext ctx = EnsureContext(composition);
        string raw = value.GetString() ?? string.Empty;
        IsoDateTime iso = IsoDateTime.Parse(raw);
        if (forStart) ctx.StartTime = new DvDateTime(iso);
        else ctx.EndTime = new DvDateTime(iso);
        return true;
    }

    private static bool TrySetContextLocation(Composition composition, string attribute, JsonElement value)
    {
        if (attribute.Length != 0) return false;
        EnsureContext(composition).Location = value.GetString();
        return true;
    }
}
