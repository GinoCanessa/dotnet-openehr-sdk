using System.Xml;
using System.Xml.Linq;
using DotnetOpenEhr.Archetypes.Aom2.Resource;
using DotnetOpenEhr.Archetypes.Identification;
using DotnetOpenEhr.Bmm;

namespace DotnetOpenEhr.Templates;

/// <summary>
/// Internal translator that walks an OPT1.4 <see cref="XDocument"/>
/// and produces a populated <see cref="OperationalTemplate"/>. Split
/// from the public <see cref="Opt14XmlParser"/> façade purely for file
/// size — every public entry point on the façade routes through
/// <see cref="ParseCore"/>.
/// </summary>
internal static class Opt14XmlReader
{
    internal static readonly XNamespace OpenEhr = "http://schemas.openehr.org/v1";
    internal static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    internal static OperationalTemplate ParseCore(XDocument doc, BmmModel rmBmm, ParseOptions options)
    {
        XElement? root = doc.Root;
        if (root is null)
        {
            throw new InvalidOperationException("OPT1.4 document has no root element.");
        }

        // The XSD allows either <template> or <operational_template>
        // as the document root; both shape the same payload.
        bool isStrictNamespace = root.Name.Namespace == OpenEhr;
        bool isStrictLocalName = root.Name.LocalName is "template" or "operational_template";
        if (!isStrictLocalName)
        {
            throw new InvalidOperationException(
                $"OPT1.4 root element must be 'template' or 'operational_template' " +
                $"(got '{root.Name.LocalName}').");
        }
        if (!isStrictNamespace)
        {
            // Strict mode requires the canonical openEHR namespace.
            // Lenient mode tolerates a missing/foreign namespace and
            // falls back to local-name lookup downstream.
            if (!options.Lenient)
            {
                throw new InvalidOperationException(
                    $"OPT1.4 root element must be in the '{OpenEhr.NamespaceName}' " +
                    $"namespace (got '{root.Name.NamespaceName}'). Set " +
                    $"ParseOptions.Lenient = true to accept documents that drop the namespace.");
            }
        }

        OperationalTemplate result = new()
        {
            IsTemplate = true,
        };

        // ----- AuthoredResource header --------------------------------

        XElement? languageEl = FindChild(root, "language", options.Lenient);
        if (languageEl is not null)
        {
            string? langCode = FindCodeString(languageEl, options.Lenient);
            if (!string.IsNullOrEmpty(langCode))
            {
                result.OriginalLanguage = langCode;
            }
        }

        // Translations: repeating <translations> blocks at the
        // <template> level.
        foreach (XElement transEl in FindChildren(root, "translations", options.Lenient))
        {
            TranslationDetails? td = ReadTranslationDetails(transEl, options.Lenient);
            if (td is not null && !string.IsNullOrEmpty(td.Language))
            {
                result.Translations ??= [];
                result.Translations[td.Language] = td;
            }
        }

        XElement? descEl = FindChild(root, "description", options.Lenient);
        if (descEl is not null)
        {
            ReadDescription(descEl, result.Description, options.Lenient);
        }

        XElement? uidEl = FindChild(root, "uid", options.Lenient);
        if (uidEl is not null)
        {
            string? value = FindChildValue(uidEl, "value", options.Lenient);
            if (!string.IsNullOrEmpty(value))
            {
                result.Uid = value;
            }
        }

        XElement? controlledEl = FindChild(root, "is_controlled", options.Lenient);
        if (controlledEl is not null && bool.TryParse(controlledEl.Value.Trim(), out bool isCtrl))
        {
            result.IsControlled = isCtrl;
        }

        // ----- HeaderMetadata: OPT1.4 <template_id> / <concept> -------
        // OPT1.4's top-level <template_id>/<value> is the human-friendly
        // template name (e.g. "KDS_Vitalstatus"), NOT an archetype HRID.
        // Store it under HeaderMetadata so downstream callers that want
        // the friendly name can read it without trying to parse it as
        // an HRID.
        XElement? templateIdEl = FindChild(root, "template_id", options.Lenient);
        if (templateIdEl is not null)
        {
            string? friendly = FindChildValue(templateIdEl, "value", options.Lenient);
            if (!string.IsNullOrEmpty(friendly))
            {
                result.HeaderMetadata["template_id"] = friendly;
            }
        }
        XElement? conceptEl = FindChild(root, "concept", options.Lenient);
        if (conceptEl is not null)
        {
            string conceptText = (conceptEl.Value ?? string.Empty).Trim();
            if (conceptText.Length > 0)
            {
                result.HeaderMetadata["concept"] = conceptText;
            }
        }

        // ----- ArchetypeId from <definition>/<archetype_id>/<value> ---
        // In OPT1.4 the root <definition> carries its own archetype_id
        // directly (as a "root C_ARCHETYPE_ROOT" with the HRID at the
        // end of its content). This is what featurerequest criterion
        // (b) calls "the archetype concept_id" — i.e. the root
        // archetype's HRID concept-id segment.
        XElement? definitionEl = FindChild(root, "definition", options.Lenient);
        if (definitionEl is not null)
        {
            XElement? archIdEl = FindChild(definitionEl, "archetype_id", options.Lenient);
            string? hridText = archIdEl is null
                ? null
                : FindChildValue(archIdEl, "value", options.Lenient);
            if (!string.IsNullOrEmpty(hridText))
            {
                if (ArchetypeHRID.TryParse(hridText, out ArchetypeHRID? hrid))
                {
                    result.ArchetypeId = hrid;
                }
                else if (!options.Lenient)
                {
                    throw new InvalidOperationException(
                        $"OPT1.4 root archetype_id '{hridText}' is not a valid archetype HRID.");
                }
            }
        }

        // Phase 2: Definition recursion and Terminology harvesting are
        // intentionally deferred to Phases 3 and 4. The Initialize call
        // is deferred along with them.
        _ = rmBmm;
        return result;
    }

    // ------------------------------------------------------------------
    // Generic helpers: namespace-aware child lookup with lenient fallback.
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns the first child element of <paramref name="parent"/>
    /// with local name <paramref name="localName"/> in the canonical
    /// openEHR namespace. When <paramref name="lenient"/> is
    /// <see langword="true"/> and no namespaced child is found, falls
    /// back to a local-name-only match so documents that drop or
    /// remap the openEHR namespace still resolve.
    /// </summary>
    internal static XElement? FindChild(XElement parent, string localName, bool lenient)
    {
        XElement? match = parent.Element(OpenEhr + localName);
        if (match is not null)
        {
            return match;
        }
        if (lenient)
        {
            foreach (XElement child in parent.Elements())
            {
                if (string.Equals(child.Name.LocalName, localName, StringComparison.Ordinal))
                {
                    return child;
                }
            }
        }
        return null;
    }

    internal static IEnumerable<XElement> FindChildren(XElement parent, string localName, bool lenient)
    {
        bool anyCanonical = false;
        foreach (XElement child in parent.Elements(OpenEhr + localName))
        {
            anyCanonical = true;
            yield return child;
        }
        if (anyCanonical || !lenient)
        {
            yield break;
        }
        foreach (XElement child in parent.Elements())
        {
            if (string.Equals(child.Name.LocalName, localName, StringComparison.Ordinal))
            {
                yield return child;
            }
        }
    }

    /// <summary>
    /// Returns the trimmed text of the first child element with local
    /// name <paramref name="localName"/>, or <see langword="null"/>
    /// if no such child exists.
    /// </summary>
    internal static string? FindChildValue(XElement parent, string localName, bool lenient)
    {
        XElement? el = FindChild(parent, localName, lenient);
        if (el is null)
        {
            return null;
        }
        return (el.Value ?? string.Empty).Trim();
    }

    /// <summary>
    /// Reads the openEHR-shape <c>&lt;language&gt;/&lt;code_string&gt;</c>
    /// or <c>&lt;language&gt;/&lt;terminology_id&gt;/&lt;value&gt;</c>
    /// pair and returns the code string, which is the conventional
    /// language tag in this code base.
    /// </summary>
    internal static string? FindCodeString(XElement container, bool lenient)
    {
        // Convention: <code_string> sibling carries the ISO-639 tag;
        // <terminology_id>/<value> carries the terminology
        // identifier ("ISO_639-1"). We surface the code string.
        return FindChildValue(container, "code_string", lenient);
    }

    // ------------------------------------------------------------------
    // AuthoredResource sub-readers.
    // ------------------------------------------------------------------

    private static TranslationDetails? ReadTranslationDetails(XElement transEl, bool lenient)
    {
        XElement? langContainer = FindChild(transEl, "language", lenient);
        string? lang = langContainer is null ? null : FindCodeString(langContainer, lenient);
        if (string.IsNullOrEmpty(lang))
        {
            return null;
        }
        TranslationDetails td = new() { Language = lang };
        foreach (XElement authorEl in FindChildren(transEl, "author", lenient))
        {
            string? id = authorEl.Attribute("id")?.Value;
            if (!string.IsNullOrEmpty(id))
            {
                td.Author[id] = (authorEl.Value ?? string.Empty).Trim();
            }
        }
        foreach (XElement accEl in FindChildren(transEl, "accreditation", lenient))
        {
            td.Accreditation ??= [];
            td.Accreditation.Add((accEl.Value ?? string.Empty).Trim());
        }
        return td;
    }

    private static void ReadDescription(XElement descEl, ResourceDescription target, bool lenient)
    {
        string? lifecycle = FindChildValue(descEl, "lifecycle_state", lenient);
        if (!string.IsNullOrEmpty(lifecycle))
        {
            target.LifecycleState = lifecycle;
        }

        // <original_author id="key">value</original_author> repeats.
        foreach (XElement aut in FindChildren(descEl, "original_author", lenient))
        {
            string? id = aut.Attribute("id")?.Value;
            if (!string.IsNullOrEmpty(id))
            {
                target.OriginalAuthor[id] = (aut.Value ?? string.Empty).Trim();
            }
        }

        // <other_details id="key">value</other_details> at the
        // <description> level — copy into Description.OtherDetails for
        // round-trip fidelity.
        foreach (XElement od in FindChildren(descEl, "other_details", lenient))
        {
            string? id = od.Attribute("id")?.Value;
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }
            target.OtherDetails ??= [];
            target.OtherDetails[id] = (od.Value ?? string.Empty).Trim();
        }

        // <details> blocks: each carries a <language>/<code_string>
        // plus per-language description fields (purpose, keywords, …).
        foreach (XElement detail in FindChildren(descEl, "details", lenient))
        {
            XElement? langEl = FindChild(detail, "language", lenient);
            string? lang = langEl is null ? null : FindCodeString(langEl, lenient);
            if (string.IsNullOrEmpty(lang))
            {
                continue;
            }
            ResourceDescriptionItem item = new() { Language = lang };
            string? purpose = FindChildValue(detail, "purpose", lenient);
            if (!string.IsNullOrEmpty(purpose))
            {
                item.Purpose = purpose;
            }
            string? keywords = FindChildValue(detail, "keywords", lenient);
            if (!string.IsNullOrEmpty(keywords))
            {
                // OPT1.4 emits <keywords> as a single text node, sometimes
                // comma-separated; preserve the raw value rather than
                // guessing a delimiter.
                item.Keywords = [keywords];
            }
            string? use = FindChildValue(detail, "use", lenient);
            if (!string.IsNullOrEmpty(use))
            {
                item.Use = use;
            }
            string? misuse = FindChildValue(detail, "misuse", lenient);
            if (!string.IsNullOrEmpty(misuse))
            {
                item.Misuse = misuse;
            }
            string? copyright = FindChildValue(detail, "copyright", lenient);
            if (!string.IsNullOrEmpty(copyright))
            {
                item.Copyright = copyright;
            }
            target.Details[lang] = item;
        }
    }

    // Suppress "unused" warning for IXmlLineInfo until phases 3-5 wire
    // line-info into error messages.
    internal static (int Line, int Column) LineInfo(XElement el)
    {
        if (el is IXmlLineInfo info && info.HasLineInfo())
        {
            return (info.LineNumber, info.LinePosition);
        }
        return (0, 0);
    }
}
