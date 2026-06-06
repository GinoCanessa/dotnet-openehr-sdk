using System.Collections.Frozen;
using System.Globalization;
using System.Xml.Linq;
using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using DotnetOpenEhr.Foundation;

namespace DotnetOpenEhr.Templates;

/// <summary>
/// OPT1.4 <c>&lt;definition&gt;</c> subtree → AOM2 constraint tree
/// translator. Pure XML walker; produces fully-formed
/// <see cref="CComplexObject"/> / <see cref="CAttribute"/> /
/// <see cref="CObject"/> instances using the
/// <c>xsi:type</c> discriminator on every <c>&lt;attributes&gt;</c>
/// and <c>&lt;children&gt;</c> element to pick a concrete subtype.
/// </summary>
internal static class Opt14DefinitionReader
{
    // Elements that appear in real OPT1.4 fixtures but have no AOM2
    // destination today. They are silently dropped in both strict and
    // lenient modes — see "Known but unmapped" whitelist in plan.md
    // Phase 3 step 5. Keep this list small and grep-able.
    internal static readonly FrozenSet<string> KnownUnmappedElements =
        new[]
        {
            // CAttribute-level metadata (not in AOM2 CAttribute).
            "match_negated",
            // Top-level metadata blocks on <template> / C_ARCHETYPE_ROOT
            // that AOM2's single ArchetypeModelObject? Annotations slot
            // can't faithfully carry. Richer mapping deferred to v2.
            "annotations",
            // C_CODE_REFERENCE / C_DV_QUANTITY decoration that doesn't
            // round-trip into CCodePhrase / CDvQuantity today.
            "referenceSetUri",
            // Constraint metadata absorbed into ArchetypeSlot at the
            // CObject level instead.
            "expression",
            "string_expression",
        }
        .ToFrozenSet(StringComparer.Ordinal);

    internal static CComplexObject Read(
        XElement definitionEl,
        bool lenient,
        List<(XElement Source, CComplexObject Node)> archetypeRootSources)
    {
        // The root <definition> element does not carry an xsi:type
        // discriminator (it is implicitly C_COMPLEX_OBJECT, and
        // conceptually a C_ARCHETYPE_ROOT — see the trailing
        // <archetype_id>/<term_definitions> children). Build it
        // directly: rm_type_name + node_id + occurrences first, then
        // attributes.
        CComplexObject root = new();
        PopulateCObjectCommon(definitionEl, root, lenient);
        ReadAttributesInto(definitionEl, root, lenient, archetypeRootSources);
        // Register the root for Phase 4 terminology harvest.
        archetypeRootSources.Add((definitionEl, root));
        return root;
    }

    // ------------------------------------------------------------------
    // CObject construction by xsi:type. Each builder reads only the
    // fields that distinguish that subtype; the common CObject members
    // (rm_type_name, node_id, occurrences) are populated by the caller
    // via PopulateCObjectCommon.
    // ------------------------------------------------------------------

    private static CObject BuildObject(
        XElement el,
        bool lenient,
        List<(XElement Source, CComplexObject Node)>? archetypeRootSources)
    {
        string? xsi = el.Attribute(Opt14XmlReader.Xsi + "type")?.Value;
        // Strip any "xs:"-style namespace prefix some emitters add.
        string discriminator = xsi is null ? string.Empty : StripPrefix(xsi);

        CObject obj = discriminator switch
        {
            "C_COMPLEX_OBJECT" or "" => BuildComplex(el, lenient, archetypeRootSources),
            "C_ARCHETYPE_ROOT" => BuildArchetypeRoot(el, lenient, archetypeRootSources),
            "ARCHETYPE_SLOT" => BuildArchetypeSlot(el, lenient),
            "ARCHETYPE_INTERNAL_REF" => BuildInternalRef(el, lenient),
            "C_COMPLEX_OBJECT_PROXY" => BuildComplexProxy(el, lenient),
            "C_PRIMITIVE_OBJECT" => BuildPrimitiveWrapper(el, lenient, archetypeRootSources),
            "C_STRING" => ReadCString(el),
            "C_INTEGER" => ReadCInteger(el),
            "C_REAL" => ReadCReal(el),
            "C_BOOLEAN" => ReadCBoolean(el),
            "C_DATE" => ReadCDate(el),
            "C_TIME" => ReadCTime(el),
            "C_DATE_TIME" => ReadCDateTime(el),
            "C_DURATION" => ReadCDuration(el),
            "C_TERMINOLOGY_CODE" => ReadCTerminologyCode(el, lenient),
            "C_DV_QUANTITY" => ReadCDvQuantity(el, lenient),
            "C_DV_ORDINAL" => ReadCDvOrdinal(el, lenient),
            "C_CODE_PHRASE" => ReadCCodePhrase(el, lenient),
            // C_CODE_REFERENCE is OPT1.4-only (constraint by external
            // value-set URI). AOM2 has no dedicated type; the closest
            // match is CCodePhrase. The <referenceSetUri> child is on
            // the known-unmapped whitelist above.
            "C_CODE_REFERENCE" => ReadCCodePhrase(el, lenient),
            // State-machine subgraph: defined in OpenehrProfile.xsd but
            // not modelled in AOM2 today. Strict throws; lenient skips
            // (caller drops the resulting placeholder).
            "C_DV_STATE" or "STATE_MACHINE" or "STATE" or "TRANSITION"
                => throw new NotSupportedException(
                    $"OPT1.4 '{discriminator}' is not modelled in AOM2 v1; deferred to v2 " +
                    $"(line {Opt14XmlReader.LineInfo(el).Line})."),
            _ => throw new InvalidOperationException(
                $"Unknown OPT1.4 xsi:type '{discriminator}' on <{el.Name.LocalName}> " +
                $"(line {Opt14XmlReader.LineInfo(el).Line})."),
        };

        PopulateCObjectCommon(el, obj, lenient);
        return obj;
    }

    private static CComplexObject BuildComplex(
        XElement el,
        bool lenient,
        List<(XElement Source, CComplexObject Node)>? archetypeRootSources)
    {
        CComplexObject cco = new();
        ReadAttributesInto(el, cco, lenient, archetypeRootSources);
        return cco;
    }

    private static CArchetypeRoot BuildArchetypeRoot(
        XElement el,
        bool lenient,
        List<(XElement Source, CComplexObject Node)>? archetypeRootSources)
    {
        CArchetypeRoot root = new();
        string? archetypeRef = Opt14XmlReader.FindChildValue(
            Opt14XmlReader.FindChild(el, "archetype_id", lenient) ?? el, "value", lenient);
        if (!string.IsNullOrEmpty(archetypeRef))
        {
            root.ArchetypeRef = archetypeRef;
        }
        ReadAttributesInto(el, root, lenient, archetypeRootSources);
        return root;
    }

    private static ArchetypeSlot BuildArchetypeSlot(XElement el, bool lenient)
    {
        ArchetypeSlot slot = new();
        foreach (XElement inc in Opt14XmlReader.FindChildren(el, "includes", lenient))
        {
            slot.Includes.Add(ReadAssertion(inc, lenient));
        }
        foreach (XElement exc in Opt14XmlReader.FindChildren(el, "excludes", lenient))
        {
            slot.Excludes.Add(ReadAssertion(exc, lenient));
        }
        string? closed = Opt14XmlReader.FindChildValue(el, "closed", lenient);
        if (!string.IsNullOrEmpty(closed) && bool.TryParse(closed, out bool c))
        {
            slot.IsClosed = c;
        }
        return slot;
    }

    private static ArchetypeInternalRef BuildInternalRef(XElement el, bool lenient)
    {
        ArchetypeInternalRef r = new();
        string? tp = Opt14XmlReader.FindChildValue(el, "target_path", lenient);
        if (!string.IsNullOrEmpty(tp))
        {
            r.TargetPath = tp;
        }
        return r;
    }

    private static CComplexObjectProxy BuildComplexProxy(XElement el, bool lenient)
    {
        CComplexObjectProxy p = new();
        string? tp = Opt14XmlReader.FindChildValue(el, "target_path", lenient);
        if (!string.IsNullOrEmpty(tp))
        {
            p.TargetPath = tp;
        }
        return p;
    }

    /// <summary>
    /// Unwraps a <c>C_PRIMITIVE_OBJECT</c> envelope. OPT1.4 wraps every
    /// primitive constraint as
    /// <c>&lt;children xsi:type="C_PRIMITIVE_OBJECT"&gt;&lt;rm_type_name&gt;…&lt;/rm_type_name&gt;
    /// &lt;node_id&gt;…&lt;/node_id&gt;&lt;occurrences&gt;…&lt;/occurrences&gt;
    /// &lt;item xsi:type="C_STRING|C_INTEGER|…"&gt;…&lt;/item&gt;&lt;/children&gt;</c>
    /// instead of placing the inner constraint directly under
    /// <c>&lt;children&gt;</c>. We parse the inner <c>&lt;item&gt;</c>
    /// and let the caller merge the wrapper's <c>rm_type_name</c> /
    /// <c>node_id</c> / <c>occurrences</c> on top via
    /// <see cref="PopulateCObjectCommon"/>.
    /// </summary>
    private static CObject BuildPrimitiveWrapper(
        XElement el,
        bool lenient,
        List<(XElement Source, CComplexObject Node)>? archetypeRootSources)
    {
        XElement? item = Opt14XmlReader.FindChild(el, "item", lenient);
        if (item is null)
        {
            // Empty wrapper — fall back to a permissive CString so the
            // outer common-member copy still produces a valid CObject.
            return new CString();
        }
        return BuildObject(item, lenient, archetypeRootSources);
    }

    private static void PopulateCObjectCommon(XElement el, CObject obj, bool lenient)
    {
        string? rmType = Opt14XmlReader.FindChildValue(el, "rm_type_name", lenient);
        if (!string.IsNullOrEmpty(rmType))
        {
            obj.RmTypeName = rmType;
        }
        // Accept both <node_id> and <archetype_node_id>; assign
        // whichever is non-null. Planning decision #2.
        string? nodeId = Opt14XmlReader.FindChildValue(el, "node_id", lenient)
            ?? Opt14XmlReader.FindChildValue(el, "archetype_node_id", lenient);
        if (!string.IsNullOrEmpty(nodeId))
        {
            obj.NodeId = nodeId;
        }
        Interval<int>? occ = ReadIntInterval(Opt14XmlReader.FindChild(el, "occurrences", lenient), lenient);
        if (occ is not null)
        {
            obj.Occurrences = occ;
        }
    }

    // ------------------------------------------------------------------
    // Attribute reading + recursion.
    // ------------------------------------------------------------------

    private static void ReadAttributesInto(
        XElement parent,
        CComplexObject target,
        bool lenient,
        List<(XElement Source, CComplexObject Node)>? archetypeRootSources)
    {
        foreach (XElement attrEl in Opt14XmlReader.FindChildren(parent, "attributes", lenient))
        {
            CAttribute? attr = BuildAttribute(attrEl, lenient, archetypeRootSources);
            if (attr is not null)
            {
                target.Attributes.Add(attr);
            }
        }
        foreach (XElement tupleEl in Opt14XmlReader.FindChildren(parent, "attribute_tuples", lenient))
        {
            CAttributeTuple? tuple = BuildAttributeTuple(tupleEl, lenient, archetypeRootSources);
            if (tuple is not null)
            {
                target.AttributeTuples.Add(tuple);
            }
        }
    }

    private static CAttribute? BuildAttribute(
        XElement el,
        bool lenient,
        List<(XElement Source, CComplexObject Node)>? archetypeRootSources)
    {
        string? xsi = el.Attribute(Opt14XmlReader.Xsi + "type")?.Value;
        string disc = xsi is null ? string.Empty : StripPrefix(xsi);
        CAttribute attr;
        switch (disc)
        {
            case "C_MULTIPLE_ATTRIBUTE":
                CMultipleAttribute multi = new();
                XElement? card = Opt14XmlReader.FindChild(el, "cardinality", lenient);
                if (card is not null)
                {
                    multi.Cardinality = ReadCardinality(card, lenient);
                }
                attr = multi;
                break;
            case "C_SINGLE_ATTRIBUTE":
            case "":
                attr = new CSingleAttribute();
                break;
            default:
                if (!lenient)
                {
                    throw new InvalidOperationException(
                        $"Unknown OPT1.4 attribute xsi:type '{disc}' on <{el.Name.LocalName}> " +
                        $"(line {Opt14XmlReader.LineInfo(el).Line}).");
                }
                return null;
        }

        string? rmAttr = Opt14XmlReader.FindChildValue(el, "rm_attribute_name", lenient);
        if (!string.IsNullOrEmpty(rmAttr))
        {
            attr.RmAttributeName = rmAttr;
        }
        attr.Existence = ReadIntInterval(Opt14XmlReader.FindChild(el, "existence", lenient), lenient);

        foreach (XElement childEl in Opt14XmlReader.FindChildren(el, "children", lenient))
        {
            CObject? child = BuildChild(childEl, lenient, archetypeRootSources);
            if (child is not null)
            {
                attr.Children.Add(child);
            }
        }
        return attr;
    }

    private static CObject? BuildChild(
        XElement childEl,
        bool lenient,
        List<(XElement Source, CComplexObject Node)>? archetypeRootSources)
    {
        try
        {
            CObject obj = BuildObject(childEl, lenient, archetypeRootSources);
            if (obj is CArchetypeRoot root && archetypeRootSources is not null)
            {
                archetypeRootSources.Add((childEl, root));
            }
            return obj;
        }
        catch (NotSupportedException) when (lenient)
        {
            return null;
        }
        catch (InvalidOperationException) when (lenient)
        {
            return null;
        }
    }

    private static CAttributeTuple? BuildAttributeTuple(
        XElement el,
        bool lenient,
        List<(XElement Source, CComplexObject Node)>? archetypeRootSources)
    {
        CAttributeTuple tuple = new();
        foreach (XElement memberEl in Opt14XmlReader.FindChildren(el, "members", lenient))
        {
            CAttribute? a = BuildAttribute(memberEl, lenient, archetypeRootSources);
            if (a is not null)
            {
                tuple.Members.Add(a);
            }
        }
        foreach (XElement childEl in Opt14XmlReader.FindChildren(el, "children", lenient))
        {
            CObjectTuple row = new();
            foreach (XElement rowMember in Opt14XmlReader.FindChildren(childEl, "members", lenient))
            {
                CObject? o = BuildChild(rowMember, lenient, archetypeRootSources);
                if (o is not null)
                {
                    row.Members.Add(o);
                }
            }
            tuple.Children.Add(row);
        }
        return tuple;
    }

    // ------------------------------------------------------------------
    // Primitive constraint readers.
    // ------------------------------------------------------------------

    private static CString ReadCString(XElement el)
    {
        CString c = new();
        string? pattern = Opt14XmlReader.FindChildValue(el, "pattern", false);
        if (!string.IsNullOrEmpty(pattern))
        {
            c.Pattern = pattern;
        }
        foreach (XElement listEl in el.Elements(Opt14XmlReader.OpenEhr + "list"))
        {
            c.EnumeratedValues ??= [];
            c.EnumeratedValues.Add((listEl.Value ?? string.Empty).Trim());
        }
        string? def = Opt14XmlReader.FindChildValue(el, "default_value", false);
        if (def is not null)
        {
            c.DefaultValue = def;
        }
        return c;
    }

    private static CInteger ReadCInteger(XElement el)
    {
        CInteger c = new();
        c.Range = ReadIntInterval(Opt14XmlReader.FindChild(el, "range", false), false);
        foreach (XElement listEl in el.Elements(Opt14XmlReader.OpenEhr + "list"))
        {
            if (int.TryParse(listEl.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            {
                c.EnumeratedValues ??= [];
                c.EnumeratedValues.Add(v);
            }
        }
        return c;
    }

    private static CReal ReadCReal(XElement el)
    {
        CReal c = new();
        c.Range = ReadDoubleInterval(Opt14XmlReader.FindChild(el, "range", false), false);
        foreach (XElement listEl in el.Elements(Opt14XmlReader.OpenEhr + "list"))
        {
            if (double.TryParse(listEl.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                c.EnumeratedValues ??= [];
                c.EnumeratedValues.Add(v);
            }
        }
        return c;
    }

    private static CBoolean ReadCBoolean(XElement el)
    {
        CBoolean c = new();
        string? tv = Opt14XmlReader.FindChildValue(el, "true_valid", false);
        string? fv = Opt14XmlReader.FindChildValue(el, "false_valid", false);
        if (!string.IsNullOrEmpty(tv) && bool.TryParse(tv, out bool t))
        {
            c.TrueValid = t;
        }
        if (!string.IsNullOrEmpty(fv) && bool.TryParse(fv, out bool f))
        {
            c.FalseValid = f;
        }
        return c;
    }

    private static CDate ReadCDate(XElement el)
    {
        CDate c = new();
        string? pattern = Opt14XmlReader.FindChildValue(el, "pattern", false);
        if (!string.IsNullOrEmpty(pattern))
        {
            c.Pattern = pattern;
        }
        return c;
    }

    private static CTime ReadCTime(XElement el)
    {
        CTime c = new();
        string? pattern = Opt14XmlReader.FindChildValue(el, "pattern", false);
        if (!string.IsNullOrEmpty(pattern))
        {
            c.Pattern = pattern;
        }
        return c;
    }

    private static CDateTime ReadCDateTime(XElement el)
    {
        CDateTime c = new();
        string? pattern = Opt14XmlReader.FindChildValue(el, "pattern", false);
        if (!string.IsNullOrEmpty(pattern))
        {
            c.Pattern = pattern;
        }
        return c;
    }

    private static CDuration ReadCDuration(XElement el)
    {
        CDuration c = new();
        string? pattern = Opt14XmlReader.FindChildValue(el, "pattern", false);
        if (!string.IsNullOrEmpty(pattern))
        {
            c.Pattern = pattern;
        }
        return c;
    }

    private static CTerminologyCode ReadCTerminologyCode(XElement el, bool lenient)
    {
        CTerminologyCode c = new();
        string? terminologyId = Opt14XmlReader.FindChildValue(
            Opt14XmlReader.FindChild(el, "terminology_id", lenient) ?? el, "value", lenient);
        if (!string.IsNullOrEmpty(terminologyId))
        {
            c.TerminologyId = terminologyId;
        }
        string? vsRef = Opt14XmlReader.FindChildValue(el, "value_set_reference", lenient)
            ?? Opt14XmlReader.FindChildValue(el, "referenceSetUri", lenient);
        if (!string.IsNullOrEmpty(vsRef))
        {
            c.ValueSetRef = vsRef;
        }
        foreach (XElement codeListEl in Opt14XmlReader.FindChildren(el, "code_list", lenient))
        {
            string code = (codeListEl.Value ?? string.Empty).Trim();
            if (code.Length > 0)
            {
                c.EnumeratedValues ??= [];
                c.EnumeratedValues.Add(code);
            }
        }
        return c;
    }

    private static CCodePhrase ReadCCodePhrase(XElement el, bool lenient)
    {
        CCodePhrase c = new();
        XElement? termIdEl = Opt14XmlReader.FindChild(el, "terminology_id", lenient);
        if (termIdEl is not null)
        {
            string? terminologyId = Opt14XmlReader.FindChildValue(termIdEl, "value", lenient);
            if (!string.IsNullOrEmpty(terminologyId))
            {
                c.TerminologyId = terminologyId;
            }
        }
        foreach (XElement codeEl in Opt14XmlReader.FindChildren(el, "code_list", lenient))
        {
            string code = (codeEl.Value ?? string.Empty).Trim();
            if (code.Length > 0)
            {
                c.CodeList.Add(code);
            }
        }
        return c;
    }

    private static CDvQuantity ReadCDvQuantity(XElement el, bool lenient)
    {
        CDvQuantity c = new();
        XElement? propEl = Opt14XmlReader.FindChild(el, "property", lenient);
        if (propEl is not null)
        {
            string? propCode = Opt14XmlReader.FindChildValue(propEl, "code_string", lenient);
            if (!string.IsNullOrEmpty(propCode))
            {
                c.Property = propCode;
            }
        }
        foreach (XElement listEl in Opt14XmlReader.FindChildren(el, "list", lenient))
        {
            CQuantityItem item = new();
            string? units = Opt14XmlReader.FindChildValue(listEl, "units", lenient);
            if (!string.IsNullOrEmpty(units))
            {
                item.Units = units;
            }
            item.Magnitude = ReadDoubleInterval(Opt14XmlReader.FindChild(listEl, "magnitude", lenient), lenient);
            item.Precision = ReadIntInterval(Opt14XmlReader.FindChild(listEl, "precision", lenient), lenient);
            c.Items.Add(item);
        }
        return c;
    }

    private static CDvOrdinal ReadCDvOrdinal(XElement el, bool lenient)
    {
        CDvOrdinal c = new();
        foreach (XElement listEl in Opt14XmlReader.FindChildren(el, "list", lenient))
        {
            CDvOrdinalItem item = new();
            string? value = Opt14XmlReader.FindChildValue(listEl, "value", lenient);
            if (!string.IsNullOrEmpty(value)
                && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            {
                item.Value = v;
            }
            XElement? symbolEl = Opt14XmlReader.FindChild(listEl, "symbol", lenient);
            if (symbolEl is not null)
            {
                string? defining = Opt14XmlReader.FindChildValue(symbolEl, "defining_code", lenient)
                    ?? Opt14XmlReader.FindChildValue(symbolEl, "code_string", lenient);
                if (!string.IsNullOrEmpty(defining))
                {
                    item.Symbol = defining;
                }
            }
            c.Items.Add(item);
        }
        return c;
    }

    // ------------------------------------------------------------------
    // Assertion + interval + cardinality readers.
    // ------------------------------------------------------------------

    private static DotnetOpenEhr.Archetypes.Aom2.Constraint.Assertion ReadAssertion(XElement el, bool lenient)
    {
        DotnetOpenEhr.Archetypes.Aom2.Constraint.Assertion a = new();
        string? raw = Opt14XmlReader.FindChildValue(el, "string_expression", lenient);
        if (!string.IsNullOrEmpty(raw))
        {
            a.RawText = raw;
        }
        string? tag = Opt14XmlReader.FindChildValue(el, "tag", lenient);
        if (!string.IsNullOrEmpty(tag))
        {
            a.Tag = tag;
        }
        return a;
    }

    private static Cardinality? ReadCardinality(XElement el, bool lenient)
    {
        Interval<int>? interval = ReadIntInterval(Opt14XmlReader.FindChild(el, "interval", lenient), lenient);
        if (interval is null)
        {
            return null;
        }
        bool ordered = bool.TryParse(Opt14XmlReader.FindChildValue(el, "is_ordered", lenient), out bool o) && o;
        bool unique = bool.TryParse(Opt14XmlReader.FindChildValue(el, "is_unique", lenient), out bool u) && u;
        return new Cardinality(interval, ordered, unique);
    }

    private static Interval<int>? ReadIntInterval(XElement? el, bool lenient)
    {
        if (el is null)
        {
            return null;
        }
        string? lowerStr = Opt14XmlReader.FindChildValue(el, "lower", lenient);
        string? upperStr = Opt14XmlReader.FindChildValue(el, "upper", lenient);
        bool lowerInc = ParseBool(Opt14XmlReader.FindChildValue(el, "lower_included", lenient), true);
        bool upperInc = ParseBool(Opt14XmlReader.FindChildValue(el, "upper_included", lenient), true);
        bool lowerUnbounded = ParseBool(Opt14XmlReader.FindChildValue(el, "lower_unbounded", lenient), lowerStr is null);
        bool upperUnbounded = ParseBool(Opt14XmlReader.FindChildValue(el, "upper_unbounded", lenient), upperStr is null);

        int? lower = lowerUnbounded ? null
            : int.TryParse(lowerStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lo) ? lo
            : (int?)null;
        int? upper = upperUnbounded ? null
            : int.TryParse(upperStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hi) ? hi
            : (int?)null;
        return BuildInterval(lower, upper, lowerInc, upperInc);
    }

    private static Interval<double>? ReadDoubleInterval(XElement? el, bool lenient)
    {
        if (el is null)
        {
            return null;
        }
        string? lowerStr = Opt14XmlReader.FindChildValue(el, "lower", lenient);
        string? upperStr = Opt14XmlReader.FindChildValue(el, "upper", lenient);
        bool lowerInc = ParseBool(Opt14XmlReader.FindChildValue(el, "lower_included", lenient), true);
        bool upperInc = ParseBool(Opt14XmlReader.FindChildValue(el, "upper_included", lenient), true);
        bool lowerUnbounded = ParseBool(Opt14XmlReader.FindChildValue(el, "lower_unbounded", lenient), lowerStr is null);
        bool upperUnbounded = ParseBool(Opt14XmlReader.FindChildValue(el, "upper_unbounded", lenient), upperStr is null);

        double? lower = lowerUnbounded ? null
            : double.TryParse(lowerStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lo) ? lo
            : (double?)null;
        double? upper = upperUnbounded ? null
            : double.TryParse(upperStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double hi) ? hi
            : (double?)null;
        return BuildInterval(lower, upper, lowerInc, upperInc);
    }

    private static Interval<T>? BuildInterval<T>(T? lower, T? upper, bool lowerInc, bool upperInc)
        where T : struct, IComparable<T>
    {
        bool hasLower = lower.HasValue;
        bool hasUpper = upper.HasValue;
        if (!hasLower && !hasUpper)
        {
            return Interval<T>.Unbounded();
        }
        if (!hasLower)
        {
            return upperInc ? Interval<T>.AtMost(upper!.Value) : Interval<T>.LessThan(upper!.Value);
        }
        if (!hasUpper)
        {
            return lowerInc ? Interval<T>.AtLeast(lower!.Value) : Interval<T>.GreaterThan(lower!.Value);
        }
        return (lowerInc, upperInc) switch
        {
            (true, true) => Interval<T>.Bounded(lower!.Value, upper!.Value),
            (false, true) => Interval<T>.LowerOpen(lower!.Value, upper!.Value),
            (true, false) => Interval<T>.UpperOpen(lower!.Value, upper!.Value),
            (false, false) => Interval<T>.Open(lower!.Value, upper!.Value),
        };
    }

    private static bool ParseBool(string? s, bool defaultValue)
        => !string.IsNullOrEmpty(s) && bool.TryParse(s, out bool v) ? v : defaultValue;

    private static string StripPrefix(string xsi)
    {
        int colon = xsi.IndexOf(':');
        return colon >= 0 ? xsi[(colon + 1)..] : xsi;
    }
}
