namespace DotnetOpenEhr.Odin.Values;

/// <summary>
/// ODIN list of homogeneously-typed primitive values (spec 7.4) or
/// inline list of nested blocks. Mutable for round-trip fidelity.
/// </summary>
public sealed class OdinList : OdinValue
{
    public OdinList()
    {
        Items = [];
    }

    public OdinList(IList<OdinValue> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = items;
    }

    public IList<OdinValue> Items { get; set; }

    /// <summary>
    /// True if the source list had the ODIN "list continuation marker"
    /// (a trailing <c>, ...</c>) used in spec 7.4 to disambiguate
    /// single-item lists from scalars.
    /// </summary>
    public bool HasContinuationMarker { get; set; }

    public override OdinKind Kind => OdinKind.List;
}
