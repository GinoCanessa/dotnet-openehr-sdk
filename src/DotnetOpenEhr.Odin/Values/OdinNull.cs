namespace DotnetOpenEhr.Odin.Values;

/// <summary>
/// ODIN void / null value. Corresponds to the <c>&lt;...&gt;</c> or empty
/// block syntax in the ODIN spec section 5.3.
/// </summary>
public sealed class OdinNull : OdinValue
{
    /// <summary>
    /// Public so callers can construct dedicated nulls when round-tripping;
    /// the shared <see cref="OdinValue.Null"/> singleton is preferred.
    /// </summary>
    public OdinNull()
    {
    }

    public override OdinKind Kind => OdinKind.Null;
}
