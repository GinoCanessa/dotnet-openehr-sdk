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
        using JsonDocument doc = JsonDocument.Parse(utf8Json.ToArray());
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("FLAT document root must be a JSON object.");
        }

        List<KeyValuePair<FlatPath, JsonElement>> entries = [];
        foreach (JsonProperty property in doc.RootElement.EnumerateObject())
        {
            FlatPath path = FlatPath.Parse(property.Name.AsSpan());
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
        return Read(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
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
        return Read(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }
}
