using System.Diagnostics.CodeAnalysis;

namespace DotnetOpenEhr.Odin.Values;

/// <summary>
/// ODIN object literal (spec 5.1). A bag of attribute_name =&gt; value
/// pairs with an optional <see cref="OdinValue.TypeMarker"/>. Backed by
/// a <see cref="Dictionary{TKey, TValue}"/> which preserves insertion
/// order so the writer can emit attributes in source order.
/// </summary>
public sealed class OdinObject : OdinValue, IOdinKeyed
{
    public OdinObject()
    {
        Attributes = new Dictionary<string, OdinValue>(StringComparer.Ordinal);
    }

    public OdinObject(IDictionary<string, OdinValue> attributes, string? typeMarker = null)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        Attributes = attributes;
        TypeMarker = typeMarker;
    }

    public IDictionary<string, OdinValue> Attributes { get; set; }

    public bool TryGet(string key, [NotNullWhen(true)] out OdinValue? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (Attributes.TryGetValue(key, out OdinValue? v) && v is not null)
        {
            value = v;
            return true;
        }
        value = null;
        return false;
    }

    public override OdinKind Kind => OdinKind.Object;
}
