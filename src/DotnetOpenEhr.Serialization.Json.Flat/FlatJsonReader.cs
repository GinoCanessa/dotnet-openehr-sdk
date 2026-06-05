using System.Text.Json;

namespace DotnetOpenEhr.Serialization.Json.Flat;

/// <summary>
/// Streams a FLAT openEHR JSON document into <see cref="FlatPath"/> /
/// <see cref="JsonElement"/> pairs. The document must be a single
/// top-level JSON object whose property values are scalars (string,
/// number, boolean, or null).
/// </summary>
public static class FlatJsonReader
{
    /// <summary>
    /// Reads <paramref name="utf8Json"/> as a FLAT document. The
    /// returned <see cref="JsonElement"/> instances are detached
    /// clones so the caller does not need to retain a backing
    /// <see cref="JsonDocument"/>.
    /// </summary>
    /// <exception cref="JsonException">
    /// If the document root is not an object, or a property name is
    /// not a valid FLAT path, or a value is not a scalar.
    /// </exception>
    public static IReadOnlyList<KeyValuePair<FlatPath, JsonElement>> Read(ReadOnlySpan<byte> utf8Json)
    {
        // JsonDocument.Parse has no ReadOnlySpan<byte> overload, so we
        // must materialise to an array here. Callers that already hold
        // a ReadOnlyMemory<byte> should use the overload below to skip
        // this copy.
        using JsonDocument doc = JsonDocument.Parse(utf8Json.ToArray());
        return ReadEntries(doc);
    }

    /// <summary>
    /// Reads <paramref name="utf8Json"/> as a FLAT document without
    /// the extra <see cref="ReadOnlySpan{T}.ToArray()"/> copy that the
    /// span overload requires. Prefer this overload when the source is
    /// a buffer (e.g. <see cref="MemoryStream.GetBuffer"/>).
    /// </summary>
    /// <exception cref="JsonException">
    /// If the document root is not an object, or a property name is
    /// not a valid FLAT path, or a value is not a scalar.
    /// </exception>
    public static IReadOnlyList<KeyValuePair<FlatPath, JsonElement>> Read(ReadOnlyMemory<byte> utf8Json)
    {
        using JsonDocument doc = JsonDocument.Parse(utf8Json);
        return ReadEntries(doc);
    }

    private static IReadOnlyList<KeyValuePair<FlatPath, JsonElement>> ReadEntries(JsonDocument doc)
    {
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("FLAT document root must be a JSON object.");
        }

        List<KeyValuePair<FlatPath, JsonElement>> entries = [];
        foreach (JsonProperty property in doc.RootElement.EnumerateObject())
        {
            FlatPath path;
            try
            {
                path = FlatPath.Parse(property.Name.AsSpan());
            }
            catch (FormatException ex)
            {
                throw new JsonException($"Invalid FLAT path '{property.Name}': {ex.Message}", ex);
            }
            JsonValueKind kind = property.Value.ValueKind;
            if (kind is not (JsonValueKind.String or JsonValueKind.Number
                             or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null))
            {
                throw new JsonException(
                    $"FLAT value at path '{property.Name}' must be a scalar, got {kind}.");
            }
            entries.Add(new KeyValuePair<FlatPath, JsonElement>(path, property.Value.Clone()));
        }
        return entries;
    }

    /// <summary>
    /// Reads a FLAT document from <paramref name="utf8Json"/>. The
    /// stream is buffered into memory before parsing; FLAT documents
    /// are typically small (single-digit KB) so this is acceptable.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<FlatPath, JsonElement>> Read(Stream utf8Json)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);
        using MemoryStream buffer = new();
        utf8Json.CopyTo(buffer);
        return Read(buffer.GetBuffer().AsMemory(0, (int)buffer.Length));
    }

    /// <summary>
    /// Asynchronously reads a FLAT document from <paramref name="utf8Json"/>.
    /// </summary>
    public static async ValueTask<IReadOnlyList<KeyValuePair<FlatPath, JsonElement>>> ReadAsync(
        Stream utf8Json,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);
        using MemoryStream buffer = new();
        await utf8Json.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return Read(buffer.GetBuffer().AsMemory(0, (int)buffer.Length));
    }
}
