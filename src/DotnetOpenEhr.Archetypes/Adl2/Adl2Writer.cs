using System.Globalization;
using System.IO;
using System.Text;
using DotnetOpenEhr.Archetypes.Aom2;
using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using DotnetOpenEhr.Archetypes.Aom2.Resource;
using DotnetOpenEhr.Archetypes.Aom2.Terminology;
using DotnetOpenEhr.Foundation;
using DotnetOpenEhr.Odin;
using DotnetOpenEhr.Odin.Values;

namespace DotnetOpenEhr.Archetypes.Adl2;

/// <summary>
/// Output ordering mode for <see cref="Adl2Writer"/>.
/// </summary>
public enum Adl2WriteMode
{
    /// <summary>
    /// Preserve source-position-derived ordering where known
    /// (SourceLine / SourceColumn populated). Nodes constructed
    /// programmatically (no source position) fall back to canonical
    /// ordering inline.
    /// </summary>
    Original,

    /// <summary>
    /// Deterministic ordering, idempotent. Two equal trees serialize to
    /// identical bytes.
    /// </summary>
    Canonical,
}

/// <summary>
/// Visitor-style serializer for the openEHR AOM2 tree to ADL 2 text.
/// Round-trips against <see cref="Adl2Parser"/>; see <see cref="Adl2WriteMode"/>
/// for ordering semantics.
/// </summary>
public static class Adl2Writer
{
    private const string IndentUnit = "    ";
    private const string Newline = "\n";

    /// <summary>
    /// Serialize <paramref name="archetype"/> to an in-memory string.
    /// </summary>
    public static string Write(Archetype archetype, Adl2WriteMode mode = Adl2WriteMode.Canonical)
    {
        ArgumentNullException.ThrowIfNull(archetype);
        StringBuilder sb = new(1024);
        using StringWriter sw = new(sb);
        Write(archetype, sw, mode);
        return sb.ToString();
    }

    /// <summary>
    /// Serialize <paramref name="archetype"/> to <paramref name="writer"/>.
    /// </summary>
    public static void Write(Archetype archetype, TextWriter writer, Adl2WriteMode mode = Adl2WriteMode.Canonical)
    {
        ArgumentNullException.ThrowIfNull(archetype);
        ArgumentNullException.ThrowIfNull(writer);

        WriteHeader(writer, archetype);
        if (archetype.ParentArchetypeId is not null)
        {
            WriteSpecialize(writer, archetype.ParentArchetypeId);
        }
        WriteLanguage(writer, archetype, mode);
        if (HasDescription(archetype.Description))
        {
            WriteDescription(writer, archetype.Description, mode);
        }
        WriteDefinition(writer, archetype.Definition, mode);
        if (archetype.Rules is not null && !string.IsNullOrWhiteSpace(archetype.Rules.RawText))
        {
            WriteRules(writer, archetype.Rules);
        }
        if (HasTerminology(archetype.Terminology))
        {
            WriteTerminology(writer, archetype.Terminology, mode);
        }
        if (archetype.Annotations is not null)
        {
            WriteAnnotations(writer);
        }
    }

    // ------------------------------------------------------------------
    // Header / specialize
    // ------------------------------------------------------------------

    private static void WriteHeader(TextWriter writer, Archetype archetype)
    {
        string keyword = archetype switch
        {
            Template => "template",
            TemplateOverlay => "template_overlay",
            OperationalTemplate => "operational_template",
            _ => "archetype",
        };
        writer.Write(keyword);
        if (archetype.IsDifferential)
        {
            writer.Write(' ');
            writer.Write("differential");
        }
        if (archetype.HeaderMetadata.Count > 0)
        {
            writer.Write(' ');
            writer.Write('(');
            bool first = true;
            foreach (KeyValuePair<string, string> kvp in archetype.HeaderMetadata)
            {
                if (!first)
                {
                    writer.Write("; ");
                }
                first = false;
                writer.Write(kvp.Key);
                if (!string.IsNullOrEmpty(kvp.Value))
                {
                    writer.Write('=');
                    writer.Write(kvp.Value);
                }
            }
            writer.Write(')');
        }
        writer.Write(Newline);
        writer.Write(IndentUnit);
        writer.Write(archetype.ArchetypeId.ToString());
        writer.Write(Newline);
    }

    private static void WriteSpecialize(TextWriter writer, DotnetOpenEhr.Archetypes.Identification.ArchetypeHRID parent)
    {
        writer.Write(Newline);
        writer.Write("specialize");
        writer.Write(Newline);
        writer.Write(IndentUnit);
        writer.Write(parent.ToString());
        writer.Write(Newline);
    }

    // ------------------------------------------------------------------
    // Language
    // ------------------------------------------------------------------

    private static void WriteLanguage(TextWriter writer, Archetype archetype, Adl2WriteMode mode)
    {
        writer.Write(Newline);
        writer.Write("language");
        writer.Write(Newline);

        string originalLanguage = !string.IsNullOrEmpty(archetype.OriginalLanguage)
            ? archetype.OriginalLanguage
            : "en";
        WriteOdinAttribute(writer, "original_language", BuildLocalCode("ISO_639-1", originalLanguage));

        if (archetype.Translations is { Count: > 0 } translations)
        {
            OdinHash hash = BuildTranslationsHash(translations, mode);
            WriteOdinAttribute(writer, "translations", hash);
        }
    }

    private static OdinHash BuildTranslationsHash(
        Dictionary<string, TranslationDetails> translations,
        Adl2WriteMode mode)
    {
        OdinHash hash = new() { KeyKind = OdinKind.String };
        foreach (KeyValuePair<string, TranslationDetails> entry in EnumerateMaybeSorted(translations, mode))
        {
            TranslationDetails td = entry.Value;
            OdinObject obj = new();
            obj.Attributes["language"] = BuildLocalCode("ISO_639-1", td.Language);
            if (td.Author.Count > 0)
            {
                obj.Attributes["author"] = BuildStringHash(td.Author, mode);
            }
            if (td.Accreditation is { Count: > 0 } accred)
            {
                obj.Attributes["accreditation"] = accred.Count == 1
                    ? new OdinString(accred[0])
                    : BuildStringList(accred);
            }
            if (td.OtherDetails is { Count: > 0 } other)
            {
                obj.Attributes["other_details"] = BuildStringHash(other, mode);
            }
            if (!string.IsNullOrEmpty(td.VersionLastTranslated))
            {
                obj.Attributes["version_last_translated"] = new OdinString(td.VersionLastTranslated);
            }
            hash.Entries[entry.Key] = obj;
        }
        return hash;
    }

    // ------------------------------------------------------------------
    // Description
    // ------------------------------------------------------------------

    private static bool HasDescription(ResourceDescription d)
        => d is not null
            && (!string.IsNullOrEmpty(d.LifecycleState)
                || d.OriginalAuthor.Count > 0
                || d.OtherContributors is { Count: > 0 }
                || d.Details.Count > 0
                || !string.IsNullOrEmpty(d.Copyright)
                || d.OtherDetails is { Count: > 0 }
                || !string.IsNullOrEmpty(d.ResourcePackageUri)
                || d.Licence is { Count: > 0 }
                || d.IpAcknowledgements is { Count: > 0 }
                || d.References is { Count: > 0 }
                || d.ConformsTo is { Count: > 0 });

    private static void WriteDescription(TextWriter writer, ResourceDescription d, Adl2WriteMode mode)
    {
        writer.Write(Newline);
        writer.Write("description");
        writer.Write(Newline);

        if (d.OriginalAuthor.Count > 0)
        {
            WriteOdinAttribute(writer, "original_author", BuildStringHash(d.OriginalAuthor, mode));
        }
        if (d.OtherContributors is { Count: > 0 } contributors)
        {
            WriteOdinAttribute(writer, "other_contributors", BuildStringList(contributors));
        }
        if (!string.IsNullOrEmpty(d.LifecycleState))
        {
            WriteOdinAttribute(writer, "lifecycle_state", new OdinString(d.LifecycleState));
        }
        if (!string.IsNullOrEmpty(d.ResourcePackageUri))
        {
            WriteOdinAttribute(writer, "resource_package_uri", new OdinString(d.ResourcePackageUri));
        }
        if (!string.IsNullOrEmpty(d.Copyright))
        {
            WriteOdinAttribute(writer, "copyright", new OdinString(d.Copyright));
        }
        if (d.Licence is { Count: > 0 } licence)
        {
            WriteOdinAttribute(writer, "licence", BuildStringHash(licence, mode));
        }
        if (d.IpAcknowledgements is { Count: > 0 } ip)
        {
            WriteOdinAttribute(writer, "ip_acknowledgements", BuildStringList(ip));
        }
        if (d.References is { Count: > 0 } refs)
        {
            WriteOdinAttribute(writer, "references", BuildStringList(refs));
        }
        if (d.ConformsTo is { Count: > 0 } conforms)
        {
            WriteOdinAttribute(writer, "conformance", BuildStringList(conforms));
        }
        if (d.OtherDetails is { Count: > 0 } odetails)
        {
            WriteOdinAttribute(writer, "other_details", BuildStringHash(odetails, mode));
        }
        if (d.Details.Count > 0)
        {
            OdinHash detailsHash = new() { KeyKind = OdinKind.String };
            foreach (KeyValuePair<string, ResourceDescriptionItem> entry in EnumerateMaybeSorted(d.Details, mode))
            {
                detailsHash.Entries[entry.Key] = BuildDescriptionItem(entry.Value, mode);
            }
            WriteOdinAttribute(writer, "details", detailsHash);
        }
    }

    private static OdinObject BuildDescriptionItem(ResourceDescriptionItem item, Adl2WriteMode mode)
    {
        OdinObject obj = new();
        obj.Attributes["language"] = BuildLocalCode("ISO_639-1", item.Language);
        obj.Attributes["purpose"] = new OdinString(item.Purpose ?? string.Empty);
        if (item.Keywords is { Count: > 0 } keywords)
        {
            obj.Attributes["keywords"] = BuildStringList(keywords);
        }
        if (item.Use is not null)
        {
            obj.Attributes["use"] = new OdinString(item.Use);
        }
        if (item.Misuse is not null)
        {
            obj.Attributes["misuse"] = new OdinString(item.Misuse);
        }
        if (item.Copyright is not null)
        {
            obj.Attributes["copyright"] = new OdinString(item.Copyright);
        }
        if (item.OriginalResourceUri is { Count: > 0 } uri)
        {
            obj.Attributes["original_resource_uri"] = BuildStringHash(uri, mode);
        }
        if (item.OtherDetails is { Count: > 0 } other)
        {
            obj.Attributes["other_details"] = BuildStringHash(other, mode);
        }
        return obj;
    }

    // ------------------------------------------------------------------
    // Definition (cADL)
    // ------------------------------------------------------------------

    private static void WriteDefinition(TextWriter writer, CComplexObject root, Adl2WriteMode mode)
    {
        writer.Write(Newline);
        writer.Write("definition");
        writer.Write(Newline);
        WriteIndent(writer, 1);
        WriteCObject(writer, root, 1, mode);
        writer.Write(Newline);
    }

    private static void WriteCObject(TextWriter writer, CObject obj, int indent, Adl2WriteMode mode)
    {
        switch (obj)
        {
            case CArchetypeRoot root:
                WriteCArchetypeRoot(writer, root, indent, mode);
                return;
            case CComplexObject ccx:
                WriteCComplexObject(writer, ccx, indent, mode);
                return;
            case ArchetypeSlot slot:
                WriteArchetypeSlot(writer, slot, indent);
                return;
            case ArchetypeInternalRef iref:
                WriteArchetypeInternalRef(writer, iref);
                return;
            case CComplexObjectProxy proxy:
                WriteComplexObjectProxy(writer, proxy);
                return;
            case CString cs:
                WriteCString(writer, cs);
                return;
            case CInteger ci:
                WriteCInteger(writer, ci);
                return;
            case CReal cr:
                WriteCReal(writer, cr);
                return;
            case CBoolean cb:
                WriteCBoolean(writer, cb);
                return;
            case CTerminologyCode tc:
                WriteCTerminologyCode(writer, tc);
                return;
            default:
                // Fallback: minimal RM_TYPE[id] header.
                writer.Write(obj.RmTypeName);
                if (!string.IsNullOrEmpty(obj.NodeId))
                {
                    writer.Write('[');
                    writer.Write(obj.NodeId);
                    writer.Write(']');
                }
                return;
        }
    }

    private static void WriteCComplexObject(TextWriter writer, CComplexObject ccx, int indent, Adl2WriteMode mode)
    {
        writer.Write(ccx.RmTypeName);
        if (!string.IsNullOrEmpty(ccx.NodeId))
        {
            writer.Write('[');
            writer.Write(ccx.NodeId);
            writer.Write(']');
        }
        WriteOccurrencesClause(writer, ccx.Occurrences);
        if (ccx.Attributes.Count == 0 && ccx.AttributeTuples.Count == 0)
        {
            writer.Write(" matches {}");
            return;
        }
        writer.Write(" matches {");
        writer.Write(Newline);
        foreach (CAttribute attr in OrderAttributes(ccx.Attributes, mode))
        {
            WriteIndent(writer, indent + 1);
            WriteCAttribute(writer, attr, indent + 1, mode);
            writer.Write(Newline);
        }
        foreach (CAttributeTuple tuple in ccx.AttributeTuples)
        {
            WriteIndent(writer, indent + 1);
            WriteAttributeTuple(writer, tuple, indent + 1, mode);
            writer.Write(Newline);
        }
        WriteIndent(writer, indent);
        writer.Write('}');
    }

    private static void WriteCArchetypeRoot(TextWriter writer, CArchetypeRoot root, int indent, Adl2WriteMode mode)
    {
        writer.Write(root.RmTypeName);
        writer.Write('[');
        writer.Write(root.NodeId ?? root.ArchetypeRef);
        writer.Write(']');
        WriteOccurrencesClause(writer, root.Occurrences);
        if (root.Attributes.Count == 0 && root.AttributeTuples.Count == 0)
        {
            writer.Write(" matches {}");
            return;
        }
        writer.Write(" matches {");
        writer.Write(Newline);
        foreach (CAttribute attr in OrderAttributes(root.Attributes, mode))
        {
            WriteIndent(writer, indent + 1);
            WriteCAttribute(writer, attr, indent + 1, mode);
            writer.Write(Newline);
        }
        WriteIndent(writer, indent);
        writer.Write('}');
    }

    private static void WriteCAttribute(TextWriter writer, CAttribute attr, int indent, Adl2WriteMode mode)
    {
        writer.Write(attr.RmAttributeName);
        if (attr.Existence is not null)
        {
            writer.Write(" existence matches {");
            WriteIntInterval(writer, attr.Existence);
            writer.Write('}');
        }
        if (attr is CMultipleAttribute multi && multi.Cardinality is not null)
        {
            writer.Write(" cardinality matches {");
            WriteIntInterval(writer, multi.Cardinality.Interval);
            writer.Write("; ");
            writer.Write(multi.Cardinality.IsOrdered ? "ordered" : "unordered");
            if (multi.Cardinality.IsUnique)
            {
                writer.Write("; unique");
            }
            writer.Write('}');
        }
        if (attr.Children.Count == 0)
        {
            writer.Write(" matches {}");
            return;
        }
        writer.Write(" matches {");
        writer.Write(Newline);
        foreach (CObject child in OrderObjects(attr.Children, mode))
        {
            WriteIndent(writer, indent + 1);
            WriteCObject(writer, child, indent + 1, mode);
            writer.Write(Newline);
        }
        WriteIndent(writer, indent);
        writer.Write('}');
    }

    private static void WriteAttributeTuple(TextWriter writer, CAttributeTuple tuple, int indent, Adl2WriteMode mode)
    {
        writer.Write('[');
        for (int i = 0; i < tuple.Members.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(", ");
            }
            writer.Write(tuple.Members[i].RmAttributeName);
        }
        writer.Write("] matches {");
        writer.Write(Newline);
        foreach (CObjectTuple row in tuple.Children)
        {
            WriteIndent(writer, indent + 1);
            writer.Write('[');
            for (int i = 0; i < row.Members.Count; i++)
            {
                if (i > 0)
                {
                    writer.Write(", ");
                }
                writer.Write('{');
                WriteCObject(writer, row.Members[i], indent + 1, mode);
                writer.Write('}');
            }
            writer.Write(']');
            writer.Write(Newline);
        }
        WriteIndent(writer, indent);
        writer.Write('}');
    }

    private static void WriteArchetypeSlot(TextWriter writer, ArchetypeSlot slot, int indent)
    {
        writer.Write("allow_archetype ");
        writer.Write(slot.RmTypeName);
        if (!string.IsNullOrEmpty(slot.NodeId))
        {
            writer.Write('[');
            writer.Write(slot.NodeId);
            writer.Write(']');
        }
        WriteOccurrencesClause(writer, slot.Occurrences);
        if (slot.Includes.Count == 0 && slot.Excludes.Count == 0)
        {
            writer.Write(" matches {}");
            return;
        }
        writer.Write(" matches {");
        writer.Write(Newline);
        foreach (Assertion inc in slot.Includes)
        {
            WriteIndent(writer, indent + 1);
            writer.Write("include");
            writer.Write(Newline);
            WriteIndent(writer, indent + 2);
            writer.Write(inc.RawText);
            writer.Write(Newline);
        }
        foreach (Assertion exc in slot.Excludes)
        {
            WriteIndent(writer, indent + 1);
            writer.Write("exclude");
            writer.Write(Newline);
            WriteIndent(writer, indent + 2);
            writer.Write(exc.RawText);
            writer.Write(Newline);
        }
        WriteIndent(writer, indent);
        writer.Write('}');
    }

    private static void WriteArchetypeInternalRef(TextWriter writer, ArchetypeInternalRef iref)
    {
        writer.Write("use_node ");
        writer.Write(iref.RmTypeName);
        if (!string.IsNullOrEmpty(iref.NodeId))
        {
            writer.Write('[');
            writer.Write(iref.NodeId);
            writer.Write(']');
        }
        WriteOccurrencesClause(writer, iref.Occurrences);
        if (!string.IsNullOrEmpty(iref.TargetPath))
        {
            writer.Write(' ');
            writer.Write(iref.TargetPath);
        }
    }

    private static void WriteComplexObjectProxy(TextWriter writer, CComplexObjectProxy proxy)
    {
        writer.Write("use_archetype ");
        writer.Write(proxy.RmTypeName);
        if (!string.IsNullOrEmpty(proxy.NodeId))
        {
            writer.Write('[');
            writer.Write(proxy.NodeId);
            writer.Write(']');
        }
        WriteOccurrencesClause(writer, proxy.Occurrences);
        if (!string.IsNullOrEmpty(proxy.TargetPath))
        {
            writer.Write(' ');
            writer.Write(proxy.TargetPath);
        }
    }

    private static void WriteOccurrencesClause(TextWriter writer, Interval<int>? occurrences)
    {
        if (occurrences is null) return;
        writer.Write(" occurrences matches {");
        WriteIntInterval(writer, occurrences);
        writer.Write('}');
    }

    // ------------------------------------------------------------------
    // c_primitive_object writers
    // ------------------------------------------------------------------

    private static void WriteCString(TextWriter writer, CString cs)
    {
        if (cs.Pattern is not null)
        {
            writer.Write('/');
            writer.Write(cs.Pattern);
            writer.Write('/');
            return;
        }
        if (cs.EnumeratedValues is { Count: > 0 } values)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) writer.Write(", ");
                writer.Write('"');
                writer.Write(EscapeString(values[i]));
                writer.Write('"');
            }
            return;
        }
        // Open
    }

    private static void WriteCInteger(TextWriter writer, CInteger ci)
    {
        if (ci.Range is not null)
        {
            writer.Write('|');
            WriteIntInterval(writer, ci.Range);
            writer.Write('|');
            return;
        }
        if (ci.EnumeratedValues is { Count: > 0 } values)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) writer.Write(", ");
                writer.Write(values[i].ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    private static void WriteCReal(TextWriter writer, CReal cr)
    {
        if (cr.Range is not null)
        {
            writer.Write('|');
            WriteRealInterval(writer, cr.Range);
            writer.Write('|');
            return;
        }
        if (cr.EnumeratedValues is { Count: > 0 } values)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) writer.Write(", ");
                writer.Write(FormatReal(values[i]));
            }
        }
    }

    private static void WriteCBoolean(TextWriter writer, CBoolean cb)
    {
        if (cb.TrueValid && cb.FalseValid)
        {
            writer.Write("true, false");
        }
        else if (cb.TrueValid)
        {
            writer.Write("true");
        }
        else if (cb.FalseValid)
        {
            writer.Write("false");
        }
    }

    private static void WriteCTerminologyCode(TextWriter writer, CTerminologyCode tc)
    {
        writer.Write('[');
        bool wroteTerminology = false;
        if (!string.IsNullOrEmpty(tc.TerminologyId)
            && !string.Equals(tc.TerminologyId, "local", StringComparison.Ordinal))
        {
            writer.Write(tc.TerminologyId);
            writer.Write("::");
            wroteTerminology = true;
        }
        if (!string.IsNullOrEmpty(tc.ValueSetRef))
        {
            writer.Write(tc.ValueSetRef);
            if (tc.DefaultValue is not null)
            {
                writer.Write("; ");
                writer.Write(tc.DefaultValue);
            }
        }
        else if (tc.EnumeratedValues is { Count: > 0 } values)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) writer.Write(", ");
                writer.Write(values[i]);
            }
        }
        else if (!wroteTerminology)
        {
            // empty -> would produce '[]' which is invalid; bail.
        }
        writer.Write(']');
    }

    // ------------------------------------------------------------------
    // Rules
    // ------------------------------------------------------------------

    private static void WriteRules(TextWriter writer, RulesSection rules)
    {
        writer.Write(Newline);
        writer.Write("rules");
        writer.Write(Newline);
        string raw = rules.RawText.TrimEnd();
        // Re-indent: if the raw text already starts with whitespace per line,
        // keep it as-is; otherwise indent each line by one unit.
        foreach (string line in raw.Split('\n'))
        {
            if (line.Length == 0)
            {
                writer.Write(Newline);
                continue;
            }
            if (line[0] != ' ' && line[0] != '\t')
            {
                writer.Write(IndentUnit);
            }
            writer.Write(line.TrimEnd('\r'));
            writer.Write(Newline);
        }
    }

    // ------------------------------------------------------------------
    // Terminology
    // ------------------------------------------------------------------

    private static bool HasTerminology(ArchetypeTerminology t)
        => t is not null
            && (t.TermDefinitions.Count > 0
                || t.ConstraintDefinitions.Count > 0
                || t.ValueSets.Count > 0
                || t.TermBindings.Count > 0
                || t.ConstraintBindings.Count > 0);

    private static void WriteTerminology(TextWriter writer, ArchetypeTerminology t, Adl2WriteMode mode)
    {
        writer.Write(Newline);
        writer.Write("terminology");
        writer.Write(Newline);

        WriteOdinAttribute(writer, "term_definitions", BuildTermsByLang(t.TermDefinitions, mode));
        if (t.ConstraintDefinitions.Count > 0)
        {
            WriteOdinAttribute(writer, "constraint_definitions", BuildTermsByLang(t.ConstraintDefinitions, mode));
        }
        if (t.ValueSets.Count > 0)
        {
            WriteOdinAttribute(writer, "value_sets", BuildValueSets(t.ValueSets, mode));
        }
        if (t.TermBindings.Count > 0)
        {
            WriteOdinAttribute(writer, "term_bindings", BuildBindings(t.TermBindings, mode));
        }
        if (t.ConstraintBindings.Count > 0)
        {
            WriteOdinAttribute(writer, "constraint_bindings", BuildBindings(t.ConstraintBindings, mode));
        }
    }

    private static OdinHash BuildTermsByLang(
        Dictionary<string, Dictionary<string, ArchetypeTerm>> data,
        Adl2WriteMode mode)
    {
        OdinHash outer = new() { KeyKind = OdinKind.String };
        foreach (KeyValuePair<string, Dictionary<string, ArchetypeTerm>> langEntry in EnumerateMaybeSorted(data, mode))
        {
            OdinHash inner = new() { KeyKind = OdinKind.String };
            foreach (KeyValuePair<string, ArchetypeTerm> e in EnumerateMaybeSorted(langEntry.Value, mode))
            {
                OdinObject termObj = new();
                termObj.Attributes["text"] = new OdinString(e.Value.Text ?? string.Empty);
                if (e.Value.Description is not null)
                {
                    termObj.Attributes["description"] = new OdinString(e.Value.Description);
                }
                if (e.Value.Comment is not null)
                {
                    termObj.Attributes["comment"] = new OdinString(e.Value.Comment);
                }
                inner.Entries[e.Key] = termObj;
            }
            outer.Entries[langEntry.Key] = inner;
        }
        return outer;
    }

    private static OdinHash BuildValueSets(Dictionary<string, ValueSet> data, Adl2WriteMode mode)
    {
        OdinHash hash = new() { KeyKind = OdinKind.String };
        foreach (KeyValuePair<string, ValueSet> entry in EnumerateMaybeSorted(data, mode))
        {
            OdinObject obj = new();
            obj.Attributes["id"] = new OdinString(entry.Value.Id);
            OdinList members = new() { HasContinuationMarker = false };
            foreach (string m in entry.Value.Members)
            {
                members.Items.Add(new OdinString(m));
            }
            obj.Attributes["members"] = members;
            hash.Entries[entry.Key] = obj;
        }
        return hash;
    }

    private static OdinHash BuildBindings(
        Dictionary<string, Dictionary<string, string>> data,
        Adl2WriteMode mode)
    {
        OdinHash outer = new() { KeyKind = OdinKind.String };
        foreach (KeyValuePair<string, Dictionary<string, string>> termEntry in EnumerateMaybeSorted(data, mode))
        {
            OdinHash inner = new() { KeyKind = OdinKind.String };
            foreach (KeyValuePair<string, string> e in EnumerateMaybeSorted(termEntry.Value, mode))
            {
                inner.Entries[e.Key] = new OdinString(e.Value);
            }
            outer.Entries[termEntry.Key] = inner;
        }
        return outer;
    }

    // ------------------------------------------------------------------
    // Annotations
    // ------------------------------------------------------------------

    private static void WriteAnnotations(TextWriter writer)
    {
        writer.Write(Newline);
        writer.Write("annotations");
        writer.Write(Newline);
        // We don't have a full structured shape for annotations yet; emit
        // an empty documentation block so the section round-trips.
        WriteOdinAttribute(writer, "documentation", new OdinHash { KeyKind = OdinKind.String });
    }

    // ------------------------------------------------------------------
    // ODIN helpers
    // ------------------------------------------------------------------

    private static void WriteOdinAttribute(TextWriter writer, string name, OdinValue value)
    {
        writer.Write(IndentUnit);
        writer.Write(name);
        writer.Write(" = ");
        string odinText = OdinWriter.Write(value, new OdinWriteOptions
        {
            Indent = true,
            IndentUnit = IndentUnit,
            InlineLists = true,
            NewLine = Newline,
        });
        // OdinWriter on an OdinObject (top-level, no TypeMarker) emits
        // attributes one-per-line WITHOUT wrapping <…>. We need the
        // wrapped form here so the ADL parser's ConsumeOdinPairs reads a
        // single OdinBlock token. Wrap non-block values explicitly when
        // the writer's top-level mode produced bare attributes.
        bool needsWrap = value is OdinObject obj
            && obj.TypeMarker is null
            && obj.Attributes.Count > 0;
        bool needsHashWrap = value is OdinHash hash
            && hash.TypeMarker is null
            && hash.Entries.Count > 0;
        if (needsWrap || needsHashWrap)
        {
            writer.Write('<');
            writer.Write(Newline);
            // Re-indent each line by one extra level.
            foreach (string line in odinText.Split('\n'))
            {
                if (line.Length == 0)
                {
                    writer.Write(Newline);
                    continue;
                }
                writer.Write(IndentUnit);
                writer.Write(line);
                writer.Write(Newline);
            }
            writer.Write(IndentUnit);
            writer.Write('>');
        }
        else
        {
            writer.Write(odinText);
        }
        writer.Write(Newline);
    }

    private static OdinTerminologyCode BuildLocalCode(string terminologyId, string code)
    {
        TerminologyCode tc = new(terminologyId, code);
        return new OdinTerminologyCode(tc);
    }

    private static OdinHash BuildStringHash(Dictionary<string, string> data, Adl2WriteMode mode)
    {
        OdinHash hash = new() { KeyKind = OdinKind.String };
        foreach (KeyValuePair<string, string> e in EnumerateMaybeSorted(data, mode))
        {
            hash.Entries[e.Key] = new OdinString(e.Value);
        }
        return hash;
    }

    private static OdinList BuildStringList(IList<string> items)
    {
        OdinList list = new();
        foreach (string s in items)
        {
            list.Items.Add(new OdinString(s));
        }
        return list;
    }

    private static IEnumerable<KeyValuePair<string, TValue>> EnumerateMaybeSorted<TValue>(
        Dictionary<string, TValue> dict,
        Adl2WriteMode mode)
    {
        if (mode == Adl2WriteMode.Canonical)
        {
            List<KeyValuePair<string, TValue>> sorted = [.. dict];
            sorted.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));
            return sorted;
        }
        return dict;
    }

    // ------------------------------------------------------------------
    // Interval emission
    // ------------------------------------------------------------------

    private static void WriteIntInterval(TextWriter writer, Interval<int> interval)
    {
        WriteIntervalGeneric(writer, interval, static v => v.ToString(CultureInfo.InvariantCulture));
    }

    private static void WriteRealInterval(TextWriter writer, Interval<double> interval)
    {
        WriteIntervalGeneric(writer, interval, FormatReal);
    }

    private static void WriteIntervalGeneric<T>(
        TextWriter writer,
        Interval<T> interval,
        Func<T, string> formatter)
        where T : struct, IComparable<T>
    {
        if (!interval.HasLower && !interval.HasUpper)
        {
            writer.Write('*');
            return;
        }
        if (interval.HasLower && interval.HasUpper)
        {
            T lower = interval.Lower;
            T upper = interval.Upper;
            if (interval.LowerIncluded && interval.UpperIncluded && lower.CompareTo(upper) == 0)
            {
                writer.Write(formatter(lower));
                return;
            }
            if (!interval.LowerIncluded) writer.Write('>');
            writer.Write(formatter(lower));
            writer.Write("..");
            if (!interval.UpperIncluded) writer.Write('<');
            writer.Write(formatter(upper));
            return;
        }
        if (interval.HasLower)
        {
            if (!interval.LowerIncluded) writer.Write('>');
            writer.Write(formatter(interval.Lower));
            writer.Write("..*");
            return;
        }
        writer.Write("*..");
        if (!interval.UpperIncluded) writer.Write('<');
        writer.Write(formatter(interval.Upper));
    }

    private static string FormatReal(double value)
    {
        string s = value.ToString("R", CultureInfo.InvariantCulture);
        if (s.IndexOf('.') < 0 && s.IndexOf('e') < 0 && s.IndexOf('E') < 0)
        {
            s += ".0";
        }
        return s;
    }

    private static string EscapeString(string value)
    {
        StringBuilder sb = new(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Ordering
    // ------------------------------------------------------------------

    private static IEnumerable<CAttribute> OrderAttributes(List<CAttribute> attrs, Adl2WriteMode mode)
    {
        if (mode == Adl2WriteMode.Original)
        {
            return OrderByPositionThenCanonical(attrs, static a => (a.SourceLine, a.SourceColumn),
                static (a, b) => string.CompareOrdinal(a.RmAttributeName, b.RmAttributeName));
        }
        List<CAttribute> sorted = [.. attrs];
        sorted.Sort(static (a, b) => string.CompareOrdinal(a.RmAttributeName, b.RmAttributeName));
        return sorted;
    }

    private static IEnumerable<CObject> OrderObjects(List<CObject> objs, Adl2WriteMode mode)
    {
        if (mode == Adl2WriteMode.Original)
        {
            return OrderByPositionThenCanonical(objs, static o => (o.SourceLine, o.SourceColumn),
                static (a, b) => string.CompareOrdinal(a.NodeId ?? string.Empty, b.NodeId ?? string.Empty));
        }
        List<CObject> sorted = [.. objs];
        sorted.Sort(static (a, b) => string.CompareOrdinal(a.NodeId ?? string.Empty, b.NodeId ?? string.Empty));
        return sorted;
    }

    private static IEnumerable<T> OrderByPositionThenCanonical<T>(
        List<T> items,
        Func<T, (int Line, int Col)> positionAccessor,
        Comparison<T> canonicalComparer)
    {
        List<T> withPos = [];
        List<T> withoutPos = [];
        foreach (T item in items)
        {
            (int line, int _) = positionAccessor(item);
            if (line > 0) withPos.Add(item);
            else withoutPos.Add(item);
        }
        withPos.Sort((a, b) =>
        {
            (int al, int ac) = positionAccessor(a);
            (int bl, int bc) = positionAccessor(b);
            int cmp = al.CompareTo(bl);
            if (cmp != 0) return cmp;
            return ac.CompareTo(bc);
        });
        withoutPos.Sort(canonicalComparer);
        foreach (T t in withPos) yield return t;
        foreach (T t in withoutPos) yield return t;
    }

    private static void WriteIndent(TextWriter writer, int level)
    {
        for (int i = 0; i < level; i++)
        {
            writer.Write(IndentUnit);
        }
    }
}
