namespace DotnetOpenEhr.Bmm;

/// <summary>
/// A formal generic parameter declared on a <see cref="BmmClass"/>:
/// e.g. the <c>T</c> in <c>INTERVAL&lt;T&gt;</c>, optionally constrained
/// to a class via <c>conforms_to_type</c>.
/// </summary>
public sealed class BmmGenericParameter
{
    public BmmGenericParameter(string name, string? conformsToType = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        ConformsToType = conformsToType;
    }

    public string Name { get; }

    /// <summary>
    /// Optional <c>conforms_to_type</c> constraint. The value is the raw
    /// type-name string from the BMM source; resolution to a
    /// <see cref="BmmClass"/> is deferred to later phases.
    /// </summary>
    public string? ConformsToType { get; }
}
