using System.Text.Json;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;

namespace DotnetOpenEhr.Serialization.Json;

/// <summary>
/// Top-level static façade for serializing and deserializing canonical
/// openEHR JSON Compositions through the source-generated
/// <see cref="OpenEhrJsonContext"/>. AOT-safe: no reflection, no
/// runtime <see cref="JsonSerializerOptions"/> mutation.
/// </summary>
/// <remarks>
/// All paths route through the <see cref="Locatable"/>
/// polymorphic-base type info so the canonical <c>_type</c>
/// discriminator is emitted at the document root (and re-read on
/// parse). STJ only writes <c>_type</c> when serializing via a
/// polymorphic base; routing through <see cref="Locatable"/> keeps the
/// root payload faithful to the canonical openEHR JSON form.
/// </remarks>
public static class OpenEhrJson
{
    /// <summary>
    /// Parses a canonical openEHR JSON UTF-8 byte payload into a
    /// strongly-typed <see cref="Composition"/>.
    /// </summary>
    /// <exception cref="JsonException">If the root <c>_type</c> is not
    /// <c>"COMPOSITION"</c> or is missing.</exception>
    public static Composition? ParseComposition(ReadOnlySpan<byte> utf8Json)
        => CastToComposition(JsonSerializer.Deserialize(utf8Json, OpenEhrJsonContext.Default.Locatable));

    /// <summary>
    /// Parses a canonical openEHR JSON string into a strongly-typed
    /// <see cref="Composition"/>. Convenience overload that defers to
    /// the UTF-8 byte path used by the source generator.
    /// </summary>
    public static Composition? ParseComposition(string json)
        => CastToComposition(JsonSerializer.Deserialize(json, OpenEhrJsonContext.Default.Locatable));

    /// <summary>
    /// Serializes a <see cref="Composition"/> to a UTF-8 byte array in
    /// canonical openEHR JSON form (with the root <c>_type</c>
    /// discriminator written).
    /// </summary>
    public static byte[] Serialize(Composition composition)
        => JsonSerializer.SerializeToUtf8Bytes<Locatable>(composition, OpenEhrJsonContext.Default.Locatable);

    /// <summary>
    /// Serializes a <see cref="Composition"/> to the given UTF-8
    /// <see cref="Stream"/> in canonical openEHR JSON form.
    /// </summary>
    public static void Serialize(Stream output, Composition composition)
        => JsonSerializer.Serialize<Locatable>(output, composition, OpenEhrJsonContext.Default.Locatable);

    /// <summary>
    /// Asynchronously parses a canonical openEHR JSON UTF-8 stream into
    /// a strongly-typed <see cref="Composition"/>.
    /// </summary>
    public static async ValueTask<Composition?> ParseCompositionAsync(Stream utf8Json, CancellationToken ct = default)
    {
        Locatable? located = await JsonSerializer
            .DeserializeAsync(utf8Json, OpenEhrJsonContext.Default.Locatable, ct)
            .ConfigureAwait(false);
        return CastToComposition(located);
    }

    private static Composition? CastToComposition(Locatable? value)
    {
        if (value is null) return null;
        if (value is Composition c) return c;
        throw new JsonException(
            $"Expected canonical openEHR Composition root (\"_type\":\"COMPOSITION\"), got \"{value.GetType().Name}\".");
    }
}

