using System.Collections.Generic;
using DotnetOpenEhr.Archetypes.Aom2;
using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using DotnetOpenEhr.Archetypes.Aom2.Resource;
using DotnetOpenEhr.Archetypes.Aom2.Terminology;
using DotnetOpenEhr.Foundation;

namespace DotnetOpenEhr.Archetypes.Tests.Adl2;

/// <summary>
/// Deep structural equality helpers for AOM2 trees, used by the writer
/// round-trip tests. Source-position metadata
/// (<see cref="ArchetypeModelObject.SourceLine"/> /
/// <see cref="ArchetypeModelObject.SourceColumn"/>) is intentionally
/// ignored because it does not survive a Write → Parse round-trip.
/// </summary>
internal static class ArchetypeEquality
{
    public static bool ArchetypeDeepEquals(Archetype a, Archetype b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.GetType() != b.GetType()) return false;
        if (!string.Equals(a.ArchetypeId?.ToString(), b.ArchetypeId?.ToString(), System.StringComparison.Ordinal)) return false;
        if (!string.Equals(a.ParentArchetypeId?.ToString(), b.ParentArchetypeId?.ToString(), System.StringComparison.Ordinal)) return false;
        if (a.IsDifferential != b.IsDifferential) return false;
        if (a.IsTemplate != b.IsTemplate) return false;
        if (!string.Equals(a.OriginalLanguage, b.OriginalLanguage, System.StringComparison.Ordinal)) return false;
        if (!StringDictEquals(a.HeaderMetadata, b.HeaderMetadata)) return false;
        if (!TranslationsEqual(a.Translations, b.Translations)) return false;
        if (!DescriptionEquals(a.Description, b.Description)) return false;
        if (!CObjectEquals(a.Definition, b.Definition)) return false;
        if (!TerminologyEquals(a.Terminology, b.Terminology)) return false;
        if (!RulesEquals(a.Rules, b.Rules)) return false;
        // Annotations: we only compare presence (structured shape deferred).
        if ((a.Annotations is null) != (b.Annotations is null)) return false;
        return true;
    }

    // --- Resource description ---

    private static bool DescriptionEquals(ResourceDescription a, ResourceDescription b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (!string.Equals(a.LifecycleState ?? string.Empty, b.LifecycleState ?? string.Empty, System.StringComparison.Ordinal)) return false;
        if (!StringDictEquals(a.OriginalAuthor, b.OriginalAuthor)) return false;
        if (!StringDictEquals(a.OtherDetails, b.OtherDetails)) return false;
        if (!StringDictEquals(a.Licence, b.Licence)) return false;
        if (!string.Equals(a.Copyright ?? string.Empty, b.Copyright ?? string.Empty, System.StringComparison.Ordinal)) return false;
        if (!string.Equals(a.ResourcePackageUri ?? string.Empty, b.ResourcePackageUri ?? string.Empty, System.StringComparison.Ordinal)) return false;
        if (!StringListEquals(a.OtherContributors, b.OtherContributors)) return false;
        if (!StringListEquals(a.IpAcknowledgements, b.IpAcknowledgements)) return false;
        if (!StringListEquals(a.References, b.References)) return false;
        if (!StringListEquals(a.ConformsTo, b.ConformsTo)) return false;
        if (a.Details.Count != b.Details.Count) return false;
        foreach (KeyValuePair<string, ResourceDescriptionItem> kvp in a.Details)
        {
            if (!b.Details.TryGetValue(kvp.Key, out ResourceDescriptionItem? other)) return false;
            if (!DescriptionItemEquals(kvp.Value, other)) return false;
        }
        return true;
    }

    private static bool DescriptionItemEquals(ResourceDescriptionItem a, ResourceDescriptionItem b)
    {
        if (!string.Equals(a.Language ?? string.Empty, b.Language ?? string.Empty, System.StringComparison.Ordinal)) return false;
        if (!string.Equals(a.Purpose ?? string.Empty, b.Purpose ?? string.Empty, System.StringComparison.Ordinal)) return false;
        if (!string.Equals(a.Use ?? string.Empty, b.Use ?? string.Empty, System.StringComparison.Ordinal)) return false;
        if (!string.Equals(a.Misuse ?? string.Empty, b.Misuse ?? string.Empty, System.StringComparison.Ordinal)) return false;
        if (!string.Equals(a.Copyright ?? string.Empty, b.Copyright ?? string.Empty, System.StringComparison.Ordinal)) return false;
        if (!StringListEquals(a.Keywords, b.Keywords)) return false;
        if (!StringDictEquals(a.OriginalResourceUri, b.OriginalResourceUri)) return false;
        if (!StringDictEquals(a.OtherDetails, b.OtherDetails)) return false;
        return true;
    }

    // --- Translations ---

    private static bool TranslationsEqual(Dictionary<string, TranslationDetails>? a, Dictionary<string, TranslationDetails>? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return (a is null && b is { Count: 0 }) || (b is null && a is { Count: 0 });
        if (a.Count != b.Count) return false;
        foreach (KeyValuePair<string, TranslationDetails> kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out TranslationDetails? other)) return false;
            if (!TranslationDetailsEquals(kvp.Value, other)) return false;
        }
        return true;
    }

    private static bool TranslationDetailsEquals(TranslationDetails a, TranslationDetails b)
    {
        if (!string.Equals(a.Language, b.Language, System.StringComparison.Ordinal)) return false;
        if (!StringDictEquals(a.Author, b.Author)) return false;
        if (!StringListEquals(a.Accreditation, b.Accreditation)) return false;
        if (!StringDictEquals(a.OtherDetails, b.OtherDetails)) return false;
        if (!string.Equals(a.VersionLastTranslated ?? string.Empty, b.VersionLastTranslated ?? string.Empty, System.StringComparison.Ordinal)) return false;
        return true;
    }

    // --- Terminology ---

    private static bool TerminologyEquals(ArchetypeTerminology a, ArchetypeTerminology b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (!TermsByLangEquals(a.TermDefinitions, b.TermDefinitions)) return false;
        if (!TermsByLangEquals(a.ConstraintDefinitions, b.ConstraintDefinitions)) return false;
        if (!ValueSetsEqual(a.ValueSets, b.ValueSets)) return false;
        if (!BindingsEqual(a.TermBindings, b.TermBindings)) return false;
        if (!BindingsEqual(a.ConstraintBindings, b.ConstraintBindings)) return false;
        return true;
    }

    private static bool TermsByLangEquals(
        Dictionary<string, Dictionary<string, ArchetypeTerm>> a,
        Dictionary<string, Dictionary<string, ArchetypeTerm>> b)
    {
        if (a.Count != b.Count) return false;
        foreach (KeyValuePair<string, Dictionary<string, ArchetypeTerm>> kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out Dictionary<string, ArchetypeTerm>? other)) return false;
            if (kvp.Value.Count != other.Count) return false;
            foreach (KeyValuePair<string, ArchetypeTerm> termKvp in kvp.Value)
            {
                if (!other.TryGetValue(termKvp.Key, out ArchetypeTerm? otherTerm)) return false;
                if (!ArchetypeTermEquals(termKvp.Value, otherTerm)) return false;
            }
        }
        return true;
    }

    private static bool ArchetypeTermEquals(ArchetypeTerm a, ArchetypeTerm b)
        => string.Equals(a.Text ?? string.Empty, b.Text ?? string.Empty, System.StringComparison.Ordinal)
        && string.Equals(a.Description ?? string.Empty, b.Description ?? string.Empty, System.StringComparison.Ordinal)
        && string.Equals(a.Comment ?? string.Empty, b.Comment ?? string.Empty, System.StringComparison.Ordinal);

    private static bool ValueSetsEqual(Dictionary<string, ValueSet> a, Dictionary<string, ValueSet> b)
    {
        if (a.Count != b.Count) return false;
        foreach (KeyValuePair<string, ValueSet> kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out ValueSet? other)) return false;
            if (!string.Equals(kvp.Value.Id, other.Id, System.StringComparison.Ordinal)) return false;
            if (!StringListEquals(kvp.Value.Members, other.Members)) return false;
        }
        return true;
    }

    private static bool BindingsEqual(
        Dictionary<string, Dictionary<string, string>> a,
        Dictionary<string, Dictionary<string, string>> b)
    {
        if (a.Count != b.Count) return false;
        foreach (KeyValuePair<string, Dictionary<string, string>> kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out Dictionary<string, string>? other)) return false;
            if (!StringDictEquals(kvp.Value, other)) return false;
        }
        return true;
    }

    // --- Rules ---

    private static bool RulesEquals(RulesSection? a, RulesSection? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return string.Equals(
            (a.RawText ?? string.Empty).Trim(),
            (b.RawText ?? string.Empty).Trim(),
            System.StringComparison.Ordinal);
    }

    // --- cADL tree ---

    private static bool CObjectEquals(CObject? a, CObject? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (a.GetType() != b.GetType()) return false;
        if (!string.Equals(a.RmTypeName, b.RmTypeName, System.StringComparison.Ordinal)) return false;
        if (!string.Equals(a.NodeId ?? string.Empty, b.NodeId ?? string.Empty, System.StringComparison.Ordinal)) return false;
        if (!IntervalEquals(a.Occurrences, b.Occurrences)) return false;
        if (a.SiblingOrder != b.SiblingOrder) return false;

        switch (a)
        {
            case CArchetypeRoot ar:
                CArchetypeRoot br = (CArchetypeRoot)b;
                if (!string.Equals(ar.ArchetypeRef, br.ArchetypeRef, System.StringComparison.Ordinal)) return false;
                return ComplexBodyEquals(ar, br);
            case CComplexObject ccx:
                return ComplexBodyEquals(ccx, (CComplexObject)b);
            case ArchetypeSlot slot:
                ArchetypeSlot bs = (ArchetypeSlot)b;
                return AssertionListEquals(slot.Includes, bs.Includes)
                    && AssertionListEquals(slot.Excludes, bs.Excludes);
            case ArchetypeInternalRef iref:
                return string.Equals(iref.TargetPath, ((ArchetypeInternalRef)b).TargetPath, System.StringComparison.Ordinal);
            case CComplexObjectProxy proxy:
                return string.Equals(proxy.TargetPath, ((CComplexObjectProxy)b).TargetPath, System.StringComparison.Ordinal);
            case CString cs:
                CString bcs = (CString)b;
                return string.Equals(cs.Pattern ?? string.Empty, bcs.Pattern ?? string.Empty, System.StringComparison.Ordinal)
                    && StringListEquals(cs.EnumeratedValues, bcs.EnumeratedValues);
            case CInteger ci:
                CInteger bci = (CInteger)b;
                return IntervalEquals(ci.Range, bci.Range)
                    && IntListEquals(ci.EnumeratedValues, bci.EnumeratedValues);
            case CReal cr:
                CReal bcr = (CReal)b;
                return IntervalEquals(cr.Range, bcr.Range)
                    && DoubleListEquals(cr.EnumeratedValues, bcr.EnumeratedValues);
            case CBoolean cb:
                CBoolean bcb = (CBoolean)b;
                return cb.TrueValid == bcb.TrueValid && cb.FalseValid == bcb.FalseValid;
            case CTerminologyCode tc:
                CTerminologyCode btc = (CTerminologyCode)b;
                return string.Equals(tc.TerminologyId, btc.TerminologyId, System.StringComparison.Ordinal)
                    && string.Equals(tc.ValueSetRef ?? string.Empty, btc.ValueSetRef ?? string.Empty, System.StringComparison.Ordinal)
                    && StringListEquals(tc.EnumeratedValues, btc.EnumeratedValues);
        }
        return true;
    }

    private static bool ComplexBodyEquals(CComplexObject a, CComplexObject b)
    {
        if (a.Attributes.Count != b.Attributes.Count) return false;
        // Compare in canonical order (sorted by name + node-id).
        List<CAttribute> aa = SortAttributes(a.Attributes);
        List<CAttribute> bb = SortAttributes(b.Attributes);
        for (int i = 0; i < aa.Count; i++)
        {
            if (!CAttributeEquals(aa[i], bb[i])) return false;
        }
        if (a.AttributeTuples.Count != b.AttributeTuples.Count) return false;
        for (int i = 0; i < a.AttributeTuples.Count; i++)
        {
            if (!CAttributeTupleEquals(a.AttributeTuples[i], b.AttributeTuples[i])) return false;
        }
        return true;
    }

    private static List<CAttribute> SortAttributes(List<CAttribute> attrs)
    {
        List<CAttribute> sorted = [.. attrs];
        sorted.Sort(static (x, y) => string.CompareOrdinal(x.RmAttributeName, y.RmAttributeName));
        return sorted;
    }

    private static bool CAttributeEquals(CAttribute a, CAttribute b)
    {
        if (!string.Equals(a.RmAttributeName, b.RmAttributeName, System.StringComparison.Ordinal)) return false;
        if (!IntervalEquals(a.Existence, b.Existence)) return false;
        bool aMulti = a is CMultipleAttribute am;
        bool bMulti = b is CMultipleAttribute bm;
        if (aMulti != bMulti) return false;
        if (aMulti && bMulti)
        {
            Cardinality? ac = ((CMultipleAttribute)a).Cardinality;
            Cardinality? bc = ((CMultipleAttribute)b).Cardinality;
            if ((ac is null) != (bc is null)) return false;
            if (ac is not null && bc is not null)
            {
                if (!ac.Equals(bc)) return false;
            }
        }
        if (a.Children.Count != b.Children.Count) return false;
        List<CObject> aChildren = SortObjects(a.Children);
        List<CObject> bChildren = SortObjects(b.Children);
        for (int i = 0; i < aChildren.Count; i++)
        {
            if (!CObjectEquals(aChildren[i], bChildren[i])) return false;
        }
        return true;
    }

    private static List<CObject> SortObjects(List<CObject> objs)
    {
        List<CObject> sorted = [.. objs];
        sorted.Sort(static (x, y) =>
        {
            int c = string.CompareOrdinal(x.NodeId ?? string.Empty, y.NodeId ?? string.Empty);
            if (c != 0) return c;
            return string.CompareOrdinal(x.RmTypeName, y.RmTypeName);
        });
        return sorted;
    }

    private static bool CAttributeTupleEquals(CAttributeTuple a, CAttributeTuple b)
    {
        if (a.Members.Count != b.Members.Count) return false;
        if (a.Children.Count != b.Children.Count) return false;
        for (int i = 0; i < a.Members.Count; i++)
        {
            if (!string.Equals(a.Members[i].RmAttributeName, b.Members[i].RmAttributeName, System.StringComparison.Ordinal)) return false;
        }
        for (int i = 0; i < a.Children.Count; i++)
        {
            CObjectTuple ar = a.Children[i];
            CObjectTuple br = b.Children[i];
            if (ar.Members.Count != br.Members.Count) return false;
            for (int j = 0; j < ar.Members.Count; j++)
            {
                if (!CObjectEquals(ar.Members[j], br.Members[j])) return false;
            }
        }
        return true;
    }

    private static bool AssertionListEquals(List<Assertion> a, List<Assertion> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!string.Equals(NormalizeAssertion(a[i].RawText), NormalizeAssertion(b[i].RawText), System.StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static string NormalizeAssertion(string raw)
    {
        // Collapse all whitespace so newlines/indentation differences don't trip equality.
        System.Text.StringBuilder sb = new();
        bool lastSpace = false;
        foreach (char c in raw)
        {
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                if (!lastSpace && sb.Length > 0) sb.Append(' ');
                lastSpace = true;
            }
            else
            {
                sb.Append(c);
                lastSpace = false;
            }
        }
        return sb.ToString().Trim();
    }

    // --- Generic helpers ---

    private static bool StringDictEquals(Dictionary<string, string>? a, Dictionary<string, string>? b)
    {
        if (a is null && b is null) return true;
        if (a is null) return b!.Count == 0;
        if (b is null) return a.Count == 0;
        if (a.Count != b.Count) return false;
        foreach (KeyValuePair<string, string> kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out string? v)) return false;
            if (!string.Equals(kvp.Value, v, System.StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static bool StringListEquals(IList<string>? a, IList<string>? b)
    {
        if (a is null && b is null) return true;
        if (a is null) return b!.Count == 0;
        if (b is null) return a.Count == 0;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i], b[i], System.StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static bool IntListEquals(IList<int>? a, IList<int>? b)
    {
        if (a is null && b is null) return true;
        if (a is null) return b!.Count == 0;
        if (b is null) return a.Count == 0;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    private static bool DoubleListEquals(IList<double>? a, IList<double>? b)
    {
        if (a is null && b is null) return true;
        if (a is null) return b!.Count == 0;
        if (b is null) return a.Count == 0;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!a[i].Equals(b[i])) return false;
        }
        return true;
    }

    private static bool IntervalEquals<T>(Interval<T>? a, Interval<T>? b) where T : struct, System.IComparable<T>
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Equals(b);
    }
}
