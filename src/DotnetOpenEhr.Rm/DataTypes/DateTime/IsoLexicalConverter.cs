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
        return Parse(text) ?? throw new JsonException($"'{text}' is not a valid {typeof(T).Name}.");
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
}
