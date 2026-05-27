using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotnetOpenEhr.Foundation.Iso;

namespace DotnetOpenEhr.Rm.DataTypes.DateTime;

/// <summary>
/// STJ converter pair for the Foundation <c>Iso*</c> value types. Reads
/// and writes the canonical lexical form so partial precision survives a
/// JSON round-trip. AOT-safe: pure string ↔ <typeparamref name="T"/>
/// without reflection.
/// </summary>
public sealed class IsoLexicalConverter<T> : JsonConverter<T>
    where T : class
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        string text = reader.GetString() ?? throw new JsonException("Expected ISO lexical string.");
        T? parsed = Parse(text);
        if (parsed is not null) return parsed;
        if (typeof(T) == typeof(IsoDuration) && TryParseZeroDuration(text, out IsoDuration? zero))
        {
            return (T)(object)zero;
        }
        throw new JsonException($"'{text}' is not a valid {typeof(T).Name}.");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => writer.WriteStringValue(Lexical(value));

    private static T? Parse(string text)
    {
        if (typeof(T) == typeof(IsoDate))
        {
            return IsoDate.TryParse(text, out IsoDate? d) ? (T)(object)d : null;
        }
        if (typeof(T) == typeof(IsoTime))
        {
            return IsoTime.TryParse(text, out IsoTime? t) ? (T)(object)t : null;
        }
        if (typeof(T) == typeof(IsoDateTime))
        {
            return IsoDateTime.TryParse(text, out IsoDateTime? dt) ? (T)(object)dt : null;
        }
        if (typeof(T) == typeof(IsoDuration))
        {
            return IsoDuration.TryParse(text, out IsoDuration? dur) ? (T)(object)dur : null;
        }
        throw new NotSupportedException($"{typeof(T).Name} is not a supported Iso type.");
    }

    [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "STJ contract guarantees value is non-null when called.")]
    private static string Lexical(T value)
    {
        return value switch
        {
            IsoDate d => d.OriginalLexicalForm,
            IsoTime t => t.OriginalLexicalForm,
            IsoDateTime dt => dt.OriginalLexicalForm,
            IsoDuration dur => dur.OriginalLexicalForm,
            _ => throw new NotSupportedException($"{value.GetType().Name} is not a supported Iso type.")
        };
    }

    // Fallback for legitimate but Foundation-strict-parser-rejected
    // "zero duration" lexical forms (PT0S, PT0H, PT0M, P0D). The
    // Foundation IsoDuration parser rejects PT-only forms whose time
    // components are all zero (see IsoDuration.TryParse), but the
    // openEHR canonical wire form regularly emits "PT0S" for an empty
    // sampling period. Round-trip integration over real KDS fixtures
    // depends on accepting these without losing the lexical form.
    private static bool TryParseZeroDuration(string text, [NotNullWhen(true)] out IsoDuration? value)
    {
        value = null;
        if (string.IsNullOrEmpty(text)) return false;
        bool negative = false;
        int idx = 0;
        if (text[0] == '-') { negative = true; idx = 1; }
        else if (text[0] == '+') { idx = 1; }
        if (idx >= text.Length || text[idx] != 'P') return false;
        idx++;
        bool sawComponent = false;
        bool inTime = false;
        while (idx < text.Length)
        {
            char c = text[idx];
            if (c == 'T') { inTime = true; idx++; continue; }
            // Accept digits + optional decimal point, must equal zero.
            int numStart = idx;
            while (idx < text.Length && (char.IsDigit(text[idx]) || text[idx] == '.' || text[idx] == ','))
            {
                idx++;
            }
            if (numStart == idx || idx >= text.Length) return false;
            string numText = text.Substring(numStart, idx - numStart).Replace(',', '.');
            if (!decimal.TryParse(numText, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out decimal num))
            {
                return false;
            }
            if (num != 0m) return false;
            char unit = text[idx++];
            if (inTime)
            {
                if (unit is not ('H' or 'M' or 'S')) return false;
            }
            else
            {
                if (unit is not ('Y' or 'M' or 'W' or 'D')) return false;
            }
            sawComponent = true;
        }
        if (!sawComponent) return false;
        value = new IsoDuration(isNegative: negative, originalLexicalForm: text);
        return true;
    }
}
