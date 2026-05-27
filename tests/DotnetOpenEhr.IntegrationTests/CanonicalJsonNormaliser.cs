using System.Text.Json;

namespace DotnetOpenEhr.IntegrationTests;

/// <summary>
/// Pragmatic canonical-JSON normaliser used by the byte-equivalence
/// round-trip integration test. Implements three rules:
///   1. Drop all insignificant whitespace.
///   2. Sort object keys alphabetically (applied recursively).
///   3. Render integer-valued JSON numbers without a decimal point
///      (e.g. <c>1.0</c> → <c>1</c>).
/// </summary>
/// <remarks>
/// This is not the full openEHR canonical-form ordering specification
/// — see <c>docs/canonical-json-ordering.md</c> for the deferral
/// rationale. The normaliser is good enough to surface drift caused by
/// formatting alone (whitespace, key ordering, integer formatting) and
/// to make a best-effort byte-diff possible.
/// </remarks>
internal static class CanonicalJsonNormaliser
{
    public static byte[] Normalise(ReadOnlySpan<byte> utf8Json)
    {
        using JsonDocument doc = JsonDocument.Parse(utf8Json.ToArray());
        using MemoryStream buffer = new();
        JsonWriterOptions options = new() { Indented = false, SkipValidation = true };
        using (Utf8JsonWriter writer = new(buffer, options))
        {
            WriteValue(writer, doc.RootElement);
        }
        return buffer.ToArray();
    }

    private static void WriteValue(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                List<JsonProperty> props = [.. element.EnumerateObject()];
                props.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
                foreach (JsonProperty p in props)
                {
                    writer.WritePropertyName(p.Name);
                    WriteValue(writer, p.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement child in element.EnumerateArray())
                {
                    WriteValue(writer, child);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                WriteNumber(writer, element);
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
                throw new JsonException($"Unsupported JSON value kind: {element.ValueKind}.");
        }
    }

    private static void WriteNumber(Utf8JsonWriter writer, JsonElement element)
    {
        // Try integer first so 1.0 collapses to 1.
        if (element.TryGetInt64(out long asLong))
        {
            writer.WriteNumberValue(asLong);
            return;
        }
        if (element.TryGetDouble(out double asDouble))
        {
            if (!double.IsNaN(asDouble) && !double.IsInfinity(asDouble)
                && asDouble == Math.Truncate(asDouble)
                && asDouble is >= long.MinValue and <= long.MaxValue)
            {
                writer.WriteNumberValue((long)asDouble);
            }
            else
            {
                writer.WriteNumberValue(asDouble);
            }
            return;
        }
        // Fallback — write the raw token if neither int nor double parses.
        writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
    }
}
