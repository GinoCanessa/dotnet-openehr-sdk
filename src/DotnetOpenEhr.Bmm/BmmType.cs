namespace DotnetOpenEhr.Bmm;

/// <summary>
/// Discriminator for the closed set of BMM type forms supported by the
/// BMM parser.
/// </summary>
public enum BmmTypeKind
{
    Simple = 0,
    Generic = 1,
    Container = 2,
}

/// <summary>
/// Base of the BMM type reference hierarchy. Concrete subclasses model
/// the three forms accepted by the parser: <see cref="BmmSimpleType"/>,
/// <see cref="BmmGenericType"/>, and <see cref="BmmContainerType"/>.
/// </summary>
/// <remarks>
/// Type references are <em>raw</em>: <see cref="TypeName"/> is the source
/// string. RM-resolution layers bind these to <see cref="BmmClass"/>
/// instances; the BMM parser does not.
/// </remarks>
public abstract class BmmType
{
    protected BmmType(string typeName)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        TypeName = typeName;
    }

    /// <summary>
    /// The root class name of this type reference (e.g. <c>List</c>,
    /// <c>HASH</c>, <c>DV_QUANTITY</c>). Generic parameters are not
    /// included.
    /// </summary>
    public string TypeName { get; }

    public abstract BmmTypeKind Kind { get; }
}

/// <summary>
/// Atomic type reference (no generic parameters), e.g. <c>String</c>.
/// </summary>
public sealed class BmmSimpleType : BmmType
{
    public BmmSimpleType(string typeName) : base(typeName) { }

    public override BmmTypeKind Kind => BmmTypeKind.Simple;
}

/// <summary>
/// Open generic type reference, e.g. <c>EVENT&lt;ITEM_TREE&gt;</c>.
/// Container types (<see cref="BmmContainerType"/>) are kept distinct
/// because their root name carries cardinality semantics (List, Set, ...).
/// </summary>
public sealed class BmmGenericType : BmmType
{
    public BmmGenericType(string typeName, IReadOnlyList<BmmType> typeArguments)
        : base(typeName)
    {
        ArgumentNullException.ThrowIfNull(typeArguments);
        if (typeArguments.Count == 0)
        {
            throw new ArgumentException("Generic type requires at least one argument.", nameof(typeArguments));
        }
        TypeArguments = typeArguments;
    }

    public IReadOnlyList<BmmType> TypeArguments { get; }

    public override BmmTypeKind Kind => BmmTypeKind.Generic;
}

/// <summary>
/// Container type reference: <c>List&lt;X&gt;</c>, <c>Set&lt;X&gt;</c>,
/// <c>Array&lt;X&gt;</c>, <c>Hash&lt;K,V&gt;</c>. Root name is
/// case-preserved from source so a writer can re-emit it.
/// </summary>
public sealed class BmmContainerType : BmmType
{
    public BmmContainerType(string containerName, IReadOnlyList<BmmType> typeArguments)
        : base(containerName)
    {
        ArgumentNullException.ThrowIfNull(typeArguments);
        if (typeArguments.Count == 0)
        {
            throw new ArgumentException("Container type requires at least one argument.", nameof(typeArguments));
        }
        TypeArguments = typeArguments;
    }

    public IReadOnlyList<BmmType> TypeArguments { get; }

    public override BmmTypeKind Kind => BmmTypeKind.Container;

    /// <summary>
    /// Set of recognised container roots; matches the BMM serial form's
    /// expected casing-insensitive tokens.
    /// </summary>
    public static IReadOnlySet<string> KnownContainerRoots { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "List",
            "Set",
            "Array",
            "Hash",
            "P_List",
            "P_Set",
            "P_Array",
            "P_Hash",
        };

    public static bool IsContainerRoot(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return KnownContainerRoots.Contains(name);
    }
}
