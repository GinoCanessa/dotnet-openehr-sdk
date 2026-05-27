using DotnetOpenEhr.Foundation;

namespace DotnetOpenEhr.Bmm;

/// <summary>
/// A property (attribute / relation) declared on a <see cref="BmmClass"/>.
/// </summary>
public sealed class BmmProperty
{
    public BmmProperty(
        string name,
        BmmType type,
        Cardinality? cardinality = null,
        Interval<int>? existence = null,
        bool isMandatory = false,
        bool isComputed = false,
        bool isImRuntime = false,
        BmmSourceReference source = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(type);
        Name = name;
        Type = type;
        Cardinality = cardinality;
        Existence = existence;
        IsMandatory = isMandatory;
        IsComputed = isComputed;
        IsImRuntime = isImRuntime;
        Source = source;
    }

    public string Name { get; }
    public BmmType Type { get; }

    /// <summary>
    /// Container cardinality (only set when the property's type is a
    /// container type). Encoded with <see cref="Foundation.Cardinality"/>.
    /// </summary>
    public Cardinality? Cardinality { get; }

    /// <summary>
    /// Occurrence existence interval — <c>|0..1|</c>, <c>|1..1|</c>,
    /// <c>|0..*|</c>, etc. Maps directly from the BMM
    /// <c>existence</c> attribute.
    /// </summary>
    public Interval<int>? Existence { get; }

    public bool IsMandatory { get; }
    public bool IsComputed { get; }

    /// <summary>
    /// True if the BMM declares this property as runtime-injected
    /// (<c>is_im_runtime</c>): the value is not statically captured in
    /// instances persisted on disk but materialised by an IM runtime.
    /// </summary>
    public bool IsImRuntime { get; }

    public BmmSourceReference Source { get; }
}
