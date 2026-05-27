using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using DotnetOpenEhr.Foundation;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;

namespace DotnetOpenEhr.Templates.Validation;

/// <summary>
/// Walks a <see cref="Composition"/> against an <see cref="OperationalTemplate"/>
/// in lock-step and emits structural, cardinality, and occurrences
/// findings. Data-type constraints on primitive leaves are out of
/// scope for this validator — Phase 8c adds those.
/// </summary>
/// <remarks>
/// The walk descends only as far as the RM tree of <see cref="Locatable"/>
/// nodes; it stops at <see cref="Element"/> (data-value leaves are
/// validated by the data-type validator). Cancellation is checked at
/// the top of every <see cref="Locatable"/> visit.
/// </remarks>
public sealed class OperationalTemplateValidator
{
    /// <summary>
    /// Validates <paramref name="composition"/> against
    /// <paramref name="template"/> and returns the (possibly empty)
    /// list of findings.
    /// </summary>
    /// <exception cref="OperationCanceledException">
    /// Thrown if <paramref name="ct"/> is cancelled before or during
    /// the walk.
    /// </exception>
    public IReadOnlyList<ValidationIssue> Validate(
        Composition composition,
        OperationalTemplate template,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(composition);
        return Validate((Locatable)composition, template, ct);
    }

    /// <summary>
    /// Validates an arbitrary <see cref="Locatable"/> root against the
    /// template's root <c>Definition</c>. Useful when a template is
    /// rooted at a non-COMPOSITION RM type (e.g. a standalone
    /// OBSERVATION template).
    /// </summary>
    public IReadOnlyList<ValidationIssue> Validate(
        Locatable root,
        OperationalTemplate template,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(template);
        ct.ThrowIfCancellationRequested();

        List<ValidationIssue> issues = [];
        CComplexObject? rootTemplate = template.Definition;
        if (rootTemplate is null)
        {
            return issues;
        }

        Walk(root, rootTemplate, "/", issues, ct);
        return issues;
    }

    private static void Walk(
        Locatable node,
        CComplexObject templateNode,
        string aqlPath,
        List<ValidationIssue> issues,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (node is Element)
        {
            return;
        }

        foreach (CAttribute attr in templateNode.Attributes)
        {
            if (string.IsNullOrEmpty(attr.RmAttributeName))
            {
                continue;
            }

            List<Locatable> children = GetLocatableChildren(node, attr.RmAttributeName);

            if (attr is CMultipleAttribute multipleAttr && multipleAttr.Cardinality is { } cardinality)
            {
                CheckCardinality(aqlPath, attr.RmAttributeName, children.Count, cardinality.Interval, issues);
            }

            CheckOccurrences(aqlPath, attr, children, issues);

            foreach (Locatable child in children)
            {
                ct.ThrowIfCancellationRequested();

                string childNodeId = child.ArchetypeNodeId ?? string.Empty;
                string childAql = AppendSegment(aqlPath, attr.RmAttributeName, childNodeId);

                CObject? matched = FindChildByNodeId(attr, childNodeId);
                if (matched is null)
                {
                    issues.Add(new ValidationIssue(
                        childAql,
                        ValidationRuleIds.NodeNotInTemplate,
                        ValidationSeverity.Error,
                        $"Composition node '{childNodeId}' has no matching constraint under attribute '{attr.RmAttributeName}' in the template."));
                    continue;
                }

                if (matched is CComplexObject inner)
                {
                    Walk(child, inner, childAql, issues, ct);
                }
            }
        }
    }

    private static void CheckCardinality(
        string aqlPath,
        string attrName,
        int actual,
        Interval<int> interval,
        List<ValidationIssue> issues)
    {
        if (interval.Contains(actual))
        {
            return;
        }

        issues.Add(new ValidationIssue(
            AppendAttribute(aqlPath, attrName),
            ValidationRuleIds.CardinalityViolation,
            ValidationSeverity.Error,
            $"Cardinality violation on attribute '{attrName}': expected {FormatInterval(interval)}, found {actual}."));
    }

    private static void CheckOccurrences(
        string aqlPath,
        CAttribute attr,
        List<Locatable> children,
        List<ValidationIssue> issues)
    {
        foreach (CObject childTemplate in attr.Children)
        {
            if (childTemplate.Occurrences is not { } occurrences)
            {
                continue;
            }

            string nodeId = childTemplate.NodeId ?? string.Empty;
            int count = 0;
            foreach (Locatable c in children)
            {
                if (string.Equals(c.ArchetypeNodeId, nodeId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            if (occurrences.Contains(count))
            {
                continue;
            }

            string childAql = AppendSegment(aqlPath, attr.RmAttributeName, nodeId);
            issues.Add(new ValidationIssue(
                childAql,
                ValidationRuleIds.OccurrencesViolation,
                ValidationSeverity.Error,
                $"Occurrences violation at '{childAql}': expected {FormatInterval(occurrences)}, found {count}."));
        }
    }

    private static CObject? FindChildByNodeId(CAttribute attr, string nodeId)
    {
        foreach (CObject co in attr.Children)
        {
            if (string.Equals(co.NodeId, nodeId, StringComparison.Ordinal))
            {
                return co;
            }
        }
        return null;
    }

    private static string AppendAttribute(string parent, string attrName)
        => parent.EndsWith('/') ? parent + attrName : parent + "/" + attrName;

    private static string AppendSegment(string parent, string attrName, string nodeId)
    {
        string segment = string.IsNullOrEmpty(nodeId) ? attrName : $"{attrName}[{nodeId}]";
        return parent.EndsWith('/') ? parent + segment : parent + "/" + segment;
    }

    private static string FormatInterval(Interval<int> interval)
    {
        string lower = interval.HasLower ? interval.Lower.ToString() : "*";
        string upper = interval.HasUpper ? interval.Upper.ToString() : "*";
        return $"{lower}..{upper}";
    }

    private static List<Locatable> GetLocatableChildren(Locatable parent, string rmAttributeName)
    {
        switch (parent)
        {
            case Composition c:
                return rmAttributeName switch
                {
                    "content" => ToList(c.Content),
                    _ => [],
                };
            case Section s:
                return rmAttributeName switch
                {
                    "items" => ToList(s.Items),
                    _ => [],
                };
            case Observation o:
                return rmAttributeName switch
                {
                    "data" => Singleton(o.Data),
                    "state" => Singleton(o.State),
                    "protocol" => Singleton(o.Protocol),
                    _ => [],
                };
            case Evaluation e:
                return rmAttributeName switch
                {
                    "data" => Singleton(e.Data),
                    "protocol" => Singleton(e.Protocol),
                    _ => [],
                };
            case Instruction i:
                return rmAttributeName switch
                {
                    "activities" => ToList(i.Activities),
                    "protocol" => Singleton(i.Protocol),
                    _ => [],
                };
            case Rm.Composition.Action a:
                return rmAttributeName switch
                {
                    "description" => Singleton(a.Description),
                    "protocol" => Singleton(a.Protocol),
                    _ => [],
                };
            case AdminEntry ae:
                return rmAttributeName switch
                {
                    "data" => Singleton(ae.Data),
                    _ => [],
                };
            case Activity act:
                return rmAttributeName switch
                {
                    "description" => Singleton(act.Description),
                    _ => [],
                };
            case History h:
                return rmAttributeName switch
                {
                    "events" => ToList(h.Events),
                    "summary" => Singleton(h.Summary),
                    _ => [],
                };
            case Event ev:
                return rmAttributeName switch
                {
                    "data" => Singleton(ev.Data),
                    "state" => Singleton(ev.State),
                    _ => [],
                };
            case ItemTree it:
                return rmAttributeName switch
                {
                    "items" => ToList(it.Items),
                    _ => [],
                };
            case ItemList il:
                return rmAttributeName switch
                {
                    "items" => ToList(il.Items),
                    _ => [],
                };
            case ItemSingle isng:
                return rmAttributeName switch
                {
                    "item" => Singleton(isng.Item),
                    _ => [],
                };
            case ItemTable itab:
                return rmAttributeName switch
                {
                    "rows" => ToList(itab.Rows),
                    _ => [],
                };
            case Cluster cl:
                return rmAttributeName switch
                {
                    "items" => ToList(cl.Items),
                    _ => [],
                };
            default:
                return [];
        }
    }

    private static List<Locatable> Singleton<T>(T? value) where T : Locatable
        => value is null ? [] : [value];

    private static List<Locatable> ToList<T>(IEnumerable<T>? source) where T : Locatable
    {
        if (source is null)
        {
            return [];
        }
        List<Locatable> result = [];
        foreach (T item in source)
        {
            if (item is not null)
            {
                result.Add(item);
            }
        }
        return result;
    }
}
