using System.Diagnostics.CodeAnalysis;

namespace DotnetOpenEhr.Odin.Values;

/// <summary>
/// ODIN keyed container / hash (spec 5.4). Entries are keyed by the
/// textual form of any primitive comparable value (strings, integers,
/// dates). Backed by a <see cref="Dictionary{TKey, TValue}"/> which
/// preserves insertion order on .NET, so the writer emits keys in
/// source order.
/// </summary>
public sealed class OdinHash : OdinValue, IOdinKeyed
{
    public OdinHash()
    {
        Entries = new Dictionary<string, OdinValue>(StringComparer.Ordinal);
    }

    public OdinHash(IDictionary<string, OdinValue> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = entries;
    }

    public IDictionary<string, OdinValue> Entries { get; set; }

    /// <summary>
    /// Captures the primitive kind of the source keys (string, integer,
    /// date, time, datetime). Set by the parser; the writer uses it to
    /// re-emit keys without their string-quoting when appropriate.
    /// </summary>
    public OdinKind KeyKind { get; set; } = OdinKind.String;

    public bool TryGet(string key, [NotNullWhen(true)] out OdinValue? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (Entries.TryGetValue(key, out OdinValue? v) && v is not null)
        {
            value = v;
            return true;
        }
        value = null;
        return false;
    }

    public override OdinKind Kind => OdinKind.Hash;
}
