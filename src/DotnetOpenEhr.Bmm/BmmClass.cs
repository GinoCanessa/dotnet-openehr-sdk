namespace DotnetOpenEhr.Bmm;

/// <summary>
/// A class declaration in a BMM schema. Materialised from one entry in
/// the top-level <c>class_definitions</c> hash.
/// </summary>
/// <remarks>
/// Inheritance is captured as raw ancestor names (<see cref="Ancestors"/>);
/// the parser does not resolve those to <see cref="BmmClass"/>
/// references. Later phases that need the resolved graph can build it
/// from <see cref="BmmModel.ClassDefinitions"/>.
/// </remarks>
public sealed class BmmClass
{
    public BmmClass(
        string name,
        IReadOnlyList<string> ancestors,
        bool isAbstract,
        IReadOnlyDictionary<string, BmmProperty> properties,
        IReadOnlyList<BmmGenericParameter> genericParameters,
        BmmSourceReference source = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(ancestors);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(genericParameters);

        Name = name;
        Ancestors = ancestors;
        IsAbstract = isAbstract;
        Properties = properties;
        GenericParameters = genericParameters;
        Source = source;
    }

    public string Name { get; }

    /// <summary>
    /// Raw parent-class names from the BMM source. Resolution to
    /// <see cref="BmmClass"/> instances is deferred.
    /// </summary>
    public IReadOnlyList<string> Ancestors { get; }

    public bool IsAbstract { get; }

    /// <summary>
    /// Properties declared directly on this class, keyed by property
    /// name. Properties inherited from <see cref="Ancestors"/> are not
    /// flattened in.
    /// </summary>
    public IReadOnlyDictionary<string, BmmProperty> Properties { get; }

    public IReadOnlyList<BmmGenericParameter> GenericParameters { get; }

    public bool IsGeneric => GenericParameters.Count > 0;

    public BmmSourceReference Source { get; }
}
