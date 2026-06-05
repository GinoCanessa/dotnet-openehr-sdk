using System.Globalization;
using System.Text;
using System.Text.Json;
using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes;
using DotnetOpenEhr.Rm.DataTypes.Basic;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Templates.Abstractions;

namespace DotnetOpenEhr.Serialization.Json.Flat;

/// <summary>
/// Schema-driven counterpart to <see cref="FlatJsonContentWriter"/>:
/// reads a previously-emitted FLAT bag of <see cref="FlatPath"/> /
/// <see cref="JsonElement"/> pairs and rebuilds the Composition's
/// content tree on top of metadata already applied by
/// <see cref="OpenEhrFlatJson"/>.
/// </summary>
/// <remarks>
/// MVP scope mirrors <see cref="FlatJsonContentWriter"/>; see that
/// type's remarks for the list of supported / unsupported RM shapes.
/// </remarks>
internal static class FlatJsonContentParser
{
    private const int MaxRepeatIndex = 4096;
    private const int MaxSparseGap = 128;

    /// <summary>Attempts to attach <paramref name="entry"/> to the content tree.
    /// Returns <c>false</c> when the path is not a content path so the caller
    /// can fall back to other resolution strategies.</summary>
    internal static bool TryApplyContentEntry(
        Composition composition,
        string templateId,
        KeyValuePair<FlatPath, JsonElement> entry,
        ITemplateSchema schema)
    {
        string full = entry.Key.OriginalForm;
        string attribute = entry.Key.Attribute;
        if (full.Length <= templateId.Length) return false;
        if (full[templateId.Length] != '/') return false;
        if (!full.AsSpan(0, templateId.Length).SequenceEqual(templateId.AsSpan())) return false;

        string tail = full.Substring(templateId.Length + 1);
        string body = attribute.Length == 0
            ? tail
            : tail.Substring(0, tail.Length - attribute.Length);

        // Must start with the content axis to be a content entry. The
        // metadata side (category, context, ...) was already handled
        // upstream.
        if (!body.StartsWith("content", StringComparison.Ordinal))
        {
            return false;
        }

        List<Segment> segments = SplitSegments(body, full);
        if (segments.Count == 0) return false;

        return Navigate(composition, templateId, segments, attribute, entry.Value, schema, full);
    }

    /// <summary>One slash-delimited segment of a FLAT path. <see cref="Index"/>
    /// is <c>null</c> when the segment has no <c>:N</c> repeat index.</summary>
    private readonly record struct Segment(string Name, int? Index);

    private static List<Segment> SplitSegments(string body, string originalPath)
    {
        List<Segment> result = [];
        int i = 0;
        while (i < body.Length)
        {
            int slash = body.IndexOf('/', i);
            int end = slash < 0 ? body.Length : slash;
            int colon = body.IndexOf(':', i, end - i);
            string name;
            int? idx = null;
            if (colon < 0)
            {
                name = body.Substring(i, end - i);
            }
            else
            {
                name = body.Substring(i, colon - i);
                string indexStr = body.Substring(colon + 1, end - colon - 1);
                if (indexStr.Length == 0)
                {
                    ThrowInvalidRepeatIndex(originalPath, indexStr);
                }
                if (indexStr[0] == '-')
                {
                    ThrowInvalidRepeatIndex(originalPath, indexStr);
                }
                if (!int.TryParse(indexStr, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed))
                {
                    ThrowInvalidRepeatIndex(originalPath, indexStr);
                }
                if (parsed > MaxRepeatIndex)
                {
                    throw new JsonException(
                        $"FLAT path '{originalPath}' uses repeat index {parsed.ToString(CultureInfo.InvariantCulture)}, which exceeds the maximum supported index {MaxRepeatIndex.ToString(CultureInfo.InvariantCulture)}.");
                }
                idx = parsed;
            }
            result.Add(new Segment(name, idx));
            if (slash < 0) break;
            i = slash + 1;
        }
        return result;
    }

    private static void ThrowInvalidRepeatIndex(string originalPath, string index)
        => throw new JsonException(
            $"FLAT path '{originalPath}' contains invalid repeat index '{index}'. Repeat indexes must be non-negative integers.");

    private static bool Navigate(
        Composition composition,
        string templateId,
        List<Segment> segments,
        string attribute,
        JsonElement value,
        ITemplateSchema schema,
        string originalPath)
    {
        // The last segment is either:
        //   - a leaf scalar (e.g. "value" with attribute "|magnitude"),
        //   - the locatable-name sidecar ("name" with attribute "|value"),
        //   - the archetype-node-id sidecar ("_archetype_node_id" with no attribute),
        //   - or, for ItemSingle, the "item" attribute that contains
        //     an Element whose value follows below.
        // Everything before it is a navigation segment that must yield
        // (or create) a parent node.

        int leafIdx = segments.Count - 1;
        Segment leaf = segments[leafIdx];

        // Sidecar: <path>/_archetype_node_id (no attribute).
        if (string.Equals(leaf.Name, "_archetype_node_id", StringComparison.Ordinal) && attribute.Length == 0)
        {
            object? parent = NavigateTo(composition, templateId, segments, 0, leafIdx, schema, originalPath);
            if (parent is Locatable loc)
            {
                loc.ArchetypeNodeId = value.GetString() ?? string.Empty;
                return true;
            }
            return false;
        }

        // Sidecar: <path>/name|value — runtime name of the parent locatable.
        if (string.Equals(leaf.Name, "name", StringComparison.Ordinal)
            && string.Equals(attribute, "|value", StringComparison.Ordinal))
        {
            object? parent = NavigateTo(composition, templateId, segments, 0, leafIdx, schema, originalPath);
            if (parent is Locatable loc)
            {
                loc.Name = new DvText(value.GetString() ?? string.Empty);
                return true;
            }
            return false;
        }

        // language / encoding on Entry — emitted by the writer as
        // <entry-path>/language|terminology and /language|code.
        if ((string.Equals(leaf.Name, "language", StringComparison.Ordinal)
                || string.Equals(leaf.Name, "encoding", StringComparison.Ordinal))
            && attribute.Length > 0)
        {
            object? parent = NavigateTo(composition, templateId, segments, 0, leafIdx, schema, originalPath);
            if (parent is Entry entry)
            {
                CodePhrase target = string.Equals(leaf.Name, "language", StringComparison.Ordinal)
                    ? entry.Language
                    : entry.Encoding;
                ApplyCodePhraseAttr(target, attribute, value);
                return true;
            }
            return false;
        }

        // Element.value leaves: the navigation pointer is "value" (or
        // "null_flavour") and the attribute carries the scalar selector.
        if (attribute.Length > 0)
        {
            // The leaf segment names the value-bearing property; navigate
            // to its parent and apply.
            object? container = NavigateTo(composition, templateId, segments, 0, leafIdx, schema, originalPath);
            string leafFlatPath = ComposeFlatPath(templateId, segments, leafIdx);
            return ApplyScalar(container, leaf.Name, attribute, value, leafFlatPath, originalPath, schema);
        }

        // Bare-key DateTime leaves emitted by the writer as direct strings:
        //   <history-path>/origin, <event-path>/time.
        // Route them through the scalar applier with an empty attribute;
        // the applier knows how to convert the lexical form.
        if (string.Equals(leaf.Name, "origin", StringComparison.Ordinal)
            || string.Equals(leaf.Name, "time", StringComparison.Ordinal))
        {
            object? container = NavigateTo(composition, templateId, segments, 0, leafIdx, schema, originalPath);
            string leafFlatPath = ComposeFlatPath(templateId, segments, leafIdx);
            return ApplyScalar(container, leaf.Name, attribute, value, leafFlatPath, originalPath, schema);
        }

        // Unknown no-attribute leaf — out of MVP scope.
        return false;
    }

    private static string ComposeFlatPath(string templateId, List<Segment> segments, int endIndex)
    {
        StringBuilder sb = new(templateId);
        for (int i = 0; i <= endIndex; i++)
        {
            sb.Append('/').Append(segments[i].Name);
        }
        return sb.ToString();
    }

    /// <summary>Walks segments[from..to) (exclusive end) creating parent
    /// nodes as needed and returns the final parent object whose
    /// segments[to-1] child is the navigation target.</summary>
    private static object? NavigateTo(
        Composition composition,
        string templateId,
        List<Segment> segments,
        int from,
        int to,
        ITemplateSchema schema,
        string originalPath)
    {
        object current = composition;
        StringBuilder flat = new(templateId);
        for (int i = from; i < to; i++)
        {
            Segment seg = segments[i];
            flat.Append('/').Append(seg.Name);
            string flatLookup = flat.ToString();
            schema.TryResolveType(flatLookup.AsSpan(), out TemplateRmTypeResolution resolution);
            object? next = StepInto(current, seg, resolution, originalPath);
            if (next is null) return null;
            current = next;
        }
        return current;
    }

    private static object? StepInto(object parent, Segment seg, TemplateRmTypeResolution resolution, string originalPath)
    {
        switch (parent)
        {
            case Composition comp when string.Equals(seg.Name, "content", StringComparison.Ordinal):
                comp.Content ??= [];
                return EnsureContentItem(comp.Content, seg.Index, resolution.RmTypeName, originalPath);

            case Section section when string.Equals(seg.Name, "items", StringComparison.Ordinal):
                section.Items ??= [];
                return EnsureContentItem(section.Items, seg.Index, resolution.RmTypeName, originalPath);

            case Observation obs when string.Equals(seg.Name, "data", StringComparison.Ordinal):
                obs.Data ??= new History();
                return obs.Data;
            case Observation obs when string.Equals(seg.Name, "state", StringComparison.Ordinal):
                obs.State ??= new History();
                return obs.State;

            case Evaluation eval when string.Equals(seg.Name, "data", StringComparison.Ordinal):
                return EnsureItemStructure(() => eval.Data, v => eval.Data = v, resolution.RmTypeName);

            case AdminEntry ae when string.Equals(seg.Name, "data", StringComparison.Ordinal):
                return EnsureItemStructure(() => ae.Data, v => ae.Data = v, resolution.RmTypeName);

            case History hist when string.Equals(seg.Name, "events", StringComparison.Ordinal):
                hist.Events ??= [];
                return EnsureEvent(hist.Events, seg.Index, resolution.RmTypeName, originalPath);

            case Event ev when string.Equals(seg.Name, "data", StringComparison.Ordinal):
                ev.Data = EnsureItemStructureValue(ev.Data, resolution.RmTypeName);
                return ev.Data;

            case ItemTree tree when string.Equals(seg.Name, "items", StringComparison.Ordinal):
                tree.Items ??= [];
                return EnsureItem(tree.Items, seg.Index, resolution.RmTypeName, originalPath);

            case ItemList list when string.Equals(seg.Name, "items", StringComparison.Ordinal):
                list.Items ??= [];
                return EnsureElement(list.Items, seg.Index, originalPath);

            case ItemSingle single when string.Equals(seg.Name, "item", StringComparison.Ordinal):
                return single.Item;

            case Cluster cluster when string.Equals(seg.Name, "items", StringComparison.Ordinal):
                return EnsureItem(cluster.Items, seg.Index, resolution.RmTypeName, originalPath);
        }
        return null;
    }

    private static ContentItem? EnsureContentItem(IList<ContentItem> list, int? idx, string? rmType, string originalPath)
    {
        int target = idx ?? 0;
        EnsureRepeatAllocationIsBounded(list.Count, target, originalPath);
        while (list.Count <= target)
        {
            list.Add(InstantiateContentItem(rmType, originalPath));
        }
        return list[target];
    }

    private static ContentItem InstantiateContentItem(string? rmType, string originalPath)
    {
        return rmType switch
        {
            "SECTION" => new Section(),
            "OBSERVATION" => new Observation(),
            "EVALUATION" => new Evaluation(),
            "ADMIN_ENTRY" => new AdminEntry(),
            // H6 — fail loudly on rmTypes the schema-driven content
            // writer doesn't support (notably INSTRUCTION/ACTION today,
            // and any future addition that lands without a paired
            // writer arm). Previously this silently downgraded to a
            // Section, corrupting the shape.
            _ => throw new JsonException(
                $"FLAT path '{originalPath}': RM type '{rmType ?? "(null)"}' is not supported by the schema-driven content writer."),
        };
    }

    private static Item EnsureItem(IList<Item>? list, int? idx, string? rmType, string originalPath)
    {
        ArgumentNullException.ThrowIfNull(list);
        int target = idx ?? 0;
        EnsureRepeatAllocationIsBounded(list.Count, target, originalPath);
        while (list.Count <= target)
        {
            list.Add(InstantiateItem(rmType));
        }
        return list[target];
    }

    private static Item InstantiateItem(string? rmType)
    {
        return rmType switch
        {
            "CLUSTER" => new Cluster(),
            _ => new Element(),
        };
    }

    private static Element EnsureElement(IList<Element> list, int? idx, string originalPath)
    {
        int target = idx ?? 0;
        EnsureRepeatAllocationIsBounded(list.Count, target, originalPath);
        while (list.Count <= target)
        {
            list.Add(new Element());
        }
        return list[target];
    }

    private static Event EnsureEvent(IList<Event> list, int? idx, string? rmType, string originalPath)
    {
        int target = idx ?? 0;
        EnsureRepeatAllocationIsBounded(list.Count, target, originalPath);
        while (list.Count <= target)
        {
            list.Add(InstantiateEvent(rmType));
        }
        return list[target];
    }

    private static void EnsureRepeatAllocationIsBounded(int currentCount, int targetIndex, string originalPath)
    {
        if (targetIndex - currentCount > MaxSparseGap)
        {
            throw new JsonException(
                $"FLAT path '{originalPath}' uses sparse repeat index {targetIndex.ToString(CultureInfo.InvariantCulture)} with current collection count {currentCount.ToString(CultureInfo.InvariantCulture)}; the maximum sparse gap is {MaxSparseGap.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static Event InstantiateEvent(string? rmType)
    {
        return rmType switch
        {
            "INTERVAL_EVENT" => new IntervalEvent(),
            _ => new PointEvent(),
        };
    }

    private static ItemStructure EnsureItemStructure(Func<ItemStructure> get, Action<ItemStructure> set, string? rmType)
    {
        ItemStructure current = get();
        ItemStructure resolved = EnsureItemStructureValue(current, rmType);
        if (!ReferenceEquals(resolved, current))
        {
            set(resolved);
        }
        return resolved;
    }

    private static ItemStructure EnsureItemStructureValue(ItemStructure current, string? rmType)
    {
        // If the schema demands a specific subtype and the existing
        // placeholder is the wrong shape, swap it.
        if (rmType is null) return current ?? new ItemTree();
        return rmType switch
        {
            "ITEM_TREE" => current is ItemTree ? current : new ItemTree(),
            "ITEM_LIST" => current is ItemList ? current : new ItemList(),
            "ITEM_SINGLE" => current is ItemSingle ? current : new ItemSingle(),
            // ITEM_TABLE intentionally out of scope.
            _ => current ?? new ItemTree(),
        };
    }

    private static bool ApplyScalar(
        object? parent,
        string leafName,
        string attribute,
        JsonElement value,
        string leafFlatPath,
        string fullFlatPath,
        ITemplateSchema schema)
    {
        if (parent is null) return false;

        if (parent is Element el)
        {
            switch (leafName)
            {
                case "value":
                    // Polymorphic — schema picks the concrete DV_* type.
                    schema.TryResolveType(leafFlatPath.AsSpan(), out TemplateRmTypeResolution res);
                    el.Value = MergeDataValue(el.Value, res.RmTypeName, attribute, value, fullFlatPath);
                    return true;

                case "null_flavour":
                    el.NullFlavour ??= new DvCodedText();
                    return ApplyDvCodedTextAttr(el.NullFlavour, attribute, value);
            }
        }

        if (parent is Event ev && string.Equals(leafName, "time", StringComparison.Ordinal))
        {
            ev.Time = ParseDvDateTime(value);
            return true;
        }

        if (parent is History hist && string.Equals(leafName, "origin", StringComparison.Ordinal))
        {
            hist.Origin = ParseDvDateTime(value);
            return true;
        }

        return false;
    }

    private static DataValue MergeDataValue(DataValue? current, string? rmType, string attribute, JsonElement value, string path)
        => MergeDataValueInternal(current, rmType, attribute, value, path);

    /// <summary>
    /// Test-visible entry point for the merge logic. Unit-tested in
    /// <c>MergeDataValueScalarPreservationTests</c> via
    /// <c>InternalsVisibleTo</c>; production callers should go through
    /// <see cref="MergeDataValue"/> which forwards here.
    /// </summary>
    internal static DataValue MergeDataValueInternal(DataValue? current, string? rmType, string attribute, JsonElement value, string path)
    {
        // The schema's FLAT-path index strips :N indices and therefore
        // last-wins when multiple sibling Elements (different
        // archetype_node_ids) share a parent path. The scalar attribute
        // is a strong type hint that disambiguates these cases — e.g.
        // |magnitude implies DV_QUANTITY/DV_COUNT, |code+|terminology
        // imply DV_CODED_TEXT. Prefer the attribute hint over the
        // schema's resolved type when the two disagree.
        string? inferred = InferRmTypeFromAttribute(attribute, value, rmType);
        string? effective = inferred ?? rmType;

        DataValue target = current ?? InstantiateDataValue(effective);
        if (effective is not null && !MatchesType(target, effective))
        {
            // M1 (0604-04): when the effective type changes mid-merge,
            // build a fresh instance of the new type but copy any
            // shared scalars from the previous instance forward via
            // DataValueScalarCopier. Otherwise the second entry would
            // silently zero out scalars written by the first entry
            // (e.g. |magnitude then |units bumping DvCount→DvQuantity
            // would lose magnitude).
            //
            // Exception: DvCodedText is a DvText subtype with a richer
            // shape. A `|value` arriving after `|code`+`|terminology`
            // makes the effective type "DV_TEXT" per
            // InferRmTypeFromAttribute fall-through, but we must NOT
            // downcast — both the text and the coded fields are
            // legitimate; the value is updated in place on the
            // existing DvCodedText. Documented as a positive contract
            // in MergeDataValueScalarPreservationTests.
            if (target is DvCodedText && string.Equals(effective, "DV_TEXT", StringComparison.Ordinal))
            {
                // Fall through and ApplyDataValueAttr below will write
                // |value into the existing DvCodedText.
            }
            else
            {
                DataValue replacement = InstantiateDataValue(effective);
                DataValueScalarCopier.Copy(target, replacement);
                target = replacement;
            }
        }
        ApplyDataValueAttr(target, attribute, value, path);
        return target;
    }

    /// <summary>
    /// Per-DV scalar copier for <see cref="MergeDataValue"/>'s
    /// type-transition path. M1 (0604-04): when the effective type
    /// changes mid-merge, copy any scalars the source and target types
    /// share so the second entry does not silently zero out values
    /// written by the first entry.
    ///
    /// The covered set matches what the FLAT parser actually
    /// instantiates (see <see cref="InstantiateDataValue"/>) and the
    /// transitions reachable from
    /// <see cref="InferRmTypeFromAttribute"/>. Adding arms for DV types
    /// the parser does not produce would expand FLAT support out of
    /// scope; intentionally not done here.
    /// </summary>
    private static class DataValueScalarCopier
    {
        public static void Copy(DataValue from, DataValue to)
        {
            switch (from, to)
            {
                // DvCount → DvQuantity: |magnitude (integral) lands as
                // DvCount; then |units forces re-instantiation as
                // DvQuantity. Preserve magnitude (long → double).
                case (DvCount src, DvQuantity dst):
                    dst.Magnitude = src.Magnitude;
                    break;

                // DvQuantity → DvCount: not currently reachable from
                // InferRmTypeFromAttribute (|units pins DV_QUANTITY,
                // |magnitude with non-integral JSON also pins
                // DV_QUANTITY). Defensive arm: round magnitude down.
                case (DvQuantity src, DvCount dst):
                    dst.Magnitude = (long)src.Magnitude;
                    break;

                // DvText → DvCodedText: a |value followed by a
                // |code+|terminology forces re-instantiation as
                // DvCodedText. Preserve the prior text since
                // DvCodedText has a `Value` field too (inherited).
                case (DvText src, DvCodedText dst):
                    dst.Value = src.Value;
                    break;

                // DvText → DvBoolean: a boolean-valued |value arriving
                // after a string-valued |value legitimately replaces
                // the target type. No shared scalars — explicit no-op,
                // documented so a future reader knows the transition
                // is reachable and intentionally destructive of the
                // prior text.
                case (DvText, DvBoolean):
                    break;

                // Other transitions are not reachable from the current
                // InferRmTypeFromAttribute table. Leaving the target
                // freshly-instantiated (no copy) is the safest default
                // — anything that becomes reachable in the future
                // should add an explicit arm here rather than rely on
                // this fallthrough.
                default:
                    break;
            }
        }
    }

    /// <summary>Maps a scalar attribute selector to an RM type when the
    /// selector is unambiguous. Falls back to <paramref name="schemaHint"/>
    /// for attributes that several DV_* types share (e.g. <c>|value</c>).</summary>
    private static string? InferRmTypeFromAttribute(string attribute, JsonElement value, string? schemaHint)
    {
        switch (attribute)
        {
            case "|magnitude":
                // DV_QUANTITY uses double, DV_COUNT uses long. If the schema
                // hint is one of those, trust it; otherwise sniff the JSON.
                if (string.Equals(schemaHint, "DV_QUANTITY", StringComparison.Ordinal)
                    || string.Equals(schemaHint, "DV_COUNT", StringComparison.Ordinal))
                {
                    return schemaHint;
                }
                return LooksLikeIntegral(value) ? "DV_COUNT" : "DV_QUANTITY";
            case "|units":
            case "|precision":
                return "DV_QUANTITY";
            case "|code":
            case "|terminology":
                return "DV_CODED_TEXT";
            case "|value":
                // Shared by DV_BOOLEAN, DV_TEXT, DV_CODED_TEXT, DV_DATE_TIME,
                // DV_DATE, DV_TIME, DV_DURATION — use JSON kind as a hint.
                if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                {
                    return "DV_BOOLEAN";
                }
                return schemaHint;
            default:
                return schemaHint;
        }
    }

    private static bool LooksLikeIntegral(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number) return false;
        return value.TryGetInt64(out _);
    }

    private static bool MatchesType(DataValue value, string rmType)
    {
        return rmType switch
        {
            "DV_QUANTITY" => value is DvQuantity,
            "DV_COUNT" => value is DvCount,
            "DV_BOOLEAN" => value is DvBoolean,
            "DV_CODED_TEXT" => value is DvCodedText,
            "DV_TEXT" => value is DvText and not DvCodedText,
            "DV_DATE_TIME" => value is DvDateTime,
            "DV_DATE" => value is DvDate,
            "DV_TIME" => value is DvTime,
            "DV_DURATION" => value is DvDuration,
            _ => true,
        };
    }

    private static DataValue InstantiateDataValue(string? rmType)
    {
        return rmType switch
        {
            "DV_QUANTITY" => new DvQuantity(),
            "DV_COUNT" => new DvCount(),
            "DV_BOOLEAN" => new DvBoolean(),
            "DV_CODED_TEXT" => new DvCodedText(),
            "DV_TEXT" => new DvText(),
            "DV_DATE_TIME" => new DvDateTime(),
            "DV_DATE" => new DvDate(),
            "DV_TIME" => new DvTime(),
            "DV_DURATION" => new DvDuration(),
            _ => new DvText(),
        };
    }

    private static void ApplyDataValueAttr(DataValue target, string attribute, JsonElement value, string path)
    {
        switch (target)
        {
            case DvQuantity q:
                switch (attribute)
                {
                    case "|magnitude": q.Magnitude = ReadDouble(value, path); break;
                    case "|units": q.Units = value.GetString() ?? string.Empty; break;
                    case "|precision": q.Precision = ReadInt(value, path); break;
                }
                break;
            case DvCount c when string.Equals(attribute, "|magnitude", StringComparison.Ordinal):
                c.Magnitude = ReadInt64(value, path);
                break;
            case DvBoolean b when string.Equals(attribute, "|value", StringComparison.Ordinal):
                b.Value = value.ValueKind == JsonValueKind.True
                    || (value.ValueKind == JsonValueKind.String
                        && bool.TryParse(value.GetString(), out bool parsed)
                        && parsed);
                break;
            case DvCodedText ct:
                ApplyDvCodedTextAttr(ct, attribute, value);
                break;
            case DvText t when string.Equals(attribute, "|value", StringComparison.Ordinal):
                t.Value = value.GetString() ?? string.Empty;
                break;
            case DvDateTime dt when string.Equals(attribute, "|value", StringComparison.Ordinal):
                dt.Value = IsoDateTime.Parse(value.GetString() ?? string.Empty);
                break;
            case DvDate d when string.Equals(attribute, "|value", StringComparison.Ordinal):
                d.Value = IsoDate.Parse(value.GetString() ?? string.Empty);
                break;
            case DvTime tm when string.Equals(attribute, "|value", StringComparison.Ordinal):
                tm.Value = IsoTime.Parse(value.GetString() ?? string.Empty);
                break;
            case DvDuration dur when string.Equals(attribute, "|value", StringComparison.Ordinal):
                dur.Value = IsoDuration.Parse(value.GetString() ?? string.Empty);
                break;
        }
    }

    private static bool ApplyDvCodedTextAttr(DvCodedText target, string attribute, JsonElement value)
    {
        switch (attribute)
        {
            case "|code": target.DefiningCode.CodeString = value.GetString() ?? string.Empty; return true;
            case "|value": target.Value = value.GetString() ?? string.Empty; return true;
            case "|terminology": target.DefiningCode.TerminologyId.Value = value.GetString() ?? string.Empty; return true;
        }
        return false;
    }

    private static void ApplyCodePhraseAttr(CodePhrase target, string attribute, JsonElement value)
    {
        switch (attribute)
        {
            case "|code": target.CodeString = value.GetString() ?? string.Empty; break;
            case "|terminology": target.TerminologyId.Value = value.GetString() ?? string.Empty; break;
        }
    }

    private static DvDateTime ParseDvDateTime(JsonElement value)
    {
        // L5 — the writer emits the date-time as a bare key (no
        // attribute); this helper just turns the raw JSON string into
        // a DvDateTime. The earlier signature carried unused `current`
        // and `attribute` parameters; both have been dropped.
        string raw = value.GetString() ?? string.Empty;
        return new DvDateTime(IsoDateTime.Parse(raw));
    }

    private static double ReadDouble(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Number) return value.GetDouble();
        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
        {
            return d;
        }
        throw new JsonException(
            $"FLAT path '{path}': expected a numeric (double) value, got {value.ValueKind} '{value.GetRawText()}'.");
    }

    private static int ReadInt(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int i)) return i;
        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed;
        }
        throw new JsonException(
            $"FLAT path '{path}': expected a numeric (int) value, got {value.ValueKind} '{value.GetRawText()}'.");
    }

    private static long ReadInt64(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long l)) return l;
        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
        {
            return parsed;
        }
        throw new JsonException(
            $"FLAT path '{path}': expected a numeric (long) value, got {value.ValueKind} '{value.GetRawText()}'.");
    }
}
