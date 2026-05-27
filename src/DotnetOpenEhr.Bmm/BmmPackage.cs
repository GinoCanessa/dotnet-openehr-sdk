namespace DotnetOpenEhr.Bmm;

/// <summary>
/// A logical package within a BMM model. Packages group classes and may
/// nest sub-packages. Classes are referenced by name; the canonical
/// definition lives in <see cref="BmmModel.ClassDefinitions"/>.
/// </summary>
public sealed class BmmPackage
{
    public BmmPackage(
        string name,
        IReadOnlyList<string> classNames,
        IReadOnlyDictionary<string, BmmPackage> subPackages,
        BmmSourceReference source = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(classNames);
        ArgumentNullException.ThrowIfNull(subPackages);

        Name = name;
        ClassNames = classNames;
        SubPackages = subPackages;
        Source = source;
    }

    public string Name { get; }

    /// <summary>
    /// Names of classes declared directly in this package. The class
    /// objects themselves live in <see cref="BmmModel.ClassDefinitions"/>.
    /// </summary>
    public IReadOnlyList<string> ClassNames { get; }

    public IReadOnlyDictionary<string, BmmPackage> SubPackages { get; }

    public BmmSourceReference Source { get; }
}
