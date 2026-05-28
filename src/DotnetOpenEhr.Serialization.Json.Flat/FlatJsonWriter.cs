using System.Text.Json;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataTypes.Basic;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Templates.Abstractions;

namespace DotnetOpenEhr.Serialization.Json.Flat;

/// <summary>
/// Walks a <see cref="Composition"/> tree and emits its FLAT openEHR
/// JSON form. The first path segment is the supplied
/// <c>templateId</c>; metadata-level properties (category, language,
/// territory, composer, uid, and any context fields) are emitted as
/// FLAT keys with their primitive-value attributes.
/// </summary>
/// <remarks>
/// Schemaless writer scope: covers Composition root metadata and
/// EventContext. Content emission is intentionally limited to the
/// values reachable without a template — clinical content under
/// archetype roots is deferred to the template-aware writer.
/// </remarks>
public static class FlatJsonWriter
{
    /// <summary>
    /// Serialises <paramref name="composition"/> to UTF-8 FLAT JSON.
    /// </summary>
    public static byte[] Write(Composition composition, string templateId)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentException.ThrowIfNullOrEmpty(templateId);

        using MemoryStream output = new();
        WriteCore(output, composition, templateId);
        return output.ToArray();
    }

    /// <summary>
    /// Serialises <paramref name="composition"/> to FLAT JSON, writing
    /// to <paramref name="output"/>.
    /// </summary>
    public static void Write(Stream output, Composition composition, string templateId)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentException.ThrowIfNullOrEmpty(templateId);
        WriteCore(output, composition, templateId);
    }

    /// <summary>
    /// Schema-driven overload: emits Composition root metadata, the
    /// EventContext, and the full archetypable content tree using
    /// <paramref name="schema"/> as the FLAT-path root authority.
    /// The archetype-content walker is implemented in
    /// <see cref="FlatJsonContentWriter"/>.
    /// </summary>
    public static byte[] Write(Composition composition, ITemplateSchema schema)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(schema);
        using MemoryStream output = new();
        WriteCore(output, composition, schema);
        return output.ToArray();
    }

    /// <summary>
    /// Schema-driven stream overload — same contract as
    /// <see cref="Write(Composition, ITemplateSchema)"/>.
    /// </summary>
    public static void Write(Stream output, Composition composition, ITemplateSchema schema)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(schema);
        WriteCore(output, composition, schema);
    }

    private static void WriteCore(Stream output, Composition composition, ITemplateSchema schema)
    {
        JsonWriterOptions opts = new()
        {
            Indented = false,
            SkipValidation = false,
        };
        using Utf8JsonWriter writer = new(output, opts);
        writer.WriteStartObject();
        WriteCompositionBody(writer, composition, schema.TemplateId);
        FlatJsonContentWriter.WriteContent(writer, composition, schema);
        writer.WriteEndObject();
        writer.Flush();
    }

    private static void WriteCore(Stream output, Composition composition, string templateId)
    {
        JsonWriterOptions opts = new()
        {
            Indented = false,
            SkipValidation = false,
        };
        using Utf8JsonWriter writer = new(output, opts);
        writer.WriteStartObject();

        WriteCompositionBody(writer, composition, templateId);

        writer.WriteEndObject();
        writer.Flush();
    }

    private static void WriteCompositionBody(Utf8JsonWriter writer, Composition composition, string templateId)
    {
        WriteDvCodedText(writer, $"{templateId}/category", composition.Category);

        if (composition.Context is not null)
        {
            WriteContext(writer, $"{templateId}/context", composition.Context);
        }

        WriteCodePhrase(writer, $"{templateId}/language", composition.Language);
        WriteCodePhrase(writer, $"{templateId}/territory", composition.Territory);
        WritePartyProxy(writer, $"{templateId}/composer", composition.Composer);

        if (composition.Uid is not null && !string.IsNullOrEmpty(composition.Uid.Value))
        {
            writer.WriteString($"{templateId}/_uid", composition.Uid.Value);
        }
    }

    private static void WriteContext(Utf8JsonWriter writer, string basePath, EventContext context)
    {
        WriteDvDateTime(writer, $"{basePath}/start_time", context.StartTime);

        if (context.EndTime is not null)
        {
            WriteDvDateTime(writer, $"{basePath}/_end_time", context.EndTime);
        }

        if (!string.IsNullOrEmpty(context.Location))
        {
            writer.WriteString($"{basePath}/location", context.Location);
        }

        WriteDvCodedText(writer, $"{basePath}/setting", context.Setting);

        if (context.HealthCareFacility is not null)
        {
            WritePartyIdentified(writer, $"{basePath}/_health_care_facility", context.HealthCareFacility);
        }
    }

    private static void WriteDvCodedText(Utf8JsonWriter writer, string basePath, DvCodedText? value)
    {
        if (value is null) return;
        if (!string.IsNullOrEmpty(value.DefiningCode.CodeString))
        {
            writer.WriteString($"{basePath}|code", value.DefiningCode.CodeString);
        }
        if (!string.IsNullOrEmpty(value.Value))
        {
            writer.WriteString($"{basePath}|value", value.Value);
        }
        if (!string.IsNullOrEmpty(value.DefiningCode.TerminologyId.Value))
        {
            writer.WriteString($"{basePath}|terminology", value.DefiningCode.TerminologyId.Value);
        }
    }

    private static void WriteCodePhrase(Utf8JsonWriter writer, string basePath, CodePhrase? value)
    {
        if (value is null) return;
        if (!string.IsNullOrEmpty(value.TerminologyId.Value))
        {
            writer.WriteString($"{basePath}|terminology", value.TerminologyId.Value);
        }
        if (!string.IsNullOrEmpty(value.CodeString))
        {
            writer.WriteString($"{basePath}|code", value.CodeString);
        }
    }

    private static void WritePartyProxy(Utf8JsonWriter writer, string basePath, PartyProxy? party)
    {
        if (party is null) return;
        if (party is PartyIdentified identified)
        {
            WritePartyIdentified(writer, basePath, identified);
        }
        // PartySelf carries no FLAT-addressable scalars in the
        // schemaless metadata surface.
    }

    private static void WritePartyIdentified(Utf8JsonWriter writer, string basePath, PartyIdentified party)
    {
        if (!string.IsNullOrEmpty(party.Name))
        {
            writer.WriteString($"{basePath}|name", party.Name);
        }
        if (party.Identifiers is { Count: > 0 })
        {
            DvIdentifier first = party.Identifiers[0];
            if (!string.IsNullOrEmpty(first.Id))
            {
                writer.WriteString($"{basePath}|id", first.Id);
            }
            if (!string.IsNullOrEmpty(first.Issuer))
            {
                writer.WriteString($"{basePath}|id_namespace", first.Issuer);
            }
            if (!string.IsNullOrEmpty(first.Assigner))
            {
                writer.WriteString($"{basePath}|id_assigner", first.Assigner);
            }
            if (!string.IsNullOrEmpty(first.Type))
            {
                writer.WriteString($"{basePath}|id_type", first.Type);
            }
        }
    }

    private static void WriteDvDateTime(Utf8JsonWriter writer, string key, DvDateTime value)
    {
        writer.WriteString(key, value.Value.OriginalLexicalForm);
    }

    /// <summary>
    /// Re-emits a previously-parsed FLAT entry list in canonical key
    /// order (ordinal sort by full path). Useful for round-trip
    /// comparison: both sides are sorted before byte-equivalence is
    /// asserted, so source-document ordering is irrelevant.
    /// </summary>
    public static byte[] WriteCanonical(IReadOnlyList<KeyValuePair<FlatPath, JsonElement>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        List<KeyValuePair<FlatPath, JsonElement>> sorted = [.. entries];
        sorted.Sort(static (a, b) => string.CompareOrdinal(a.Key.OriginalForm, b.Key.OriginalForm));

        using MemoryStream output = new();
        JsonWriterOptions opts = new() { Indented = false, SkipValidation = false };
        using (Utf8JsonWriter writer = new(output, opts))
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<FlatPath, JsonElement> entry in sorted)
            {
                writer.WritePropertyName(entry.Key.OriginalForm);
                WriteScalar(writer, entry.Value);
            }
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    private static void WriteScalar(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                if (value.TryGetInt64(out long l))
                {
                    writer.WriteNumberValue(l);
                }
                else
                {
                    writer.WriteNumberValue(value.GetDouble());
                }
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported FLAT scalar kind: {value.ValueKind}");
        }
    }
}
