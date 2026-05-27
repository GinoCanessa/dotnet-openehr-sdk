using DotnetOpenEhr.Archetypes.Aom2;
using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using DotnetOpenEhr.Bmm;

namespace DotnetOpenEhr.Archetypes.Validation;

/// <summary>
/// Cross-validates an <see cref="Archetype"/> against a Reference Model
/// described by a <see cref="BmmModel"/>. Surfaces RM-type-name,
/// attribute-name, primitive-type, and (best-effort) generic-parameter
/// conformance issues as a flat list of <see cref="ArchetypeIssue"/>.
/// </summary>
/// <remarks>
/// The walker is purely top-down and stateless across invocations;
/// instances are cheap and thread-safe.
/// </remarks>
public sealed class ArchetypeBmmValidator
{
    // Map AOM2 primitive constraint runtime types to the set of BMM
    // type-name strings that satisfy them. BMM uses snake-case for the
    // ISO 8601 wrappers (e.g. "Iso8601_date") and the openEHR
    // foundation aliases ("Terminology_code"); we accept the obvious
    // PascalCase and short aliases too so the rule is forgiving on
    // hand-authored schemas.
    private static readonly IReadOnlyDictionary<Type, IReadOnlySet<string>> s_primitiveExpectedTypes =
        new Dictionary<Type, IReadOnlySet<string>>
        {
            [typeof(CString)] = ToSet("String"),
            [typeof(CInteger)] = ToSet("Integer", "Integer64"),
            [typeof(CReal)] = ToSet("Real", "Double"),
            [typeof(CBoolean)] = ToSet("Boolean"),
            [typeof(CDate)] = ToSet("Iso8601_date", "Iso8601Date", "Date"),
            [typeof(CTime)] = ToSet("Iso8601_time", "Iso8601Time", "Time"),
            [typeof(CDateTime)] = ToSet("Iso8601_date_time", "Iso8601DateTime", "DateTime", "Date_time"),
            [typeof(CDuration)] = ToSet("Iso8601_duration", "Iso8601Duration", "Duration"),
            [typeof(CTerminologyCode)] = ToSet("Terminology_code", "TerminologyCode", "CODE_PHRASE", "Code_phrase", "CodePhrase"),
        };

    private static IReadOnlySet<string> ToSet(params string[] names)
        => new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Validates <paramref name="archetype"/> against <paramref name="rmBmm"/>
    /// and returns every issue found. Issues are returned in
    /// document order (depth-first walk of the definition tree).
    /// </summary>
    /// <param name="archetype">The archetype to validate.</param>
    /// <param name="rmBmm">The reference-model BMM to validate against.</param>
    /// <param name="ct">
    /// Cancellation token; checked once per top-level attribute walk.
    /// </param>
    public IReadOnlyList<ArchetypeIssue> Validate(
        Archetype archetype,
        BmmModel rmBmm,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(archetype);
        ArgumentNullException.ThrowIfNull(rmBmm);

        List<ArchetypeIssue> issues = [];
        if (archetype.Definition is null)
        {
            return issues;
        }

        WalkComplex(archetype.Definition, "/", rmBmm, issues, ct, isRoot: true);
        return issues;
    }

    private static void WalkComplex(
        CComplexObject node,
        string path,
        BmmModel rmBmm,
        List<ArchetypeIssue> issues,
        CancellationToken ct,
        bool isRoot)
    {
        BmmClass? bmmClass = string.IsNullOrEmpty(node.RmTypeName)
            ? null
            : rmBmm.GetClass(node.RmTypeName);

        if (bmmClass is null)
        {
            issues.Add(new ArchetypeIssue(
                ArchetypeIssueSeverity.Error,
                path,
                ArchetypeIssueCodes.UnknownRmType,
                $"RM type '{node.RmTypeName}' is not declared in the supplied BMM."));
            return;
        }

        foreach (CAttribute attr in node.Attributes)
        {
            if (isRoot)
            {
                if (ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                }
            }
            WalkAttribute(attr, path, bmmClass, rmBmm, issues, ct);
        }

        foreach (CAttributeTuple tuple in node.AttributeTuples)
        {
            if (isRoot)
            {
                if (ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                }
            }
            WalkAttributeTuple(tuple, path, bmmClass, rmBmm, issues, ct);
        }
    }

    private static void WalkAttribute(
        CAttribute attr,
        string parentPath,
        BmmClass parentClass,
        BmmModel rmBmm,
        List<ArchetypeIssue> issues,
        CancellationToken ct)
    {
        string attrPath = AppendAttribute(parentPath, attr.RmAttributeName);

        BmmProperty? property = ResolveProperty(parentClass, attr.RmAttributeName, rmBmm);
        if (property is null)
        {
            issues.Add(new ArchetypeIssue(
                ArchetypeIssueSeverity.Error,
                attrPath,
                ArchetypeIssueCodes.UnknownAttribute,
                $"Attribute '{attr.RmAttributeName}' is not declared on class '{parentClass.Name}' (or any ancestor) in the supplied BMM."));
            return;
        }

        string? expectedElementType = GetElementTypeName(property.Type);

        foreach (CObject child in attr.Children)
        {
            WalkChild(child, attrPath, expectedElementType, rmBmm, issues, ct);
        }
    }

    private static void WalkAttributeTuple(
        CAttributeTuple tuple,
        string parentPath,
        BmmClass parentClass,
        BmmModel rmBmm,
        List<ArchetypeIssue> issues,
        CancellationToken ct)
    {
        // First validate each member attribute name against the parent
        // class. Capture the resolved property element type per slot so
        // tuple rows can be type-checked positionally.
        List<string?> slotExpectedTypes = new(tuple.Members.Count);
        foreach (CAttribute member in tuple.Members)
        {
            string memberPath = AppendAttribute(parentPath, member.RmAttributeName);
            BmmProperty? property = ResolveProperty(parentClass, member.RmAttributeName, rmBmm);
            if (property is null)
            {
                issues.Add(new ArchetypeIssue(
                    ArchetypeIssueSeverity.Error,
                    memberPath,
                    ArchetypeIssueCodes.UnknownAttribute,
                    $"Attribute '{member.RmAttributeName}' is not declared on class '{parentClass.Name}' (or any ancestor) in the supplied BMM."));
                slotExpectedTypes.Add(null);
            }
            else
            {
                slotExpectedTypes.Add(GetElementTypeName(property.Type));
            }
        }

        foreach (CObjectTuple row in tuple.Children)
        {
            int slotCount = Math.Min(row.Members.Count, slotExpectedTypes.Count);
            for (int i = 0; i < slotCount; i++)
            {
                string memberName = i < tuple.Members.Count
                    ? tuple.Members[i].RmAttributeName
                    : string.Empty;
                string slotPath = AppendAttribute(parentPath, memberName);
                WalkChild(row.Members[i], slotPath, slotExpectedTypes[i], rmBmm, issues, ct);
            }
        }
    }

    private static void WalkChild(
        CObject child,
        string parentAttrPath,
        string? expectedElementType,
        BmmModel rmBmm,
        List<ArchetypeIssue> issues,
        CancellationToken ct)
    {
        string childPath = string.IsNullOrEmpty(child.NodeId)
            ? parentAttrPath
            : $"{parentAttrPath}[{child.NodeId}]";

        switch (child)
        {
            case CComplexObject ccmp:
                // BMM_004 — best-effort generic / container element conformance.
                if (expectedElementType is not null
                    && !IsAssignableTo(ccmp.RmTypeName, expectedElementType, rmBmm))
                {
                    issues.Add(new ArchetypeIssue(
                        ArchetypeIssueSeverity.Warning,
                        childPath,
                        ArchetypeIssueCodes.GenericParamMismatch,
                        $"Child type '{ccmp.RmTypeName}' is not assignment-compatible with the parent attribute's declared element type '{expectedElementType}'."));
                }
                WalkComplex(ccmp, childPath, rmBmm, issues, ct, isRoot: false);
                break;

            case CString:
            case CInteger:
            case CReal:
            case CBoolean:
            case CDate:
            case CTime:
            case CDateTime:
            case CDuration:
            case CTerminologyCode:
                CheckPrimitive(child, childPath, expectedElementType, issues);
                break;

            // CReferenceObject (ArchetypeSlot, ArchetypeInternalRef,
            // CComplexObjectProxy) and the second-order constraints
            // (CCodePhrase, CDvQuantity, CDvOrdinal) are not validated
            // here. Their RM-type semantics are richer than the
            // single-axis check we perform on CComplexObject and they
            // would produce false positives without dedicated rules.
            default:
                break;
        }
    }

    private static void CheckPrimitive(
        CObject primitive,
        string path,
        string? expectedElementType,
        List<ArchetypeIssue> issues)
    {
        if (expectedElementType is null)
        {
            return;
        }
        if (!s_primitiveExpectedTypes.TryGetValue(primitive.GetType(), out IReadOnlySet<string>? allowed))
        {
            return;
        }
        if (allowed.Contains(expectedElementType))
        {
            return;
        }

        issues.Add(new ArchetypeIssue(
            ArchetypeIssueSeverity.Error,
            path,
            ArchetypeIssueCodes.TypeMismatch,
            $"Constraint '{primitive.GetType().Name}' is incompatible with the parent attribute's declared type '{expectedElementType}'."));
    }

    // Resolves a property by name on the class, walking ancestors until
    // found or the chain is exhausted. Case-sensitive lookup first (BMM
    // attribute names conventionally match exactly), then falls through
    // to a case-insensitive scan for forgiveness.
    private static BmmProperty? ResolveProperty(BmmClass cls, string name, BmmModel rmBmm)
    {
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        return ResolvePropertyImpl(cls, name, rmBmm, visited);
    }

    private static BmmProperty? ResolvePropertyImpl(
        BmmClass cls,
        string name,
        BmmModel rmBmm,
        HashSet<string> visited)
    {
        if (!visited.Add(cls.Name))
        {
            return null;
        }

        if (cls.Properties.TryGetValue(name, out BmmProperty? exact))
        {
            return exact;
        }
        foreach (KeyValuePair<string, BmmProperty> kvp in cls.Properties)
        {
            if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        foreach (string ancestorName in cls.Ancestors)
        {
            BmmClass? ancestor = rmBmm.GetClass(ancestorName);
            if (ancestor is null)
            {
                continue;
            }
            BmmProperty? inherited = ResolvePropertyImpl(ancestor, name, rmBmm, visited);
            if (inherited is not null)
            {
                return inherited;
            }
        }
        return null;
    }

    // Returns the effective element type-name for an attribute property:
    // for a simple type, the type itself; for a container or generic, the
    // root of the first type argument. This is what an archetype's child
    // CComplexObject would be compared against for assignment compatibility.
    private static string? GetElementTypeName(BmmType type)
    {
        switch (type)
        {
            case BmmContainerType container:
                return container.TypeArguments.Count > 0
                    ? GetElementTypeName(container.TypeArguments[0])
                    : null;
            case BmmGenericType generic:
                return generic.TypeName;
            case BmmSimpleType simple:
                return simple.TypeName;
            default:
                return type.TypeName;
        }
    }

    // True when `childType` equals `expectedType` or is a descendant
    // (transitively) of `expectedType` per the BMM ancestor graph.
    // Returns true if either class is unresolvable from the BMM (so the
    // rule is best-effort and never produces false positives when the
    // BMM coverage is thin).
    private static bool IsAssignableTo(string childType, string expectedType, BmmModel rmBmm)
    {
        if (string.IsNullOrEmpty(childType) || string.IsNullOrEmpty(expectedType))
        {
            return true;
        }
        if (string.Equals(childType, expectedType, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        BmmClass? childClass = rmBmm.GetClass(childType);
        BmmClass? expectedClass = rmBmm.GetClass(expectedType);
        if (childClass is null || expectedClass is null)
        {
            return true;
        }

        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        return HasAncestor(childClass, expectedType, rmBmm, visited);
    }

    private static bool HasAncestor(
        BmmClass cls,
        string targetName,
        BmmModel rmBmm,
        HashSet<string> visited)
    {
        if (!visited.Add(cls.Name))
        {
            return false;
        }
        foreach (string ancestorName in cls.Ancestors)
        {
            if (string.Equals(ancestorName, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            BmmClass? ancestor = rmBmm.GetClass(ancestorName);
            if (ancestor is null)
            {
                continue;
            }
            if (HasAncestor(ancestor, targetName, rmBmm, visited))
            {
                return true;
            }
        }
        return false;
    }

    private static string AppendAttribute(string parentPath, string attrName)
    {
        if (parentPath == "/")
        {
            return "/" + attrName;
        }
        return parentPath + "/" + attrName;
    }
}
