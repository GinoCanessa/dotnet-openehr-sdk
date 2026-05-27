using System.Collections.Frozen;
using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using DotnetOpenEhr.Archetypes.Aom2.Terminology;
using DotnetOpenEhr.Archetypes.Identification;
using DotnetOpenEhr.Bmm;
using DotnetOpenEhr.Foundation;
using DotnetOpenEhr.Templates.Abstractions;

namespace DotnetOpenEhr.Templates;

/// <summary>
/// Concrete <c>OPERATIONAL_TEMPLATE</c> implementation. Extends the
/// abstract <see cref="DotnetOpenEhr.Archetypes.Aom2.OperationalTemplate"/>
/// shipped by <c>DotnetOpenEhr.Archetypes</c> with OPT2-specific
/// extensions (<see cref="ComponentTerminologies"/>) and implements the
/// <see cref="ITemplateSchema"/> seam consumed by the template-aware
/// FLAT serializer.
/// </summary>
/// <remarks>
/// The <see cref="ITemplateSchema.Nodes"/> collection and the underlying
/// FLAT-path → RM-type index are materialised lazily by
/// <see cref="Initialize"/>; <see cref="Opt2Parser"/> calls this after
/// the AOM2 tree is built. Direct constructors leave the schema empty
/// until <see cref="Initialize"/> is invoked.
/// </remarks>
public sealed class OperationalTemplate
    : DotnetOpenEhr.Archetypes.Aom2.OperationalTemplate, ITemplateSchema
{
    private FrozenSet<TemplateNode> _nodes = [];
    private FrozenDictionary<string, TemplateRmTypeResolution> _index =
        FrozenDictionary<string, TemplateRmTypeResolution>.Empty;

    /// <summary>
    /// Public parameterless constructor — delegates to the
    /// protected-internal base.
    /// </summary>
    public OperationalTemplate()
    {
    }

    /// <summary>
    /// OPT2 <c>component_terminologies</c> block: the per-component-archetype
    /// terminology containers that an OPT2 carries on top of its own
    /// <see cref="DotnetOpenEhr.Archetypes.Aom2.Archetype.Terminology"/>.
    /// Keyed by the component archetype HRID.
    /// </summary>
    public Dictionary<ArchetypeHRID, ArchetypeTerminology> ComponentTerminologies { get; set; } = [];

    /// <inheritdoc />
    public string TemplateId => ArchetypeId?.ToString() ?? string.Empty;

    /// <inheritdoc />
    public IReadOnlyCollection<TemplateNode> Nodes => _nodes;

    /// <inheritdoc />
    public bool TryResolveType(ReadOnlySpan<char> flatPath, out TemplateRmTypeResolution resolution)
    {
        string key = flatPath.ToString();
        if (_index.TryGetValue(key, out TemplateRmTypeResolution res))
        {
            resolution = res;
            return true;
        }
        resolution = default;
        return false;
    }

    /// <summary>
    /// Eagerly walks <see cref="DotnetOpenEhr.Archetypes.Aom2.Archetype.Definition"/>,
    /// materialises one <see cref="TemplateNode"/> per addressable
    /// constraint child, and builds the FLAT-path index used by
    /// <see cref="TryResolveType"/>. <paramref name="rmBmm"/> is
    /// consulted to mark <see cref="TemplateRmTypeResolution.IsPolymorphic"/>
    /// nodes whose declared RM property type has known subclasses.
    /// </summary>
    /// <remarks>
    /// Safe to call more than once; the most recent invocation wins.
    /// </remarks>
    public void Initialize(BmmModel rmBmm)
    {
        ArgumentNullException.ThrowIfNull(rmBmm);

        List<TemplateNode> nodes = [];
        Dictionary<string, TemplateRmTypeResolution> index = new(StringComparer.Ordinal);

        if (Definition is { } root && !string.IsNullOrEmpty(root.RmTypeName))
        {
            string templateId = TemplateId;
            string rootAql = "/";
            string rootFlat = templateId;
            TemplateOccurrence rootOcc = ToOccurrence(root.Occurrences) ?? new TemplateOccurrence(1, 1);
            TemplateNode rootNode = new(rootAql, rootFlat, root.RmTypeName, rootOcc);
            nodes.Add(rootNode);
            index[rootFlat] = new TemplateRmTypeResolution(root.RmTypeName, IsPolymorphic: false);

            Walk(root, rootAql, rootFlat, rmBmm, nodes, index);
        }

        _nodes = nodes.ToFrozenSet();
        _index = index.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static void Walk(
        CComplexObject parent,
        string parentAql,
        string parentFlat,
        BmmModel rmBmm,
        List<TemplateNode> nodes,
        Dictionary<string, TemplateRmTypeResolution> index)
    {
        string parentRmType = parent.RmTypeName ?? string.Empty;
        foreach (CAttribute attr in parent.Attributes)
        {
            if (string.IsNullOrEmpty(attr.RmAttributeName))
            {
                continue;
            }

            bool polymorphic = IsPolymorphicAttribute(rmBmm, parentRmType, attr.RmAttributeName);

            foreach (CObject child in attr.Children)
            {
                if (child is null || string.IsNullOrEmpty(child.RmTypeName))
                {
                    continue;
                }

                string childAql = parentAql.EndsWith('/')
                    ? parentAql + AqlSegment(attr.RmAttributeName, child.NodeId)
                    : parentAql + "/" + AqlSegment(attr.RmAttributeName, child.NodeId);
                string childFlat = parentFlat.Length == 0
                    ? attr.RmAttributeName
                    : parentFlat + "/" + attr.RmAttributeName.ToLowerInvariant();

                TemplateOccurrence occ = ToOccurrence(child.Occurrences)
                    ?? DefaultOccurrence(attr);

                TemplateNode node = new(childAql, childFlat, child.RmTypeName, occ);
                nodes.Add(node);
                index[childFlat] = new TemplateRmTypeResolution(child.RmTypeName, polymorphic);

                if (child is CComplexObject inner)
                {
                    Walk(inner, childAql, childFlat, rmBmm, nodes, index);
                }
            }
        }
    }

    private static string AqlSegment(string attrName, string? nodeId)
        => string.IsNullOrEmpty(nodeId) ? attrName : $"{attrName}[{nodeId}]";

    private static TemplateOccurrence? ToOccurrence(Interval<int>? interval)
    {
        if (interval is null)
        {
            return null;
        }
        int min = interval.HasLower ? interval.Lower : 0;
        int max = interval.HasUpper ? interval.Upper : TemplateOccurrence.Unbounded;
        if (max < 0)
        {
            max = TemplateOccurrence.Unbounded;
        }
        return new TemplateOccurrence(min, max);
    }

    private static TemplateOccurrence DefaultOccurrence(CAttribute attr)
    {
        if (attr is CMultipleAttribute)
        {
            return new TemplateOccurrence(0, TemplateOccurrence.Unbounded);
        }
        return new TemplateOccurrence(1, 1);
    }

    private static bool IsPolymorphicAttribute(BmmModel rmBmm, string parentRmType, string attributeName)
    {
        if (string.IsNullOrEmpty(parentRmType) || string.IsNullOrEmpty(attributeName))
        {
            return false;
        }

        BmmClass? cls = rmBmm.GetClass(parentRmType);
        BmmProperty? prop = null;
        while (cls is not null)
        {
            if (cls.Properties.TryGetValue(attributeName, out prop))
            {
                break;
            }
            cls = cls.Ancestors.Count > 0 ? rmBmm.GetClass(cls.Ancestors[0]) : null;
        }
        if (prop is null)
        {
            return false;
        }

        string declaredType = ExtractElementTypeName(prop.Type);
        return HasSubtypes(rmBmm, declaredType);
    }

    private static string ExtractElementTypeName(BmmType type)
    {
        if (type is BmmContainerType container && container.TypeArguments.Count > 0)
        {
            return ExtractElementTypeName(container.TypeArguments[0]);
        }
        if (type is BmmGenericType generic && generic.TypeArguments.Count > 0)
        {
            // Use the root generic name (e.g. INTERVAL for INTERVAL<DV_QUANTITY>);
            // the inner argument is the bound, not the polymorphism target.
            return generic.TypeName;
        }
        return type.TypeName;
    }

    private static bool HasSubtypes(BmmModel rmBmm, string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return false;
        }
        foreach (KeyValuePair<string, BmmClass> kvp in rmBmm.ClassDefinitions)
        {
            foreach (string ancestor in kvp.Value.Ancestors)
            {
                if (string.Equals(ancestor, typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
