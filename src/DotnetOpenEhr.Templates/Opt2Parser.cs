using DotnetOpenEhr.Archetypes.Adl2;
using DotnetOpenEhr.Archetypes.Aom2;
using DotnetOpenEhr.Archetypes.Aom2.Terminology;
using DotnetOpenEhr.Archetypes.Identification;
using DotnetOpenEhr.Bmm;
using DotnetOpenEhr.Bmm.Rm;
using DotnetOpenEhr.Odin;
using DotnetOpenEhr.Odin.Values;

namespace DotnetOpenEhr.Templates;

/// <summary>
/// Parser for openEHR Operational Template 2 (OPT2) sources. Reuses
/// <see cref="Adl2Parser"/> for the bulk of the work (header, language,
/// description, definition, rules, terminology, annotations) and adds
/// OPT2-specific handling for the <c>component_terminologies</c> block.
/// </summary>
/// <remarks>
/// OPT2 source format is ADL2-flavoured with the <c>operational_template</c>
/// header keyword and an additional top-level <c>component_terminologies</c>
/// section that the base ADL2 parser does not understand. This parser
/// extracts the <c>component_terminologies</c> block ahead of time
/// (replacing it with whitespace so source offsets stay aligned),
/// delegates to <see cref="Adl2Parser"/>, copies the resulting AOM2 tree
/// into a concrete <see cref="OperationalTemplate"/>, parses the
/// extracted block via <see cref="OdinParser"/>, and finally calls
/// <see cref="OperationalTemplate.Initialize(BmmModel)"/> against the
/// supplied (or default) RM BMM so the <see cref="OperationalTemplate.Nodes"/>
/// collection is populated before return.
/// </remarks>
public static class Opt2Parser
{
    /// <summary>
    /// Parses <paramref name="source"/> into a fully-initialised
    /// <see cref="OperationalTemplate"/>. Uses the canonical openEHR RM
    /// BMM bundled in <c>DotnetOpenEhr.Bmm.Rm</c> for polymorphism
    /// detection.
    /// </summary>
    public static OperationalTemplate Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Parse(source, OpenEhrRmBmm.LoadDefault());
    }

    /// <summary>
    /// Parses <paramref name="source"/> into a fully-initialised
    /// <see cref="OperationalTemplate"/>.
    /// </summary>
    public static OperationalTemplate Parse(ReadOnlySpan<char> source)
        => Parse(source.ToString());

    /// <summary>
    /// Parses <paramref name="source"/> using <paramref name="rmBmm"/>
    /// for polymorphism detection. Tests can substitute a focused BMM
    /// to control the resolution table.
    /// </summary>
    public static OperationalTemplate Parse(string source, BmmModel rmBmm)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(rmBmm);

        (string rewritten, string? componentTerminologiesBlock) = ExtractComponentTerminologies(source);

        Archetype parsed = Adl2Parser.Parse(rewritten);
        if (parsed is not DotnetOpenEhr.Archetypes.Aom2.OperationalTemplate aomOpt)
        {
            throw new InvalidOperationException(
                $"Source is not an operational_template (parsed as {parsed.GetType().Name}).");
        }

        OperationalTemplate result = new();
        CopyArchetypeMembers(aomOpt, result);

        if (componentTerminologiesBlock is not null)
        {
            result.ComponentTerminologies = ParseComponentTerminologies(componentTerminologiesBlock);
        }

        result.Initialize(rmBmm);
        return result;
    }

    // ------------------------------------------------------------------
    // Pre-pass: extract the component_terminologies block from the source.
    // The block sits at the top level (after terminology) and is laid out
    // as a single ODIN hash: `component_terminologies = <...>`. We scan
    // for the keyword and consume balanced angle brackets, accounting for
    // ODIN string literals so `<` / `>` inside a quoted value don't
    // confuse the bracket counter.
    // ------------------------------------------------------------------

    private static (string Rewritten, string? Block) ExtractComponentTerminologies(string source)
    {
        const string Keyword = "component_terminologies";
        int firstKw = -1;
        int searchStart = 0;
        while (true)
        {
            int kwPos = source.IndexOf(Keyword, searchStart, StringComparison.Ordinal);
            if (kwPos < 0)
            {
                break;
            }
            bool boundedLeft = kwPos == 0 || !IsIdentChar(source[kwPos - 1]);
            int afterKw = kwPos + Keyword.Length;
            bool boundedRight = afterKw == source.Length || !IsIdentChar(source[afterKw]);
            if (boundedLeft && boundedRight)
            {
                firstKw = kwPos;
                break;
            }
            searchStart = afterKw;
        }

        if (firstKw < 0)
        {
            return (source, null);
        }

        // Scan forward from the keyword to find the first '<' (skipping
        // whitespace, '=', comments, and any additional identical keyword
        // tokens — OPT2 layout is `component_terminologies` section header
        // followed by `component_terminologies = <hash>`).
        int p = firstKw + Keyword.Length;
        int openAngle = -1;
        while (p < source.Length)
        {
            char c = source[p];
            if (IsWs(c) || c == '=')
            {
                p++;
                continue;
            }
            if (c == '-' && p + 1 < source.Length && source[p + 1] == '-')
            {
                while (p < source.Length && source[p] != '\n') p++;
                continue;
            }
            if (IsIdentChar(c))
            {
                // Skip a following identifier (e.g. an inner duplicate of
                // the keyword) and continue.
                while (p < source.Length && IsIdentChar(source[p])) p++;
                continue;
            }
            if (c == '<')
            {
                openAngle = p;
                break;
            }
            // Anything else is unexpected layout — bail.
            return (source, null);
        }

        if (openAngle < 0)
        {
            return (source, null);
        }

        int closeAngle = FindBalancedClose(source, openAngle);
        if (closeAngle < 0)
        {
            throw new InvalidOperationException(
                "Unterminated 'component_terminologies' block.");
        }

        string block = source.Substring(openAngle, closeAngle - openAngle + 1);
        int spanStart = firstKw;
        int spanEnd = closeAngle + 1;
        // L7 — replace the `ToCharArray()` allocate-then-mutate-then-rebuild
        // with a single `string.Create` pass that writes directly into the
        // returned string's storage. Behaviour is preserved: every byte in
        // [spanStart, spanEnd) except `\r`/`\n` is rewritten to space so
        // downstream line/column reporting stays aligned with the source.
        string rewritten = string.Create(
            source.Length,
            (Src: source, Start: spanStart, End: spanEnd),
            static (span, state) =>
            {
                state.Src.AsSpan().CopyTo(span);
                for (int i = state.Start; i < state.End; i++)
                {
                    if (span[i] != '\r' && span[i] != '\n')
                    {
                        span[i] = ' ';
                    }
                }
            });
        return (rewritten, block);
    }

    private static int FindBalancedClose(string source, int openAngle)
    {
        int depth = 0;
        int i = openAngle;
        bool inString = false;
        while (i < source.Length)
        {
            char c = source[i];
            if (inString)
            {
                if (c == '\\' && i + 1 < source.Length)
                {
                    i += 2;
                    continue;
                }
                if (c == '"')
                {
                    inString = false;
                }
                i++;
                continue;
            }
            if (c == '"')
            {
                inString = true;
                i++;
                continue;
            }
            // ODIN line comment: -- ... \n
            if (c == '-' && i + 1 < source.Length && source[i + 1] == '-')
            {
                while (i < source.Length && source[i] != '\n') i++;
                continue;
            }
            if (c == '<')
            {
                depth++;
                i++;
                continue;
            }
            if (c == '>')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
                i++;
                continue;
            }
            i++;
        }
        return -1;
    }

    private static bool IsIdentChar(char c)
        => c == '_' || c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9');

    private static bool IsWs(char c)
        => c is ' ' or '\t' or '\r' or '\n';

    // ------------------------------------------------------------------
    // Parse the extracted component_terminologies block via ODIN. The
    // block is a hash keyed by archetype HRID; each entry mirrors a
    // standard terminology container (term_definitions, value_sets, ...).
    // We reuse the same shape walker as Adl2Parser does for terminology
    // sections, only inlined to avoid coupling to a private helper.
    // ------------------------------------------------------------------

    private static Dictionary<ArchetypeHRID, ArchetypeTerminology> ParseComponentTerminologies(string block)
    {
        Dictionary<ArchetypeHRID, ArchetypeTerminology> result = [];
        OdinValue parsed = OdinParser.Parse(block);
        if (parsed is not OdinHash hash)
        {
            return result;
        }

        foreach (KeyValuePair<string, OdinValue> entry in hash.Entries)
        {
            if (!ArchetypeHRID.TryParse(entry.Key, out ArchetypeHRID? hrid))
            {
                continue;
            }

            ArchetypeTerminology term = new();
            if (entry.Value is OdinObject obj)
            {
                if (obj.TryGet("term_definitions", out OdinValue? td) && td is OdinHash tdHash)
                {
                    term.TermDefinitions = ExtractTermsByLang(tdHash);
                }
                if (obj.TryGet("constraint_definitions", out OdinValue? cd) && cd is OdinHash cdHash)
                {
                    term.ConstraintDefinitions = ExtractTermsByLang(cdHash);
                }
                if (obj.TryGet("value_sets", out OdinValue? vs) && vs is OdinHash vsHash)
                {
                    term.ValueSets = ExtractValueSets(vsHash);
                }
                if (obj.TryGet("term_bindings", out OdinValue? tb) && tb is OdinHash tbHash)
                {
                    term.TermBindings = ExtractBindings(tbHash);
                }
                if (obj.TryGet("constraint_bindings", out OdinValue? cb) && cb is OdinHash cbHash)
                {
                    term.ConstraintBindings = ExtractBindings(cbHash);
                }
            }
            result[hrid] = term;
        }
        return result;
    }

    private static Dictionary<string, Dictionary<string, ArchetypeTerm>> ExtractTermsByLang(OdinHash hash)
    {
        Dictionary<string, Dictionary<string, ArchetypeTerm>> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, OdinValue> entry in hash.Entries)
        {
            if (entry.Value is OdinHash inner)
            {
                Dictionary<string, ArchetypeTerm> terms = new(StringComparer.Ordinal);
                foreach (KeyValuePair<string, OdinValue> e2 in inner.Entries)
                {
                    if (e2.Value is OdinObject obj)
                    {
                        ArchetypeTerm at = new();
                        if (obj.TryGet("text", out OdinValue? tv) && tv is OdinString tvs) at.Text = tvs.Value;
                        if (obj.TryGet("description", out OdinValue? dv) && dv is OdinString dvs) at.Description = dvs.Value;
                        if (obj.TryGet("comment", out OdinValue? cv) && cv is OdinString cvs) at.Comment = cvs.Value;
                        terms[e2.Key] = at;
                    }
                }
                result[entry.Key] = terms;
            }
        }
        return result;
    }

    private static Dictionary<string, ValueSet> ExtractValueSets(OdinHash hash)
    {
        Dictionary<string, ValueSet> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, OdinValue> entry in hash.Entries)
        {
            if (entry.Value is OdinObject obj)
            {
                ValueSet vs = new() { Id = entry.Key };
                if (obj.TryGet("id", out OdinValue? idv) && idv is OdinString idsv) vs.Id = idsv.Value;
                if (obj.TryGet("members", out OdinValue? mv))
                {
                    if (mv is OdinList ml)
                    {
                        List<string> members = [];
                        foreach (OdinValue item in ml.Items)
                        {
                            if (item is OdinString s) members.Add(s.Value);
                        }
                        vs.Members = members;
                    }
                    else if (mv is OdinString ms)
                    {
                        vs.Members = [ms.Value];
                    }
                }
                result[entry.Key] = vs;
            }
        }
        return result;
    }

    private static Dictionary<string, Dictionary<string, string>> ExtractBindings(OdinHash hash)
    {
        Dictionary<string, Dictionary<string, string>> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, OdinValue> entry in hash.Entries)
        {
            if (entry.Value is OdinHash inner)
            {
                Dictionary<string, string> map = new(StringComparer.Ordinal);
                foreach (KeyValuePair<string, OdinValue> e2 in inner.Entries)
                {
                    map[e2.Key] = OdinValueToText(e2.Value);
                }
                result[entry.Key] = map;
            }
        }
        return result;
    }

    private static string OdinValueToText(OdinValue value) => value switch
    {
        OdinString s => s.Value,
        OdinTerminologyCode tc => tc.Value.ToString(),
        _ => value.ToString() ?? string.Empty,
    };

    // ------------------------------------------------------------------
    // Copy every Archetype-base property from the parsed Aom2 tree into
    // the concrete Templates.OperationalTemplate. The two types share
    // the same property surface (Aom2.OperationalTemplate is the
    // abstract base), so this is a straight property-by-property copy
    // with no reflection.
    // ------------------------------------------------------------------

    private static void CopyArchetypeMembers(Archetype src, OperationalTemplate dst)
    {
        dst.SourceLine = src.SourceLine;
        dst.SourceColumn = src.SourceColumn;

        dst.OriginalLanguage = src.OriginalLanguage;
        dst.Translations = src.Translations;
        dst.Description = src.Description;
        dst.RevisionHistory = src.RevisionHistory;
        dst.IsControlled = src.IsControlled;
        dst.Uid = src.Uid;

        dst.ArchetypeId = src.ArchetypeId;
        dst.ParentArchetypeId = src.ParentArchetypeId;
        dst.IsTemplate = src.IsTemplate;
        dst.IsDifferential = src.IsDifferential;
        dst.Definition = src.Definition;
        dst.Terminology = src.Terminology;
        dst.Rules = src.Rules;
        dst.Annotations = src.Annotations;
        dst.HeaderMetadata = src.HeaderMetadata;
    }
}
