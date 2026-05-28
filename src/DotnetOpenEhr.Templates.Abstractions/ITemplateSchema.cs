namespace DotnetOpenEhr.Templates.Abstractions;

/// <summary>
/// Abstraction over the parts of an openEHR Operational Template (OPT)
/// that template-aware serializers need at runtime: resolving the
/// concrete RM type of a node addressed by a FLAT path. Implementers
/// include the stub <see cref="EmptyTemplateSchema"/> and the
/// production OPT-backed schema shipped with DotnetOpenEhr.Templates.
/// </summary>
public interface ITemplateSchema
{
    /// <summary>The canonical template id this schema describes.</summary>
    string TemplateId { get; }

    /// <summary>
    /// Best-effort iterator over every known node in the template, in
    /// document order. Implementations may return an empty collection
    /// when introspection is not supported.
    /// </summary>
    IReadOnlyCollection<TemplateNode> Nodes { get; }

    /// <summary>
    /// Resolves the concrete RM type of the node addressed by
    /// <paramref name="flatPath"/>. Returns <c>false</c> when the path
    /// is unknown to the schema; callers should then fall back to
    /// other resolution strategies (e.g. monomorphic-RM lookup or
    /// inline discriminator).
    /// </summary>
    bool TryResolveType(ReadOnlySpan<char> flatPath, out TemplateRmTypeResolution resolution);
}

/// <summary>
/// Describes a single node visible in a template: its AQL path, the
/// matching FLAT path the serializer would emit, the concrete RM type
/// name, and the multiplicity constraint.
/// </summary>
public sealed record TemplateNode(
    string AqlPath,
    string FlatPath,
    string RmTypeName,
    TemplateOccurrence Occurrence);

/// <summary>
/// Multiplicity bounds for a template node. <see cref="Max"/> uses
/// <see cref="int.MaxValue"/> for an unbounded upper bound.
/// </summary>
public readonly record struct TemplateOccurrence(int Min, int Max)
{
    /// <summary>The unbounded sentinel value used by <see cref="Max"/>.</summary>
    public const int Unbounded = int.MaxValue;
}

/// <summary>
/// Result of <see cref="ITemplateSchema.TryResolveType"/>: the canonical
/// openEHR <c>RM_TYPE_NAME</c> for the addressed node, and whether the
/// declared RM type at that point is polymorphic (i.e. the schema's
/// resolution is the only thing pinning it).
/// </summary>
public readonly record struct TemplateRmTypeResolution(string RmTypeName, bool IsPolymorphic);

/// <summary>
/// Stub <see cref="ITemplateSchema"/> useful for tests and for paths
/// where no template is available. Always returns <c>false</c> from
/// <see cref="TryResolveType"/> and exposes an empty <see cref="Nodes"/>
/// collection.
/// </summary>
public sealed class EmptyTemplateSchema : ITemplateSchema
{
    /// <summary>
    /// Initialises a new empty schema with the given <paramref name="templateId"/>.
    /// </summary>
    public EmptyTemplateSchema(string templateId)
    {
        ArgumentNullException.ThrowIfNull(templateId);
        TemplateId = templateId;
    }

    /// <inheritdoc />
    public string TemplateId { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<TemplateNode> Nodes { get; } = [];

    /// <inheritdoc />
    public bool TryResolveType(ReadOnlySpan<char> flatPath, out TemplateRmTypeResolution resolution)
    {
        _ = flatPath;
        resolution = default;
        return false;
    }
}
