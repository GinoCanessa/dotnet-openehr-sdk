using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using DotnetOpenEhr.Archetypes.Aom2.Terminology;
using DotnetOpenEhr.Foundation;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes;
using DotnetOpenEhr.Rm.DataTypes.Basic;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;

namespace DotnetOpenEhr.Templates.Validation;

/// <summary>
/// Walks a <see cref="Composition"/> against an <see cref="OperationalTemplate"/>
/// in lock-step and emits structural, cardinality, occurrences, and
/// data-type-constraint findings.
/// </summary>
/// <remarks>
/// The walk descends as far as <see cref="Element"/> leaves; at each
/// <see cref="Element"/> the corresponding template <c>value</c>
/// attribute is consulted and the carried <see cref="DataValue"/> is
/// matched against the primitive / second-order constraint shapes
/// emitted by the ADL2 parser. Cancellation is checked at the top of
/// every <see cref="Locatable"/> visit.
/// </remarks>
public sealed class OperationalTemplateValidator
{
    private readonly OperationalTemplateValidatorOptions _options;
    private readonly ConcurrentDictionary<(string Pattern, TimeSpan Timeout), Regex> _regexCache;

    // H8 — process-global default regex cache: only successfully
    // compiled patterns are added, so a malformed pattern submitted N
    // times does not poison the cache. Keyed on (pattern, timeout) so
    // different validator instances with different timeout postures
    // share entries safely. Process-global; bounded by the number of
    // distinct valid (pattern, timeout) pairs across loaded templates
    // (O(100s) in realistic workloads). This static is reached only
    // when the caller does not supply
    // <see cref="OperationalTemplateValidatorOptions.RegexCache"/>;
    // when they do, the validator uses that dictionary instead. The
    // configured timeout is read from the per-instance
    // <c>_options.RegexMatchTimeout</c> directly inside
    // <see cref="ValidateString"/>; there is no thread-static plumbing.
    private static readonly ConcurrentDictionary<(string Pattern, TimeSpan Timeout), Regex> s_defaultRegexCache = new();

    /// <summary>
    /// Creates a validator with the default
    /// <see cref="OperationalTemplateValidatorOptions"/>.
    /// </summary>
    public OperationalTemplateValidator()
        : this(new OperationalTemplateValidatorOptions())
    {
    }

    /// <summary>
    /// Creates a validator with the supplied options.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="options"/>'s <c>RegexMatchTimeout</c> is negative.
    /// </exception>
    public OperationalTemplateValidator(OperationalTemplateValidatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.RegexMatchTimeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.RegexMatchTimeout,
                "RegexMatchTimeout must be non-negative; use TimeSpan.Zero to opt out.");
        }
        _options = options;
        _regexCache = options.RegexCache ?? s_defaultRegexCache;
    }
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

        Walk(root, rootTemplate, "/", template, issues, ct);
        return issues;
    }

    private void Walk(
        Locatable node,
        CComplexObject templateNode,
        string aqlPath,
        OperationalTemplate template,
        List<ValidationIssue> issues,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (node is Element element)
        {
            ValidateElementValue(element, templateNode, aqlPath, template, issues);
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
                    Walk(child, inner, childAql, template, issues, ct);
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

    // ------------------------------------------------------------------
    // Data-type constraint validation
    // ------------------------------------------------------------------

    // Deviation: the ADL2 parser materialises DV_QUANTITY / DV_CODED_TEXT /
    // DV_ORDINAL / DV_DATE / DV_TIME / DV_DATE_TIME / DV_DURATION
    // constraints as a CComplexObject with attribute children
    // (magnitude, units, value, defining_code, ...). The AOM2
    // second-order classes CDvQuantity / CDvOrdinal are not produced
    // today, so the validator pattern-matches on the RmTypeName of the
    // value-attribute child and interprets the inner attribute set.

    private void ValidateElementValue(
        Element element,
        CComplexObject templateNode,
        string elementPath,
        OperationalTemplate template,
        List<ValidationIssue> issues)
    {
        DataValue? value = element.Value;
        if (value is null)
        {
            return;
        }

        foreach (CAttribute attr in templateNode.Attributes)
        {
            if (!string.Equals(attr.RmAttributeName, "value", StringComparison.Ordinal))
            {
                continue;
            }
            foreach (CObject co in attr.Children)
            {
                string valuePath = AppendAttribute(elementPath, "value");
                ValidateDataValue(value, co, valuePath, template, issues);
            }
        }
    }

    private void ValidateDataValue(
        DataValue value,
        CObject constraint,
        string path,
        OperationalTemplate template,
        List<ValidationIssue> issues)
    {
        switch (constraint)
        {
            case CComplexObject complex:
                ValidateDataValueAsComplex(value, complex, path, template, issues);
                break;
            case CDvQuantity dvq when value is DvQuantity q:
                ValidateDvQuantity(q, dvq, path, issues);
                break;
            case CDvOrdinal dvo when value is DvOrdinal o:
                ValidateDvOrdinalAom2(o, dvo, path, issues);
                break;
            case CTerminologyCode ctc when value is DvCodedText ct:
                ValidateCodedText(ct.DefiningCode, ctc, path, template, issues);
                break;
            case CString cs when value is DvText t:
                ValidateString(t.Value, cs, path, issues);
                break;
        }
    }

    private void ValidateDataValueAsComplex(
        DataValue value,
        CComplexObject complex,
        string path,
        OperationalTemplate template,
        List<ValidationIssue> issues)
    {
        switch (value)
        {
            case DvQuantity q:
                ValidateDvQuantityFromComplex(q, complex, path, issues);
                break;
            case DvCount c:
                ValidateAttribute(complex, "magnitude", path, attrPath =>
                    ForEachChild(complex, "magnitude", child =>
                        ValidateLong(c.Magnitude, child, attrPath, issues)));
                break;
            case DvProportion p:
                ValidateAttribute(complex, "numerator", path, attrPath =>
                    ForEachChild(complex, "numerator", child =>
                        ValidateDouble(p.Numerator, child, attrPath, issues)));
                ValidateAttribute(complex, "denominator", path, attrPath =>
                    ForEachChild(complex, "denominator", child =>
                        ValidateDouble(p.Denominator, child, attrPath, issues)));
                break;
            case DvOrdinal o:
                ValidateAttribute(complex, "value", path, attrPath =>
                    ForEachChild(complex, "value", child =>
                        ValidateOrdinalValue(o.Value, child, attrPath, issues)));
                break;
            case DvCodedText ct:
                ValidateAttribute(complex, "defining_code", path, attrPath =>
                    ForEachChild(complex, "defining_code", child =>
                    {
                        if (child is CTerminologyCode tc)
                        {
                            ValidateCodedText(ct.DefiningCode, tc, attrPath, template, issues);
                        }
                    }));
                break;
            case DvText t:
                ValidateAttribute(complex, "value", path, attrPath =>
                    ForEachChild(complex, "value", child =>
                    {
                        if (child is CString cs)
                        {
                            ValidateString(t.Value, cs, attrPath, issues);
                        }
                    }));
                break;
            case DvDate d:
                ValidateLexicalTemporal(d.Value.OriginalLexicalForm, complex, path, issues);
                break;
            case DvTime tm:
                ValidateLexicalTemporal(tm.Value.OriginalLexicalForm, complex, path, issues);
                break;
            case DvDateTime dt:
                ValidateLexicalTemporal(dt.Value.OriginalLexicalForm, complex, path, issues);
                break;
            case DvDuration du:
                ValidateLexicalTemporal(du.Value.OriginalLexicalForm, complex, path, issues);
                break;
            case DvBoolean:
            case DvIdentifier:
            case DvState:
                // No primitive constraints implemented yet for these.
                break;
        }
    }

    private static void ForEachChild(CComplexObject complex, string attrName, Action<CObject> action)
    {
        foreach (CAttribute attr in complex.Attributes)
        {
            if (!string.Equals(attr.RmAttributeName, attrName, StringComparison.Ordinal))
            {
                continue;
            }
            foreach (CObject co in attr.Children)
            {
                action(co);
            }
        }
    }

    private static void ValidateAttribute(
        CComplexObject complex,
        string attrName,
        string parentPath,
        Action<string> body)
    {
        body(AppendAttribute(parentPath, attrName));
    }

    private static void ValidateDvQuantityFromComplex(
        DvQuantity q,
        CComplexObject complex,
        string path,
        List<ValidationIssue> issues)
    {
        // Units → CString.EnumeratedValues
        foreach (CAttribute attr in complex.Attributes)
        {
            if (string.Equals(attr.RmAttributeName, "units", StringComparison.Ordinal))
            {
                string unitsPath = AppendAttribute(path, "units");
                foreach (CObject co in attr.Children)
                {
                    if (co is CString cs && cs.EnumeratedValues is { Count: > 0 } allowed)
                    {
                        if (!allowed.Contains(q.Units))
                        {
                            issues.Add(new ValidationIssue(
                                unitsPath,
                                ValidationRuleIds.QuantityWrongUnits,
                                ValidationSeverity.Error,
                                $"DvQuantity units '{q.Units}' is not one of the permitted units [{string.Join(", ", allowed)}]."));
                        }
                    }
                }
            }
            else if (string.Equals(attr.RmAttributeName, "magnitude", StringComparison.Ordinal))
            {
                string magPath = AppendAttribute(path, "magnitude");
                foreach (CObject co in attr.Children)
                {
                    if (co is CReal cr)
                    {
                        if (cr.Range is { } range && !range.Contains(q.Magnitude))
                        {
                            issues.Add(new ValidationIssue(
                                magPath,
                                ValidationRuleIds.QuantityMagnitudeOutOfRange,
                                ValidationSeverity.Error,
                                $"DvQuantity magnitude {q.Magnitude.ToString(System.Globalization.CultureInfo.InvariantCulture)} is outside permitted range {range}."));
                        }
                        if (cr.EnumeratedValues is { Count: > 0 } list && !list.Contains(q.Magnitude))
                        {
                            issues.Add(new ValidationIssue(
                                magPath,
                                ValidationRuleIds.QuantityMagnitudeOutOfRange,
                                ValidationSeverity.Error,
                                $"DvQuantity magnitude {q.Magnitude.ToString(System.Globalization.CultureInfo.InvariantCulture)} is not in permitted set."));
                        }
                    }
                }
            }
            else if (string.Equals(attr.RmAttributeName, "precision", StringComparison.Ordinal))
            {
                if (q.Precision is not { } precision)
                {
                    continue;
                }
                string precPath = AppendAttribute(path, "precision");
                foreach (CObject co in attr.Children)
                {
                    if (co is CInteger ci)
                    {
                        if (ci.Range is { } range && !range.Contains(precision))
                        {
                            issues.Add(new ValidationIssue(
                                precPath,
                                ValidationRuleIds.QuantityPrecisionOutOfRange,
                                ValidationSeverity.Error,
                                $"DvQuantity precision {precision} is outside permitted range {range}."));
                        }
                        if (ci.EnumeratedValues is { Count: > 0 } list && !list.Contains(precision))
                        {
                            issues.Add(new ValidationIssue(
                                precPath,
                                ValidationRuleIds.QuantityPrecisionOutOfRange,
                                ValidationSeverity.Error,
                                $"DvQuantity precision {precision} is not in permitted set [{string.Join(", ", list)}]."));
                        }
                    }
                }
            }
        }
    }

    private static void ValidateDvQuantity(
        DvQuantity q,
        CDvQuantity constraint,
        string path,
        List<ValidationIssue> issues)
    {
        if (constraint.Items.Count == 0)
        {
            return;
        }

        CQuantityItem? matchedItem = null;
        foreach (CQuantityItem item in constraint.Items)
        {
            if (string.Equals(item.Units, q.Units, StringComparison.Ordinal))
            {
                matchedItem = item;
                break;
            }
        }

        if (matchedItem is null)
        {
            List<string> units = [];
            foreach (CQuantityItem it in constraint.Items)
            {
                units.Add(it.Units);
            }
            issues.Add(new ValidationIssue(
                AppendAttribute(path, "units"),
                ValidationRuleIds.QuantityWrongUnits,
                ValidationSeverity.Error,
                $"DvQuantity units '{q.Units}' is not one of the permitted units [{string.Join(", ", units)}]."));
            return;
        }

        if (matchedItem.Magnitude is { } magRange && !magRange.Contains(q.Magnitude))
        {
            issues.Add(new ValidationIssue(
                AppendAttribute(path, "magnitude"),
                ValidationRuleIds.QuantityMagnitudeOutOfRange,
                ValidationSeverity.Error,
                $"DvQuantity magnitude {q.Magnitude.ToString(System.Globalization.CultureInfo.InvariantCulture)} is outside permitted range {magRange} for units '{q.Units}'."));
        }

        if (matchedItem.Precision is { } precRange && q.Precision is { } precision && !precRange.Contains(precision))
        {
            issues.Add(new ValidationIssue(
                AppendAttribute(path, "precision"),
                ValidationRuleIds.QuantityPrecisionOutOfRange,
                ValidationSeverity.Error,
                $"DvQuantity precision {precision} is outside permitted range {precRange} for units '{q.Units}'."));
        }
    }

    private static void ValidateDvOrdinalAom2(
        DvOrdinal o,
        CDvOrdinal constraint,
        string path,
        List<ValidationIssue> issues)
    {
        if (constraint.Items.Count == 0)
        {
            return;
        }
        foreach (CDvOrdinalItem item in constraint.Items)
        {
            if (item.Value == o.Value)
            {
                return;
            }
        }
        List<string> allowed = [];
        foreach (CDvOrdinalItem item in constraint.Items)
        {
            allowed.Add(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        issues.Add(new ValidationIssue(
            AppendAttribute(path, "value"),
            ValidationRuleIds.OrdinalNotInSet,
            ValidationSeverity.Error,
            $"DvOrdinal value {o.Value} is not in the permitted set [{string.Join(", ", allowed)}]."));
    }

    private static void ValidateOrdinalValue(
        int actual,
        CObject child,
        string path,
        List<ValidationIssue> issues)
    {
        if (child is CInteger ci && ci.EnumeratedValues is { Count: > 0 } allowed)
        {
            if (!allowed.Contains(actual))
            {
                issues.Add(new ValidationIssue(
                    path,
                    ValidationRuleIds.OrdinalNotInSet,
                    ValidationSeverity.Error,
                    $"DvOrdinal value {actual} is not in the permitted set [{string.Join(", ", allowed)}]."));
            }
        }
    }

    private void ValidateString(
        string actual,
        CString constraint,
        string path,
        List<ValidationIssue> issues)
    {
        if (!string.IsNullOrEmpty(constraint.Pattern))
        {
            // H8 — cache successfully-compiled regexes only; malformed
            // patterns emit NotValidated without poisoning the cache.
            TimeSpan timeout = _options.RegexMatchTimeout;

            Regex? rx;
            try
            {
                rx = _regexCache.GetOrAdd(
                    (constraint.Pattern!, timeout),
                    static key => new Regex(
                        key.Pattern,
                        RegexOptions.Compiled | RegexOptions.CultureInvariant,
                        key.Timeout == TimeSpan.Zero ? Regex.InfiniteMatchTimeout : key.Timeout));
            }
            catch (ArgumentException)
            {
                // RegexParseException is an ArgumentException subtype on
                // older runtimes; both arrive here. Don't poison the
                // cache — GetOrAdd's static factory throws before insert.
                issues.Add(new ValidationIssue(
                    path,
                    ValidationRuleIds.StringPatternViolation,
                    ValidationSeverity.NotValidated,
                    $"Pattern /{constraint.Pattern}/ failed to compile; the rule cannot be evaluated."));
                return;
            }

            try
            {
                if (!rx.IsMatch(actual))
                {
                    issues.Add(new ValidationIssue(
                        path,
                        ValidationRuleIds.StringPatternViolation,
                        ValidationSeverity.Error,
                        $"Value '{actual}' does not match pattern /{constraint.Pattern}/."));
                }
            }
            catch (RegexMatchTimeoutException)
            {
                issues.Add(new ValidationIssue(
                    path,
                    ValidationRuleIds.StringPatternViolation,
                    ValidationSeverity.NotValidated,
                    $"Pattern /{constraint.Pattern}/ exceeded the configured match timeout ({timeout}); the rule cannot be evaluated."));
            }
            return;
        }
        if (constraint.EnumeratedValues is { Count: > 0 } allowed)
        {
            if (!allowed.Contains(actual))
            {
                issues.Add(new ValidationIssue(
                    path,
                    ValidationRuleIds.StringNotInEnumeration,
                    ValidationSeverity.Error,
                    $"Value '{actual}' is not in permitted set [{string.Join(", ", allowed)}]."));
            }
        }
    }

    private static void ValidateLong(
        long actual,
        CObject child,
        string path,
        List<ValidationIssue> issues)
    {
        if (child is CInteger ci)
        {
            // ValidateInteger operates on int — the CInteger constraint
            // type itself is `int`-ranged. If the long value falls
            // outside int range, the constraint can't possibly admit
            // it; emit NumericOutOfRange directly rather than the old
            // `checked((int)actual)` which threw OverflowException.
            if (actual < int.MinValue || actual > int.MaxValue)
            {
                string rangeText = ci.Range is { } r
                    ? r.ToString()
                    : $"[{int.MinValue},{int.MaxValue}]";
                issues.Add(new ValidationIssue(
                    path,
                    ValidationRuleIds.NumericOutOfRange,
                    ValidationSeverity.Error,
                    $"Value {actual.ToString(System.Globalization.CultureInfo.InvariantCulture)} "
                        + $"is outside permitted range {rangeText}."));
                return;
            }
            ValidateInteger((int)actual, ci, path, issues);
        }
        else if (child is CReal cr)
        {
            ValidateDouble(actual, cr, path, issues);
        }
    }

    private static void ValidateDouble(
        double actual,
        CObject child,
        string path,
        List<ValidationIssue> issues)
    {
        if (child is CReal cr)
        {
            if (cr.Range is { } range && !range.Contains(actual))
            {
                issues.Add(new ValidationIssue(
                    path,
                    ValidationRuleIds.NumericOutOfRange,
                    ValidationSeverity.Error,
                    $"Value {actual.ToString(System.Globalization.CultureInfo.InvariantCulture)} is outside permitted range {range}."));
            }
            if (cr.EnumeratedValues is { Count: > 0 } list && !list.Contains(actual))
            {
                issues.Add(new ValidationIssue(
                    path,
                    ValidationRuleIds.NumericNotInEnumeration,
                    ValidationSeverity.Error,
                    $"Value {actual.ToString(System.Globalization.CultureInfo.InvariantCulture)} is not in permitted set [{string.Join(", ", list)}]."));
            }
        }
        else if (child is CInteger ci)
        {
            ValidateInteger((int)actual, ci, path, issues);
        }
    }

    private static void ValidateInteger(
        int actual,
        CInteger ci,
        string path,
        List<ValidationIssue> issues)
    {
        if (ci.Range is { } range && !range.Contains(actual))
        {
            issues.Add(new ValidationIssue(
                path,
                ValidationRuleIds.NumericOutOfRange,
                ValidationSeverity.Error,
                $"Value {actual} is outside permitted range {range}."));
        }
        if (ci.EnumeratedValues is { Count: > 0 } list && !list.Contains(actual))
        {
            issues.Add(new ValidationIssue(
                path,
                ValidationRuleIds.NumericNotInEnumeration,
                ValidationSeverity.Error,
                $"Value {actual} is not in permitted set [{string.Join(", ", list)}]."));
        }
    }

    private static void ValidateLexicalTemporal(
        string lexical,
        CComplexObject complex,
        string path,
        List<ValidationIssue> issues)
    {
        foreach (CAttribute attr in complex.Attributes)
        {
            if (!string.Equals(attr.RmAttributeName, "value", StringComparison.Ordinal))
            {
                continue;
            }
            string valuePath = AppendAttribute(path, "value");
            foreach (CObject co in attr.Children)
            {
                // CString.Pattern is the inner value of an ADL2 /regex/
                // literal — match as a proper regex. The dedicated
                // CDate / CTime / CDateTime / CDuration classes carry
                // partial-precision patterns (e.g. "yyyy-mm-??") and
                // use the simpler positional matcher.
                if (co is CString cs)
                {
                    if (!string.IsNullOrEmpty(cs.Pattern))
                    {
                        if (!MatchRegex(cs.Pattern!, lexical))
                        {
                            issues.Add(new ValidationIssue(
                                valuePath,
                                ValidationRuleIds.DateTimePatternViolation,
                                ValidationSeverity.Error,
                                $"Value '{lexical}' does not match temporal pattern '{cs.Pattern}'."));
                        }
                    }
                    if (cs.EnumeratedValues is { Count: > 0 } allowed && !allowed.Contains(lexical))
                    {
                        issues.Add(new ValidationIssue(
                            valuePath,
                            ValidationRuleIds.DateTimePatternViolation,
                            ValidationSeverity.Error,
                            $"Value '{lexical}' is not in permitted set [{string.Join(", ", allowed)}]."));
                    }
                    continue;
                }

                string? partialPattern = co switch
                {
                    CDate cd => cd.Pattern,
                    CTime cti => cti.Pattern,
                    CDateTime cdt => cdt.Pattern,
                    CDuration cdu => cdu.Pattern,
                    _ => null,
                };
                if (!string.IsNullOrEmpty(partialPattern)
                    && !MatchPartialPattern(partialPattern!, lexical))
                {
                    issues.Add(new ValidationIssue(
                        valuePath,
                        ValidationRuleIds.DateTimePatternViolation,
                        ValidationSeverity.Error,
                        $"Value '{lexical}' does not match temporal pattern '{partialPattern}'."));
                }
            }
        }
    }

    private static bool MatchRegex(string pattern, string actual)
    {
        try
        {
            Regex rx = new(pattern, RegexOptions.CultureInvariant);
            return rx.IsMatch(actual);
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    // Partial-precision pattern: characters are literal except '?' which
    // matches any single digit and 'X' which means "field disallowed at
    // this position" (treated as literal here, the openEHR spec models
    // disallowed precision differently; the simple matcher covers the
    // common partial-precision case used in templates).
    private static bool MatchPartialPattern(string pattern, string actual)
    {
        if (actual.Length != pattern.Length)
        {
            // Allow actual to be a prefix-truncated form when the pattern
            // ends with optional fields ("?" segments). Strict equality
            // check first; fall back to a per-char check trimmed to the
            // shorter length.
            int len = Math.Min(pattern.Length, actual.Length);
            for (int i = 0; i < len; i++)
            {
                if (!MatchChar(pattern[i], actual[i]))
                {
                    return false;
                }
            }
            // If pattern is longer, remaining pattern chars must be all '?'.
            for (int i = len; i < pattern.Length; i++)
            {
                if (pattern[i] != '?')
                {
                    return false;
                }
            }
            return true;
        }
        for (int i = 0; i < pattern.Length; i++)
        {
            if (!MatchChar(pattern[i], actual[i]))
            {
                return false;
            }
        }
        return true;
    }

    private static bool MatchChar(char pat, char actual)
    {
        if (pat == '?')
        {
            return char.IsDigit(actual);
        }
        // Letters 'y', 'm', 'd', 'h', 's' in patterns also stand for digit positions.
        if (pat is 'y' or 'M' or 'd' or 'h' or 'H' or 'm' or 's')
        {
            return char.IsDigit(actual);
        }
        return pat == actual;
    }

    private static void ValidateCodedText(
        CodePhrase definingCode,
        CTerminologyCode constraint,
        string path,
        OperationalTemplate template,
        List<ValidationIssue> issues)
    {
        // External binding takes precedence: emit NotValidated and skip
        // the local membership check, because the local value-set may be
        // absent or stale once bound externally.
        if (HasExternalBinding(template, constraint))
        {
            issues.Add(new ValidationIssue(
                path,
                ValidationRuleIds.BindingNotResolved,
                ValidationSeverity.NotValidated,
                $"Coded value at this position has an external terminology binding; binding resolution is out of scope for the validator."));
            return;
        }

        if (!string.IsNullOrEmpty(constraint.ValueSetRef))
        {
            ValueSet? vs = ResolveValueSet(template, constraint.ValueSetRef!);
            if (vs is null)
            {
                return;
            }
            if (!vs.Members.Contains(definingCode.CodeString))
            {
                issues.Add(new ValidationIssue(
                    path,
                    ValidationRuleIds.CodeNotInValueSet,
                    ValidationSeverity.Error,
                    $"Code '{definingCode.CodeString}' is not in value set '{constraint.ValueSetRef}' (members: [{string.Join(", ", vs.Members)}])."));
            }
            return;
        }

        if (constraint.EnumeratedValues is { Count: > 0 } codes)
        {
            if (!codes.Contains(definingCode.CodeString))
            {
                issues.Add(new ValidationIssue(
                    path,
                    ValidationRuleIds.CodeNotInValueSet,
                    ValidationSeverity.Error,
                    $"Code '{definingCode.CodeString}' is not in permitted set [{string.Join(", ", codes)}]."));
            }
        }
    }

    private static ValueSet? ResolveValueSet(OperationalTemplate template, string valueSetRef)
    {
        if (template.Terminology is { } term
            && term.ValueSets.TryGetValue(valueSetRef, out ValueSet? localVs))
        {
            return localVs;
        }
        foreach (KeyValuePair<DotnetOpenEhr.Archetypes.Identification.ArchetypeHRID, ArchetypeTerminology> kvp in template.ComponentTerminologies)
        {
            if (kvp.Value.ValueSets.TryGetValue(valueSetRef, out ValueSet? compVs))
            {
                return compVs;
            }
        }
        return null;
    }

    private static bool HasExternalBinding(OperationalTemplate template, CTerminologyCode constraint)
    {
        string? key = constraint.ValueSetRef;
        if (string.IsNullOrEmpty(key))
        {
            if (constraint.EnumeratedValues is { Count: > 0 } codes)
            {
                key = codes[0];
            }
        }
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }
        // M4 — explicit parentheses around the term-bindings probe.
        // The pre-fix expression bound && tighter than || (legal C# but
        // hides the writer's intent): the original
        // `term && BindingsContain(term.TermBindings, key)
        //  || BindingsContain(template.Terminology?.ConstraintBindings, key)`
        // evaluated the right-hand side against `template.Terminology?.
        // ConstraintBindings` even when Terminology was null, causing a
        // bare-null lookup. The parenthesised form below collapses both
        // probes to the single non-null `term`.
        if (template.Terminology is { } term
            && (BindingsContain(term.TermBindings, key) || BindingsContain(term.ConstraintBindings, key)))
        {
            return true;
        }
        foreach (KeyValuePair<DotnetOpenEhr.Archetypes.Identification.ArchetypeHRID, ArchetypeTerminology> kvp in template.ComponentTerminologies)
        {
            if (BindingsContain(kvp.Value.TermBindings, key) || BindingsContain(kvp.Value.ConstraintBindings, key))
            {
                return true;
            }
        }
        return false;
    }

    private static bool BindingsContain(
        Dictionary<string, Dictionary<string, string>>? bindings,
        string key)
    {
        if (bindings is null)
        {
            return false;
        }
        foreach (KeyValuePair<string, Dictionary<string, string>> outer in bindings)
        {
            if (outer.Value.ContainsKey(key))
            {
                return true;
            }
        }
        return false;
    }
}
