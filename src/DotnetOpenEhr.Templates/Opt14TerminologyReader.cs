using System.Xml.Linq;
using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using DotnetOpenEhr.Archetypes.Aom2.Terminology;
using DotnetOpenEhr.Archetypes.Identification;

namespace DotnetOpenEhr.Templates;

/// <summary>
/// Harvests OPT1.4 terminology from the three places it can live:
/// inline on the root <c>&lt;template&gt;</c> in a
/// <c>&lt;terminology&gt;</c> block (rare), inline on every
/// <c>C_ARCHETYPE_ROOT</c> (and on the root <c>&lt;definition&gt;</c>)
/// in flat <c>&lt;term_definitions code="…"&gt;</c> + optional
/// <c>&lt;term_bindings&gt;</c> elements, and in a top-level
/// <c>&lt;component_ontologies&gt;</c> / <c>&lt;component_terminologies&gt;</c>
/// block keyed by archetype HRID.
/// </summary>
internal static class Opt14TerminologyReader
{
    /// <summary>
    /// Walks <paramref name="archetypeRootSources"/> and
    /// <paramref name="root"/>'s top-level terminology blocks, merging
    /// the harvested per-archetype terminology containers into
    /// <paramref name="result"/>. The root archetype's terminology
    /// lands on <see cref="OperationalTemplate"/>'s base
    /// <c>Terminology</c>; every composed archetype's terminology
    /// lands on <see cref="OperationalTemplate.ComponentTerminologies"/>.
    /// </summary>
    internal static int Harvest(
        XElement root,
        OperationalTemplate result,
        IReadOnlyList<(XElement Source, CComplexObject Node)> archetypeRootSources,
        bool lenient)
    {
        int termDefinitionElementsSeen = 0;
        string defaultLang = string.IsNullOrEmpty(result.OriginalLanguage)
            ? "en"
            : result.OriginalLanguage;

        // ---- Source A: top-level <template>/<terminology> ----------
        XElement? topTerminology = Opt14XmlReader.FindChild(root, "terminology", lenient);
        if (topTerminology is not null)
        {
            MergeInto(result.Terminology, ReadTerminologyContainer(topTerminology, defaultLang, lenient, ref termDefinitionElementsSeen));
        }

        // ---- Source B: per-CArchetypeRoot in the definition tree ---
        ArchetypeHRID? rootHrid = result.ArchetypeId;
        foreach ((XElement Source, CComplexObject Node) in archetypeRootSources)
        {
            ArchetypeTerminology harvested = ReadTerminologyContainer(Source, defaultLang, lenient, ref termDefinitionElementsSeen);
            if (harvested.TermDefinitions.Count == 0
                && harvested.TermBindings.Count == 0
                && harvested.ConstraintDefinitions.Count == 0
                && harvested.ConstraintBindings.Count == 0
                && harvested.ValueSets.Count == 0)
            {
                continue;
            }

            ArchetypeHRID? sourceHrid = Node switch
            {
                CArchetypeRoot car when ArchetypeHRID.TryParse(car.ArchetypeRef, out ArchetypeHRID? parsed) => parsed,
                _ => null,
            };

            bool isRoot = sourceHrid is null
                ? Source.Parent is null || Source.Parent == root
                : rootHrid is not null && string.Equals(sourceHrid.ToString(), rootHrid.ToString(), StringComparison.Ordinal);

            if (isRoot)
            {
                MergeInto(result.Terminology, harvested);
            }
            else if (sourceHrid is not null)
            {
                if (!result.ComponentTerminologies.TryGetValue(sourceHrid, out ArchetypeTerminology? existing))
                {
                    existing = new ArchetypeTerminology();
                    result.ComponentTerminologies[sourceHrid] = existing;
                }
                MergeInto(existing, harvested);
            }
        }

        // ---- Source C: <component_ontologies> / <component_terminologies> ----
        foreach (string blockName in new[] { "component_ontologies", "component_terminologies" })
        {
            XElement? block = Opt14XmlReader.FindChild(root, blockName, lenient);
            if (block is null)
            {
                continue;
            }
            foreach (XElement entry in block.Elements())
            {
                string? hridText = entry.Attribute("id")?.Value
                    ?? Opt14XmlReader.FindChildValue(entry, "archetype_id", lenient)
                    ?? Opt14XmlReader.FindChildValue(entry, "id", lenient);
                if (string.IsNullOrEmpty(hridText)
                    || !ArchetypeHRID.TryParse(hridText, out ArchetypeHRID? hrid))
                {
                    continue;
                }
                ArchetypeTerminology harvested = ReadTerminologyContainer(entry, defaultLang, lenient, ref termDefinitionElementsSeen);
                if (!result.ComponentTerminologies.TryGetValue(hrid, out ArchetypeTerminology? existing))
                {
                    existing = new ArchetypeTerminology();
                    result.ComponentTerminologies[hrid] = existing;
                }
                MergeInto(existing, harvested);
            }
        }

        return termDefinitionElementsSeen;
    }

    // ------------------------------------------------------------------
    // Container reader: walks <term_definitions> / <term_bindings>
    // children of the supplied element.
    // ------------------------------------------------------------------

    private static ArchetypeTerminology ReadTerminologyContainer(
        XElement container,
        string defaultLang,
        bool lenient,
        ref int termDefinitionElementsSeen)
    {
        ArchetypeTerminology term = new();

        foreach (XElement td in Opt14XmlReader.FindChildren(container, "term_definitions", lenient))
        {
            termDefinitionElementsSeen++;
            string lang = td.Attribute("language")?.Value
                ?? container.Attribute("language")?.Value
                ?? defaultLang;
            string? code = td.Attribute("code")?.Value;
            if (string.IsNullOrEmpty(code))
            {
                foreach (XElement codeEl in Opt14XmlReader.FindChildren(td, "items", lenient))
                {
                    string? innerCode = codeEl.Attribute("code")?.Value;
                    if (string.IsNullOrEmpty(innerCode))
                    {
                        continue;
                    }
                    ArchetypeTerm at = ReadArchetypeTerm(codeEl, lenient);
                    Insert(term.TermDefinitions, lang, innerCode, at);
                }
            }
            else
            {
                ArchetypeTerm at = ReadArchetypeTerm(td, lenient);
                Insert(term.TermDefinitions, lang, code, at);
            }
        }

        foreach (XElement cd in Opt14XmlReader.FindChildren(container, "constraint_definitions", lenient))
        {
            string lang = cd.Attribute("language")?.Value ?? defaultLang;
            string? code = cd.Attribute("code")?.Value;
            if (!string.IsNullOrEmpty(code))
            {
                Insert(term.ConstraintDefinitions, lang, code, ReadArchetypeTerm(cd, lenient));
            }
        }

        foreach (XElement tb in Opt14XmlReader.FindChildren(container, "term_bindings", lenient))
        {
            string terminology = tb.Attribute("terminology")?.Value
                ?? Opt14XmlReader.FindChildValue(tb, "terminology", lenient)
                ?? string.Empty;
            if (terminology.Length == 0)
            {
                continue;
            }
            string? attrCode = tb.Attribute("code")?.Value;
            if (!string.IsNullOrEmpty(attrCode))
            {
                string uri = ReadBindingUri(tb, lenient);
                Insert(term.TermBindings, terminology, attrCode, uri);
                continue;
            }
            foreach (XElement codeEl in Opt14XmlReader.FindChildren(tb, "items", lenient))
            {
                string? innerCode = codeEl.Attribute("code")?.Value;
                if (string.IsNullOrEmpty(innerCode))
                {
                    continue;
                }
                Insert(term.TermBindings, terminology, innerCode, ReadBindingUri(codeEl, lenient));
            }
        }

        return term;
    }

    private static ArchetypeTerm ReadArchetypeTerm(XElement holder, bool lenient)
    {
        ArchetypeTerm term = new();
        // Attribute-encoded shape: <... text="..." description="..." comment="..." />
        string? text = holder.Attribute("text")?.Value;
        string? description = holder.Attribute("description")?.Value;
        string? comment = holder.Attribute("comment")?.Value;
        // Child-encoded shape: <items id="text|description|comment">value</items>
        foreach (XElement item in Opt14XmlReader.FindChildren(holder, "items", lenient))
        {
            string? id = item.Attribute("id")?.Value;
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }
            string value = (item.Value ?? string.Empty);
            switch (id)
            {
                case "text": text ??= value; break;
                case "description": description ??= value; break;
                case "comment": comment ??= value; break;
            }
        }
        if (!string.IsNullOrEmpty(text))
        {
            term.Text = text;
        }
        if (!string.IsNullOrEmpty(description))
        {
            term.Description = description;
        }
        if (!string.IsNullOrEmpty(comment))
        {
            term.Comment = comment;
        }
        return term;
    }

    private static string ReadBindingUri(XElement holder, bool lenient)
    {
        // Bindings can encode the target as an attribute, a direct text
        // child, or as a nested CODE_PHRASE shape with
        // <terminology_id>/<value> + <code_string>. Concatenate the
        // latter as "terminology::code" to round-trip into the
        // string-keyed AOM2 map.
        string? uri = holder.Attribute("value")?.Value;
        if (!string.IsNullOrEmpty(uri))
        {
            return uri;
        }
        string? valueChild = Opt14XmlReader.FindChildValue(holder, "value", lenient);
        if (!string.IsNullOrEmpty(valueChild))
        {
            return valueChild;
        }
        string? code = Opt14XmlReader.FindChildValue(holder, "code_string", lenient);
        string? termId = Opt14XmlReader.FindChildValue(
            Opt14XmlReader.FindChild(holder, "terminology_id", lenient) ?? holder, "value", lenient);
        if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(termId))
        {
            return $"{termId}::{code}";
        }
        return (holder.Value ?? string.Empty).Trim();
    }

    private static void Insert(
        Dictionary<string, Dictionary<string, ArchetypeTerm>> table,
        string lang,
        string code,
        ArchetypeTerm term)
    {
        if (!table.TryGetValue(lang, out Dictionary<string, ArchetypeTerm>? perLang))
        {
            perLang = new(StringComparer.Ordinal);
            table[lang] = perLang;
        }
        if (!perLang.TryGetValue(code, out ArchetypeTerm? existing))
        {
            perLang[code] = term;
            return;
        }
        // Merge: never overwrite an existing non-empty value with an
        // empty one, but let later writes fill in absent fields.
        if (!string.IsNullOrEmpty(term.Text) && string.IsNullOrEmpty(existing.Text)) existing.Text = term.Text;
        if (!string.IsNullOrEmpty(term.Description) && string.IsNullOrEmpty(existing.Description)) existing.Description = term.Description;
        if (!string.IsNullOrEmpty(term.Comment) && string.IsNullOrEmpty(existing.Comment)) existing.Comment = term.Comment;
    }

    private static void Insert(
        Dictionary<string, Dictionary<string, string>> table,
        string terminology,
        string code,
        string uri)
    {
        if (!table.TryGetValue(terminology, out Dictionary<string, string>? perTerm))
        {
            perTerm = new(StringComparer.Ordinal);
            table[terminology] = perTerm;
        }
        perTerm.TryAdd(code, uri);
    }

    private static void MergeInto(ArchetypeTerminology target, ArchetypeTerminology source)
    {
        foreach (KeyValuePair<string, Dictionary<string, ArchetypeTerm>> kvp in source.TermDefinitions)
        {
            foreach (KeyValuePair<string, ArchetypeTerm> term in kvp.Value)
            {
                Insert(target.TermDefinitions, kvp.Key, term.Key, term.Value);
            }
        }
        foreach (KeyValuePair<string, Dictionary<string, ArchetypeTerm>> kvp in source.ConstraintDefinitions)
        {
            foreach (KeyValuePair<string, ArchetypeTerm> term in kvp.Value)
            {
                Insert(target.ConstraintDefinitions, kvp.Key, term.Key, term.Value);
            }
        }
        foreach (KeyValuePair<string, Dictionary<string, string>> kvp in source.TermBindings)
        {
            foreach (KeyValuePair<string, string> b in kvp.Value)
            {
                Insert(target.TermBindings, kvp.Key, b.Key, b.Value);
            }
        }
        foreach (KeyValuePair<string, Dictionary<string, string>> kvp in source.ConstraintBindings)
        {
            foreach (KeyValuePair<string, string> b in kvp.Value)
            {
                Insert(target.ConstraintBindings, kvp.Key, b.Key, b.Value);
            }
        }
        foreach (KeyValuePair<string, ValueSet> vs in source.ValueSets)
        {
            target.ValueSets.TryAdd(vs.Key, vs.Value);
        }
        if (string.IsNullOrEmpty(target.OriginalLanguage) && !string.IsNullOrEmpty(source.OriginalLanguage))
        {
            target.OriginalLanguage = source.OriginalLanguage;
        }
    }
}
