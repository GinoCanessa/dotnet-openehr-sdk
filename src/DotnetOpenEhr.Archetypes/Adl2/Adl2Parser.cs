using System.Globalization;
using System.Text;
using DotnetOpenEhr.Archetypes.Aom2;
using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using DotnetOpenEhr.Archetypes.Aom2.Resource;
using DotnetOpenEhr.Archetypes.Aom2.Terminology;
using DotnetOpenEhr.Archetypes.Identification;
using DotnetOpenEhr.Foundation;
using DotnetOpenEhr.Odin;
using DotnetOpenEhr.Odin.Values;

namespace DotnetOpenEhr.Archetypes.Adl2;

/// <summary>
/// Hand-written, single-pass recursive-descent parser for openEHR ADL 2.
/// Consumes the token stream produced by <see cref="Adl2Lexer"/> and
/// builds the AOM2 tree rooted at an <see cref="Archetype"/> subclass.
/// Embedded <c>language</c> / <c>description</c> / <c>terminology</c> /
/// <c>annotations</c> ODIN blocks are delegated to
/// <see cref="OdinParser"/>; the <c>rules</c> section is captured as
/// raw text in v1.
/// </summary>
public static class Adl2Parser
{
    public static Archetype Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return ParseInternal(source);
    }

    public static Archetype Parse(ReadOnlySpan<char> source)
        => ParseInternal(source.ToString());

    // ------------------------------------------------------------------
    // Token materialisation (Adl2Token is a ref struct; we snapshot it
    // into a POCO so the parser can keep a list/index over it).
    // ------------------------------------------------------------------

    private sealed class Adl2TokenInfo
    {
        public Adl2TokenKind Kind { get; init; }
        public string Text { get; init; } = string.Empty;
        public string? Value { get; init; }
        public string? EmbeddedNodeId { get; init; }
        public int Line { get; init; }
        public int Column { get; init; }
        public int Start { get; init; }
        public int Length { get; init; }
    }

    // ------------------------------------------------------------------
    // Pre-pass: extract the header metadata block. The lexer rejects
    // multi-dot version literals (e.g. 2.0.6) which appear in
    // (adl_version=2.0.6; rm_release=1.1.0). We slice the metadata text
    // out, replace it with same-length whitespace so source offsets
    // stay aligned, and hand-parse the key/value pairs ourselves.
    // ------------------------------------------------------------------

    private static (string rewritten, Dictionary<string, string> metadata, int metaStart, int metaEnd, bool isDifferential) ExtractHeaderMetadata(string source)
    {
        ReadOnlySpan<string> headerKeywords = ["archetype", "template_overlay", "operational_template", "template"];
        int pos = 0;
        // Skip BOM / leading whitespace / comments
        while (pos < source.Length)
        {
            char c = source[pos];
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                pos++;
                continue;
            }
            if (c == '-' && pos + 1 < source.Length && source[pos + 1] == '-')
            {
                while (pos < source.Length && source[pos] != '\n')
                {
                    pos++;
                }
                continue;
            }
            break;
        }

        string? matched = null;
        foreach (string kw in headerKeywords)
        {
            if (pos + kw.Length <= source.Length
                && source.AsSpan(pos, kw.Length).SequenceEqual(kw)
                && (pos + kw.Length == source.Length || !IsIdentChar(source[pos + kw.Length])))
            {
                matched = kw;
                break;
            }
        }

        if (matched is null)
        {
            return (source, [], -1, -1, false);
        }

        int afterKeyword = pos + matched.Length;
        // skip horizontal whitespace
        int p = afterKeyword;
        while (p < source.Length && (source[p] == ' ' || source[p] == '\t'))
        {
            p++;
        }

        // Optional "differential" keyword
        const string differential = "differential";
        bool isDifferential = false;
        int differentialStart = -1;
        int differentialEnd = -1;
        if (p + differential.Length <= source.Length
            && source.AsSpan(p, differential.Length).SequenceEqual(differential)
            && (p + differential.Length == source.Length || !IsIdentChar(source[p + differential.Length])))
        {
            isDifferential = true;
            differentialStart = p;
            differentialEnd = p + differential.Length;
            p += differential.Length;
            while (p < source.Length && (source[p] == ' ' || source[p] == '\t'))
            {
                p++;
            }
        }

        if (p >= source.Length || source[p] != '(')
        {
            // No metadata parens. We still need to blank out 'differential'
            // if we matched it, because the lexer would mis-tokenise it
            // as an archetype HRID literal otherwise.
            if (isDifferential)
            {
                StringBuilder rewritten2 = new(source);
                for (int i = differentialStart; i < differentialEnd; i++)
                {
                    rewritten2[i] = ' ';
                }
                return (rewritten2.ToString(), [], -1, -1, true);
            }
            return (source, [], -1, -1, false);
        }

        int parenStart = p;
        int depth = 0;
        int q = p;
        while (q < source.Length)
        {
            char c = source[q];
            if (c == '(') depth++;
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    break;
                }
            }
            q++;
        }
        if (depth != 0)
        {
            // Let the lexer report a sensible error
            return (source, [], -1, -1, false);
        }

        int parenEnd = q; // index of ')'
        string inner = source.Substring(parenStart + 1, parenEnd - parenStart - 1);
        Dictionary<string, string> meta = ParseMetadataPairs(inner);

        // Replace metadata block (including parens) and the optional
        // 'differential' keyword with same-length whitespace. We MUST blank
        // out 'differential' as well because the lexer's archetype-HRID
        // mode (triggered by 'archetype' / 'template' / etc.) would eat
        // 'differential' as if it were the HRID literal.
        StringBuilder rewritten = new(source);
        if (isDifferential)
        {
            for (int i = differentialStart; i < differentialEnd; i++)
            {
                rewritten[i] = ' ';
            }
        }
        for (int i = parenStart; i <= parenEnd; i++)
        {
            char c = rewritten[i];
            if (c == '\r' || c == '\n')
            {
                continue;
            }
            rewritten[i] = ' ';
        }
        return (rewritten.ToString(), meta, parenStart, parenEnd, isDifferential);
    }

    private static Dictionary<string, string> ParseMetadataPairs(string inner)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        int i = 0;
        while (i < inner.Length)
        {
            while (i < inner.Length && (char.IsWhiteSpace(inner[i]) || inner[i] == ';'))
            {
                i++;
            }
            if (i >= inner.Length)
            {
                break;
            }
            int keyStart = i;
            while (i < inner.Length && IsIdentChar(inner[i]))
            {
                i++;
            }
            if (i == keyStart)
            {
                // Skip stray character
                i++;
                continue;
            }
            string key = inner.Substring(keyStart, i - keyStart);
            // optional '=' + value
            while (i < inner.Length && (inner[i] == ' ' || inner[i] == '\t'))
            {
                i++;
            }
            string value = string.Empty;
            if (i < inner.Length && inner[i] == '=')
            {
                i++;
                while (i < inner.Length && (inner[i] == ' ' || inner[i] == '\t'))
                {
                    i++;
                }
                int valStart = i;
                while (i < inner.Length && inner[i] != ';' && inner[i] != '\r' && inner[i] != '\n')
                {
                    i++;
                }
                value = inner.Substring(valStart, i - valStart).Trim();
            }
            result[key] = value;
        }
        return result;
    }

    private static bool IsIdentChar(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
        || (c >= '0' && c <= '9') || c == '_';

    private static bool IsIdentStart(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';

    private static bool IsIdentContinue(char c) => IsIdentChar(c);

    private static List<Adl2TokenInfo> Tokenize(ReadOnlySpan<char> source)
    {
        List<Adl2TokenInfo> tokens = [];
        Adl2Lexer lexer = new(source);
        while (true)
        {
            Adl2Token t = lexer.NextToken();
            tokens.Add(new Adl2TokenInfo
            {
                Kind = t.Kind,
                Text = t.Text,
                Value = t.Value,
                EmbeddedNodeId = t.EmbeddedNodeId,
                Line = t.Line,
                Column = t.Column,
                Start = t.Start,
                Length = t.Length,
            });
            if (t.Kind == Adl2TokenKind.Eof)
            {
                break;
            }
        }
        return tokens;
    }

    // ------------------------------------------------------------------
    // Parser state
    // ------------------------------------------------------------------

    private sealed class ParserState
    {
        public List<Adl2TokenInfo> Tokens { get; }
        public int Index;
        public string Source { get; }
        public Dictionary<string, string> Metadata { get; }
        public bool IsDifferentialHeader { get; }
        public Stack<string> PathStack { get; } = new();

        public ParserState(List<Adl2TokenInfo> tokens, string source, Dictionary<string, string> metadata, bool isDifferentialHeader)
        {
            Tokens = tokens;
            Source = source;
            Metadata = metadata;
            IsDifferentialHeader = isDifferentialHeader;
            Index = 0;
        }

        public Adl2TokenInfo Peek()
        {
            SkipNewlines();
            return Tokens[Index];
        }

        public Adl2TokenInfo PeekRaw() => Tokens[Index];

        public Adl2TokenInfo PeekAt(int offset)
        {
            // Skip newlines counting forward
            int idx = Index;
            int seen = 0;
            while (idx < Tokens.Count)
            {
                if (Tokens[idx].Kind == Adl2TokenKind.Newline)
                {
                    idx++;
                    continue;
                }
                if (seen == offset)
                {
                    return Tokens[idx];
                }
                seen++;
                idx++;
            }
            return Tokens[^1];
        }

        public Adl2TokenInfo Consume()
        {
            SkipNewlines();
            Adl2TokenInfo t = Tokens[Index];
            if (t.Kind != Adl2TokenKind.Eof)
            {
                Index++;
            }
            return t;
        }

        public void SkipNewlines()
        {
            while (Index < Tokens.Count && Tokens[Index].Kind == Adl2TokenKind.Newline)
            {
                Index++;
            }
        }

        public Adl2TokenInfo Expect(Adl2TokenKind kind, string what)
        {
            Adl2TokenInfo t = Peek();
            if (t.Kind != kind)
            {
                throw Error($"Expected {what} (got {t.Kind} '{t.Text}').", t);
            }
            return Consume();
        }

        public Adl2TokenInfo ExpectKeyword(string keyword)
        {
            Adl2TokenInfo t = Peek();
            if (t.Kind != Adl2TokenKind.Keyword || !string.Equals(t.Value, keyword, StringComparison.Ordinal))
            {
                throw Error($"Expected keyword '{keyword}' (got {t.Kind} '{t.Text}').", t);
            }
            return Consume();
        }

        public bool TryConsumeKeyword(string keyword)
        {
            Adl2TokenInfo t = Peek();
            if (t.Kind == Adl2TokenKind.Keyword && string.Equals(t.Value, keyword, StringComparison.Ordinal))
            {
                Consume();
                return true;
            }
            return false;
        }

        public Adl2ParseException Error(string message, Adl2TokenInfo? at = null)
        {
            Adl2TokenInfo loc = at ?? (Index < Tokens.Count ? Tokens[Index] : Tokens[^1]);
            string? path = PathStack.Count > 0 ? "/" + string.Join("/", PathStack.Reverse()) : null;
            return new Adl2ParseException(message, loc.Line, loc.Column, path);
        }
    }

    // ------------------------------------------------------------------
    // Top-level orchestration
    // ------------------------------------------------------------------

    private static Archetype ParseInternal(string source)
    {
        (string rewritten, Dictionary<string, string> metadata, int _, int _, bool isDifferential) = ExtractHeaderMetadata(source);
        // Second pre-pass: neutralise regex literals that begin with an
        // identifier character (the lexer mis-routes /openEHR-.../ to
        // ScanPathSegment instead of ScanRegex and then chokes on '\').
        // We also length-preserve so token offsets stay valid.
        rewritten = NeutraliseRegexLikePaths(rewritten);
        // Third pre-pass: wrap bare URIs inside ODIN <…> blocks with
        // double-quotes so the OdinLexer can tokenise them as strings.
        // This is NOT length-preserving; positions reported by errors
        // inside affected blocks may shift by a small amount.
        rewritten = WrapBareUrisInOdinBlocks(rewritten);

        List<Adl2TokenInfo> tokens;
        try
        {
            tokens = Tokenize(rewritten.AsSpan());
        }
        catch (Adl2LexException ex)
        {
            throw new Adl2ParseException(ex.Message, ex.Line, ex.Column);
        }

        ParserState state = new(tokens, rewritten, metadata, isDifferential);
        return ParseArchetype(state);
    }

    private static string NeutraliseRegexLikePaths(string source)
    {
        // Walk the source. When we see '/' followed by an identifier-start
        // char that begins a single-line stretch ending at the next '/'
        // AND that stretch contains a '\' (regex escape) or a bare '.',
        // it is almost certainly a regex literal that the lexer would
        // otherwise mis-tokenise. Replace the inner content with valid
        // identifier chars (length-preserving) so tokenisation succeeds.
        // The surrounding '/' chars stay; the original raw text is lost
        // from this slice. The /…/ block will end up as path-segment
        // tokens that the parser ignores when inside a c-primitive body.
        //
        // String-literal contents and line comments are skipped: a URL like
        // "http://www.example.org/foo" inside a quoted string must NOT be
        // mangled, otherwise the round-trip writer cannot reproduce it.
        StringBuilder sb = new(source);
        int i = 0;
        while (i < source.Length)
        {
            char c = source[i];
            if (c == '"')
            {
                // Skip a double-quoted string literal verbatim.
                i++;
                while (i < source.Length)
                {
                    char sc = source[i];
                    if (sc == '\\' && i + 1 < source.Length)
                    {
                        i += 2;
                        continue;
                    }
                    if (sc == '"')
                    {
                        i++;
                        break;
                    }
                    i++;
                }
                continue;
            }
            if (c == '-' && i + 1 < source.Length && source[i + 1] == '-')
            {
                // Skip an ADL line comment.
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                }
                continue;
            }
            if (c == '/' && i + 1 < source.Length && IsIdentStart(source[i + 1]))
            {
                int j = i + 1;
                bool hasEscape = false;
                bool hasDot = false;
                while (j < source.Length && source[j] != '\n' && source[j] != '/')
                {
                    char cj = source[j];
                    if (cj == '\\') hasEscape = true;
                    else if (cj == '.') hasDot = true;
                    j++;
                }
                if (j < source.Length && source[j] == '/' && (hasEscape || hasDot))
                {
                    for (int k = i + 1; k < j; k++)
                    {
                        char ck = source[k];
                        if (!IsIdentContinue(ck) && ck != '_')
                        {
                            sb[k] = '_';
                        }
                    }
                    i = j + 1;
                    continue;
                }
            }
            i++;
        }
        return sb.ToString();
    }

    private static string WrapBareUrisInOdinBlocks(string source)
    {
        // Scan source for '<' that opens an ODIN block whose content is a
        // bare URI like 'http://...' (no quotes, no nested brackets/
        // angles). Wrap the inner content with double-quotes so the
        // OdinLexer can lex it as a string literal. We track string-
        // literal context so that we never rewrite content inside an
        // existing quoted string.
        StringBuilder sb = new();
        int i = 0;
        bool inString = false;
        while (i < source.Length)
        {
            char c = source[i];
            if (inString)
            {
                sb.Append(c);
                if (c == '\\' && i + 1 < source.Length)
                {
                    sb.Append(source[i + 1]);
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
                sb.Append(c);
                i++;
                continue;
            }
            if (c == '<' && LooksLikeBareUriBlock(source, i, out int innerStart, out int innerEnd))
            {
                sb.Append('<');
                sb.Append('"');
                sb.Append(source, innerStart, innerEnd - innerStart);
                sb.Append('"');
                sb.Append('>');
                i = innerEnd + 1; // past closing '>'
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    private static bool LooksLikeBareUriBlock(string source, int openAngle, out int innerStart, out int innerEnd)
    {
        innerStart = innerEnd = -1;
        int p = openAngle + 1;
        // Skip whitespace
        while (p < source.Length && (source[p] == ' ' || source[p] == '\t'))
        {
            p++;
        }
        int start = p;
        // Must be an identifier-start (scheme letter)
        if (p >= source.Length || !IsIdentStart(source[p]))
        {
            return false;
        }
        // Scan letters/digits/+/-/. for the scheme
        while (p < source.Length && (IsIdentContinue(source[p]) || source[p] == '+' || source[p] == '-' || source[p] == '.'))
        {
            p++;
        }
        // Must be followed by '://'
        if (p + 2 >= source.Length || source[p] != ':' || source[p + 1] != '/' || source[p + 2] != '/')
        {
            return false;
        }
        // Now scan to the closing '>' on the same logical content (no nested
        // '<' or '>', no quote, no whitespace newline).
        while (p < source.Length)
        {
            char ch = source[p];
            if (ch == '>')
            {
                int end = p;
                // Trim trailing whitespace before '>'
                int trimmed = end;
                while (trimmed > start && (source[trimmed - 1] == ' ' || source[trimmed - 1] == '\t'))
                {
                    trimmed--;
                }
                innerStart = start;
                innerEnd = trimmed;
                // But the closing angle we report points at the actual '>'.
                // We rebuild the output as <"trimmed">. Caller uses end.
                // To make caller logic simple, set innerEnd to end and rely
                // on the trimmed slice instead.
                innerStart = start;
                innerEnd = end;
                // Position the caller's cursor past '>'.
                // The wrap fn uses innerEnd as the position of '>'.
                return true;
            }
            if (ch == '<' || ch == '"' || ch == '\n' || ch == '\r')
            {
                return false;
            }
            p++;
        }
        return false;
    }

    private static Archetype ParseArchetype(ParserState state)
    {
        // Header
        Adl2TokenInfo headerTok = state.Peek();
        if (headerTok.Kind != Adl2TokenKind.Keyword)
        {
            throw state.Error("Expected archetype header keyword (archetype/template/template_overlay/operational_template).", headerTok);
        }

        Archetype archetype = headerTok.Value switch
        {
            "archetype" => new AuthoredArchetype(),
            "template" => new Template { IsTemplate = true },
            "template_overlay" => new TemplateOverlay { IsTemplate = true },
            "operational_template" => CreateOperationalTemplate(),
            _ => throw state.Error($"Unexpected archetype header keyword '{headerTok.Value}'.", headerTok),
        };
        archetype.SourceLine = headerTok.Line;
        archetype.SourceColumn = headerTok.Column;
        state.Consume();

        if (state.TryConsumeKeyword("differential") || state.IsDifferentialHeader)
        {
            if (archetype is AuthoredArchetype authored)
            {
                authored.IsDifferential = true;
            }
            else
            {
                archetype.IsDifferential = true;
            }
        }

        archetype.HeaderMetadata = state.Metadata;

        // HRID literal
        Adl2TokenInfo hridTok = state.Peek();
        if (hridTok.Kind != Adl2TokenKind.ArchetypeHridLiteral)
        {
            throw state.Error("Expected archetype HRID literal after header.", hridTok);
        }
        state.Consume();
        if (!ArchetypeHRID.TryParse(hridTok.Value ?? hridTok.Text, out ArchetypeHRID? hrid))
        {
            throw state.Error($"Invalid archetype HRID '{hridTok.Text}'.", hridTok);
        }
        archetype.ArchetypeId = hrid;

        // Optional specialize clause
        if (state.Peek().Kind == Adl2TokenKind.Keyword
            && string.Equals(state.Peek().Value, "specialize", StringComparison.Ordinal))
        {
            state.Consume();
            Adl2TokenInfo parentTok = state.Peek();
            if (parentTok.Kind != Adl2TokenKind.ArchetypeHridLiteral)
            {
                throw state.Error("Expected archetype HRID literal after 'specialize'.", parentTok);
            }
            state.Consume();
            if (!ArchetypeHRID.TryParse(parentTok.Value ?? parentTok.Text, out ArchetypeHRID? parent))
            {
                throw state.Error($"Invalid parent archetype HRID '{parentTok.Text}'.", parentTok);
            }
            archetype.ParentArchetypeId = parent;
        }

        // Section dispatch
        while (state.Peek().Kind != Adl2TokenKind.Eof)
        {
            Adl2TokenInfo sec = state.Peek();
            if (sec.Kind != Adl2TokenKind.Keyword)
            {
                throw state.Error($"Expected section keyword (got {sec.Kind} '{sec.Text}').", sec);
            }
            switch (sec.Value)
            {
                case "language":
                    ParseLanguageSection(state, archetype);
                    break;
                case "description":
                    ParseDescriptionSection(state, archetype);
                    break;
                case "definition":
                    ParseDefinitionSection(state, archetype);
                    break;
                case "rules":
                    ParseRulesSection(state, archetype);
                    break;
                case "terminology":
                    ParseTerminologySection(state, archetype);
                    break;
                case "annotations":
                    ParseAnnotationsSection(state, archetype);
                    break;
                default:
                    throw state.Error($"Unexpected section keyword '{sec.Value}'.", sec);
            }
        }

        if (archetype.ArchetypeId is null)
        {
            throw state.Error("Archetype is missing its HRID.");
        }
        if (archetype.Definition is null || string.IsNullOrEmpty(archetype.Definition.RmTypeName))
        {
            throw state.Error("Archetype is missing its 'definition' section.");
        }
        return archetype;
    }

    private static OperationalTemplate CreateOperationalTemplate() => new();

    // ------------------------------------------------------------------
    // ODIN-bearing sections
    // ------------------------------------------------------------------

    private static List<(string Key, OdinValue Value, Adl2TokenInfo NameTok)> ConsumeOdinPairs(ParserState state)
    {
        List<(string, OdinValue, Adl2TokenInfo)> pairs = [];
        while (true)
        {
            Adl2TokenInfo t = state.Peek();
            if (t.Kind != Adl2TokenKind.Identifier)
            {
                break;
            }
            // Look ahead for '='
            Adl2TokenInfo next = state.PeekAt(1);
            if (next.Kind != Adl2TokenKind.Equals)
            {
                break;
            }
            Adl2TokenInfo nameTok = state.Consume();
            state.Consume(); // '='
            Adl2TokenInfo blockTok = state.Peek();
            if (blockTok.Kind != Adl2TokenKind.OdinBlock)
            {
                throw state.Error($"Expected ODIN <…> block after '{nameTok.Text} ='.", blockTok);
            }
            state.Consume();
            // OdinParser expects the full '<…>' span. We stored Start/Length
            // on the token info; slice it out of the source.
            string blockText = state.Source.Substring(blockTok.Start, blockTok.Length);
            OdinValue val;
            try
            {
                val = OdinParser.Parse(blockText);
            }
            catch (OdinParseException ex)
            {
                int line = blockTok.Line + Math.Max(0, ex.Line - 1);
                int col = ex.Line == 1 ? blockTok.Column + ex.Column - 1 : ex.Column;
                throw new Adl2ParseException(
                    $"ODIN block parse error in '{nameTok.Text}': {ex.Message}",
                    line, col, path: nameTok.Text, ex);
            }
            pairs.Add((nameTok.Text, val, nameTok));
        }
        return pairs;
    }

    private static void ParseLanguageSection(ParserState state, Archetype archetype)
    {
        state.ExpectKeyword("language");
        List<(string Key, OdinValue Value, Adl2TokenInfo NameTok)> pairs = ConsumeOdinPairs(state);

        foreach ((string key, OdinValue val, Adl2TokenInfo _) in pairs)
        {
            switch (key)
            {
                case "original_language":
                    archetype.OriginalLanguage = ExtractLanguageCode(val);
                    archetype.Terminology.OriginalLanguage = archetype.OriginalLanguage;
                    break;
                case "translations":
                    archetype.Translations = ExtractTranslations(val);
                    break;
                default:
                    // Unknown keys silently ignored for forward-compat
                    break;
            }
        }
    }

    private static string ExtractLanguageCode(OdinValue val)
    {
        if (val is OdinTerminologyCode tc)
        {
            return tc.Value.CodeString;
        }
        if (val is OdinString s)
        {
            return s.Value;
        }
        return val.ToString();
    }

    private static Dictionary<string, TranslationDetails> ExtractTranslations(OdinValue val)
    {
        Dictionary<string, TranslationDetails> result = new(StringComparer.Ordinal);
        if (val is OdinHash hash)
        {
            foreach (KeyValuePair<string, OdinValue> entry in hash.Entries)
            {
                if (entry.Value is OdinObject obj)
                {
                    TranslationDetails td = new();
                    if (obj.TryGet("language", out OdinValue? lang) && lang is not null)
                    {
                        td.Language = ExtractLanguageCode(lang);
                    }
                    if (obj.TryGet("author", out OdinValue? author) && author is OdinHash authorHash)
                    {
                        td.Author = HashToStringMap(authorHash);
                    }
                    if (obj.TryGet("accreditation", out OdinValue? accred))
                    {
                        if (accred is OdinString s)
                        {
                            td.Accreditation = [s.Value];
                        }
                        else if (accred is OdinList list)
                        {
                            td.Accreditation = [.. list.Items.OfType<OdinString>().Select(x => x.Value)];
                        }
                    }
                    if (obj.TryGet("other_details", out OdinValue? other) && other is OdinHash otherHash)
                    {
                        td.OtherDetails = HashToStringMap(otherHash);
                    }
                    if (obj.TryGet("version_last_translated", out OdinValue? ver) && ver is OdinString vstr)
                    {
                        td.VersionLastTranslated = vstr.Value;
                    }
                    result[entry.Key] = td;
                }
            }
        }
        return result;
    }

    private static Dictionary<string, string> HashToStringMap(OdinHash hash)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, OdinValue> entry in hash.Entries)
        {
            map[entry.Key] = OdinValueToText(entry.Value);
        }
        return map;
    }

    private static string OdinValueToText(OdinValue v)
        => v switch
        {
            OdinString s => s.Value,
            OdinInteger i => i.Value.ToString(CultureInfo.InvariantCulture),
            OdinReal r => r.Value.ToString(CultureInfo.InvariantCulture),
            OdinBoolean b => b.Value ? "true" : "false",
            _ => v.ToString(),
        };

    private static void ParseDescriptionSection(ParserState state, Archetype archetype)
    {
        state.ExpectKeyword("description");
        List<(string Key, OdinValue Value, Adl2TokenInfo NameTok)> pairs = ConsumeOdinPairs(state);

        ResourceDescription desc = archetype.Description;
        foreach ((string key, OdinValue val, Adl2TokenInfo _) in pairs)
        {
            switch (key)
            {
                case "lifecycle_state":
                    desc.LifecycleState = OdinValueToText(val);
                    break;
                case "original_author":
                    if (val is OdinHash oa) desc.OriginalAuthor = HashToStringMap(oa);
                    break;
                case "copyright":
                    desc.Copyright = OdinValueToText(val);
                    break;
                case "details":
                    if (val is OdinHash dh) desc.Details = ExtractDescriptionDetails(dh);
                    break;
                case "other_details":
                    if (val is OdinHash od) desc.OtherDetails = HashToStringMap(od);
                    break;
                case "other_contributors":
                    if (val is OdinList lc)
                    {
                        desc.OtherContributors = [.. lc.Items.Select(OdinValueToText)];
                    }
                    break;
                case "licence":
                    if (val is OdinHash lh) desc.Licence = HashToStringMap(lh);
                    else if (val is OdinString ls) desc.Licence = new Dictionary<string, string>(StringComparer.Ordinal) { ["en"] = ls.Value };
                    break;
                case "resource_package_uri":
                    desc.ResourcePackageUri = OdinValueToText(val);
                    break;
                case "ip_acknowledgements":
                    if (val is OdinHash ipa) desc.IpAcknowledgements = [.. ipa.Entries.Values.Select(OdinValueToText)];
                    else if (val is OdinList ipl) desc.IpAcknowledgements = [.. ipl.Items.Select(OdinValueToText)];
                    break;
                case "references":
                    if (val is OdinHash refh) desc.References = [.. refh.Entries.Values.Select(OdinValueToText)];
                    else if (val is OdinList refl) desc.References = [.. refl.Items.Select(OdinValueToText)];
                    break;
                case "conformance":
                case "conforms_to":
                    if (val is OdinList cf) desc.ConformsTo = [.. cf.Items.Select(OdinValueToText)];
                    break;
                default:
                    // forward-compat: ignore
                    break;
            }
        }
    }

    private static Dictionary<string, ResourceDescriptionItem> ExtractDescriptionDetails(OdinHash hash)
    {
        Dictionary<string, ResourceDescriptionItem> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, OdinValue> entry in hash.Entries)
        {
            if (entry.Value is not OdinObject obj)
            {
                continue;
            }
            ResourceDescriptionItem item = new() { Language = entry.Key };
            if (obj.TryGet("language", out OdinValue? lang) && lang is not null)
            {
                item.Language = ExtractLanguageCode(lang);
            }
            if (obj.TryGet("purpose", out OdinValue? purpose) && purpose is OdinString p)
            {
                item.Purpose = p.Value;
            }
            if (obj.TryGet("use", out OdinValue? use) && use is OdinString u)
            {
                item.Use = u.Value;
            }
            if (obj.TryGet("misuse", out OdinValue? misuse) && misuse is OdinString m)
            {
                item.Misuse = m.Value;
            }
            if (obj.TryGet("keywords", out OdinValue? kw) && kw is OdinList kl)
            {
                item.Keywords = [.. kl.Items.OfType<OdinString>().Select(x => x.Value)];
            }
            if (obj.TryGet("copyright", out OdinValue? copy) && copy is OdinString c)
            {
                item.Copyright = c.Value;
            }
            if (obj.TryGet("other_details", out OdinValue? other) && other is OdinHash otherHash)
            {
                item.OtherDetails = HashToStringMap(otherHash);
            }
            if (obj.TryGet("original_resource_uri", out OdinValue? uri) && uri is OdinHash uh)
            {
                item.OriginalResourceUri = HashToStringMap(uh);
            }
            result[entry.Key] = item;
        }
        return result;
    }

    private static void ParseTerminologySection(ParserState state, Archetype archetype)
    {
        state.ExpectKeyword("terminology");
        List<(string Key, OdinValue Value, Adl2TokenInfo NameTok)> pairs = ConsumeOdinPairs(state);

        ArchetypeTerminology term = archetype.Terminology;
        foreach ((string key, OdinValue val, Adl2TokenInfo nameTok) in pairs)
        {
            switch (key)
            {
                case "term_definitions":
                    if (val is OdinHash td) term.TermDefinitions = ExtractTermsByLang(td);
                    break;
                case "constraint_definitions":
                    if (val is OdinHash cd) term.ConstraintDefinitions = ExtractTermsByLang(cd);
                    break;
                case "value_sets":
                    if (val is OdinHash vs) term.ValueSets = ExtractValueSets(vs);
                    break;
                case "term_bindings":
                    if (val is OdinHash tb) term.TermBindings = ExtractBindings(tb);
                    break;
                case "constraint_bindings":
                    if (val is OdinHash cb) term.ConstraintBindings = ExtractBindings(cb);
                    break;
                default:
                    // ignore unknown
                    break;
            }
            _ = nameTok;
        }
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
                    if (mv is OdinList ml) vs.Members = [.. ml.Items.OfType<OdinString>().Select(x => x.Value)];
                    else if (mv is OdinString ms) vs.Members = [ms.Value];
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

    private static void ParseAnnotationsSection(ParserState state, Archetype archetype)
    {
        state.ExpectKeyword("annotations");
        List<(string Key, OdinValue Value, Adl2TokenInfo NameTok)> pairs = ConsumeOdinPairs(state);
        ResourceAnnotations annotations = new();
        foreach ((string _, OdinValue _, Adl2TokenInfo _) in pairs)
        {
            // We accept any structure but don't fully map it; the
            // structured shape is deferred.
        }
        // Place raw container even if empty for forward-compat.
        if (pairs.Count > 0)
        {
            archetype.Annotations = annotations;
        }
    }

    // ------------------------------------------------------------------
    // Rules section (capture raw text)
    // ------------------------------------------------------------------

    private static readonly HashSet<string> s_sectionKeywords = new(StringComparer.Ordinal)
    {
        "archetype", "template", "template_overlay", "operational_template",
        "specialize", "language", "description", "definition", "rules",
        "terminology", "annotations",
    };

    private static void ParseRulesSection(ParserState state, Archetype archetype)
    {
        Adl2TokenInfo kw = state.ExpectKeyword("rules");
        // Capture raw text from after 'rules' to the next section keyword (or EOF).
        int startPos = kw.Start + kw.Length;
        // Skip leading whitespace
        while (startPos < state.Source.Length && (state.Source[startPos] == ' ' || state.Source[startPos] == '\t'))
        {
            startPos++;
        }

        int endPos = state.Source.Length;
        int scanIndex = state.Index;
        while (scanIndex < state.Tokens.Count)
        {
            Adl2TokenInfo t = state.Tokens[scanIndex];
            if (t.Kind == Adl2TokenKind.Eof)
            {
                endPos = t.Start;
                break;
            }
            if (t.Kind == Adl2TokenKind.Keyword && t.Value is not null && s_sectionKeywords.Contains(t.Value))
            {
                // Heuristic: top-level section keywords appear at column 1.
                if (t.Column == 1)
                {
                    endPos = t.Start;
                    state.Index = scanIndex;
                    break;
                }
            }
            scanIndex++;
        }
        // If we exited without finding a section keyword, consume all
        if (scanIndex >= state.Tokens.Count)
        {
            endPos = state.Source.Length;
            state.Index = state.Tokens.Count - 1;
        }
        else if (state.Tokens[scanIndex].Kind == Adl2TokenKind.Eof)
        {
            state.Index = scanIndex;
        }

        string raw = state.Source.Substring(startPos, Math.Max(0, endPos - startPos)).TrimEnd();
        archetype.Rules = new RulesSection
        {
            RawText = raw,
            SourceLine = kw.Line,
            SourceColumn = kw.Column,
        };
    }

    // ------------------------------------------------------------------
    // cADL: definition section
    // ------------------------------------------------------------------

    private static void ParseDefinitionSection(ParserState state, Archetype archetype)
    {
        state.ExpectKeyword("definition");
        CComplexObject root = ParseCComplexObject(state);
        archetype.Definition = root;
    }

    private static CComplexObject ParseCComplexObject(ParserState state)
    {
        Adl2TokenInfo nameTok = state.Peek();
        if (nameTok.Kind != Adl2TokenKind.Identifier)
        {
            throw state.Error($"Expected RM type name (got {nameTok.Kind} '{nameTok.Text}').", nameTok);
        }
        state.Consume();

        CComplexObject node = new()
        {
            RmTypeName = nameTok.Text,
            SourceLine = nameTok.Line,
            SourceColumn = nameTok.Column,
        };

        // Optional [node_id]
        Adl2TokenInfo idTok = state.Peek();
        if (idTok.Kind == Adl2TokenKind.IdCode || idTok.Kind == Adl2TokenKind.AtCode)
        {
            node.NodeId = idTok.Value ?? idTok.Text;
            state.Consume();
        }

        // Optional 'occurrences matches {interval}'
        TryParseOccurrences(state, node);

        // Body: either 'matches { ... }' or implicit (some constraint-only forms)
        if (state.Peek().Kind == Adl2TokenKind.Keyword
            && string.Equals(state.Peek().Value, "matches", StringComparison.Ordinal))
        {
            state.Consume();
            ParseAttributeBlock(state, node);
        }
        else if (state.Peek().Kind == Adl2TokenKind.LBrace)
        {
            ParseAttributeBlock(state, node);
        }
        // else: bare RM_TYPE_NAME [id] — that's a fully open constraint (no body),
        // valid in e.g. `DV_TEXT[id30]` lines.

        return node;
    }

    private static void TryParseOccurrences(ParserState state, CObject node)
    {
        if (state.Peek().Kind == Adl2TokenKind.Keyword
            && string.Equals(state.Peek().Value, "occurrences", StringComparison.Ordinal))
        {
            state.Consume();
            state.ExpectKeyword("matches");
            state.Expect(Adl2TokenKind.LBrace, "'{'");
            node.Occurrences = ParseIntInterval(state);
            state.Expect(Adl2TokenKind.RBrace, "'}'");
        }
    }

    private static void ParseAttributeBlock(ParserState state, CComplexObject node)
    {
        state.Expect(Adl2TokenKind.LBrace, "'{'");
        while (true)
        {
            Adl2TokenInfo t = state.Peek();
            if (t.Kind == Adl2TokenKind.RBrace)
            {
                state.Consume();
                return;
            }
            if (t.Kind == Adl2TokenKind.Eof)
            {
                throw state.Error("Unterminated complex object body (expected '}').", t);
            }
            // Skip stray commas / semicolons between attributes
            if (t.Kind == Adl2TokenKind.Comma || t.Kind == Adl2TokenKind.Semicolon)
            {
                state.Consume();
                continue;
            }

            // Tuple constraint: '[attr1, attr2, ...] matches {...}'
            if (t.Kind == Adl2TokenKind.LBracket)
            {
                CAttributeTuple tuple = ParseAttributeTuple(state);
                node.AttributeTuples.Add(tuple);
                continue;
            }

            if (t.Kind != Adl2TokenKind.Identifier)
            {
                throw state.Error($"Expected attribute name (got {t.Kind} '{t.Text}').", t);
            }
            CAttribute attr = ParseCAttribute(state);
            node.Attributes.Add(attr);
        }
    }

    private static CAttribute ParseCAttribute(ParserState state)
    {
        Adl2TokenInfo nameTok = state.Consume();
        string attrName = nameTok.Text;
        state.PathStack.Push(attrName);

        Interval<int>? existence = null;
        Cardinality? cardinality = null;

        // Optional 'existence matches {interval}'
        if (state.Peek().Kind == Adl2TokenKind.Keyword
            && string.Equals(state.Peek().Value, "existence", StringComparison.Ordinal))
        {
            state.Consume();
            state.ExpectKeyword("matches");
            state.Expect(Adl2TokenKind.LBrace, "'{'");
            existence = ParseIntInterval(state);
            state.Expect(Adl2TokenKind.RBrace, "'}'");
        }

        // Optional 'cardinality matches {interval; ordered; unique}'
        if (state.Peek().Kind == Adl2TokenKind.Keyword
            && string.Equals(state.Peek().Value, "cardinality", StringComparison.Ordinal))
        {
            state.Consume();
            state.ExpectKeyword("matches");
            state.Expect(Adl2TokenKind.LBrace, "'{'");
            Interval<int> interval = ParseIntInterval(state);
            bool isOrdered = false;
            bool isUnique = false;
            while (true)
            {
                Adl2TokenInfo t = state.Peek();
                if (t.Kind == Adl2TokenKind.RBrace)
                {
                    break;
                }
                if (t.Kind == Adl2TokenKind.Semicolon || t.Kind == Adl2TokenKind.Comma)
                {
                    state.Consume();
                    continue;
                }
                if (t.Kind == Adl2TokenKind.Keyword)
                {
                    switch (t.Value)
                    {
                        case "ordered": isOrdered = true; state.Consume(); continue;
                        case "unordered": isOrdered = false; state.Consume(); continue;
                        case "unique": isUnique = true; state.Consume(); continue;
                    }
                }
                throw state.Error($"Unexpected token in cardinality body: {t.Kind} '{t.Text}'.", t);
            }
            state.Expect(Adl2TokenKind.RBrace, "'}'");
            cardinality = new Cardinality(interval, isOrdered, isUnique);
        }

        // 'matches { object_list }' — required (we permit bare-name attributes
        // with empty body when nothing follows, but that's not valid ADL).
        CAttribute attr = cardinality is not null
            ? new CMultipleAttribute { Cardinality = cardinality }
            : (CAttribute)new CSingleAttribute();
        attr.RmAttributeName = attrName;
        attr.Existence = existence;
        attr.SourceLine = nameTok.Line;
        attr.SourceColumn = nameTok.Column;

        Adl2TokenInfo afterModifiers = state.Peek();
        if (afterModifiers.Kind == Adl2TokenKind.Keyword
            && string.Equals(afterModifiers.Value, "matches", StringComparison.Ordinal))
        {
            state.Consume();
            ParseObjectList(state, attr);
        }
        else if (afterModifiers.Kind == Adl2TokenKind.LBrace)
        {
            // Implicit matches (some ADL variants)
            ParseObjectList(state, attr);
        }
        // else: bare attribute name (e.g. 'data' with no body) — valid as fully open.

        state.PathStack.Pop();
        return attr;
    }

    private static CAttributeTuple ParseAttributeTuple(ParserState state)
    {
        Adl2TokenInfo openTok = state.Expect(Adl2TokenKind.LBracket, "'['");
        CAttributeTuple tuple = new()
        {
            SourceLine = openTok.Line,
            SourceColumn = openTok.Column,
        };
        // Member attribute names
        while (true)
        {
            Adl2TokenInfo t = state.Peek();
            if (t.Kind == Adl2TokenKind.RBracket)
            {
                state.Consume();
                break;
            }
            if (t.Kind == Adl2TokenKind.Comma || t.Kind == Adl2TokenKind.Semicolon)
            {
                state.Consume();
                continue;
            }
            if (t.Kind != Adl2TokenKind.Identifier)
            {
                throw state.Error($"Expected attribute name in tuple (got {t.Kind} '{t.Text}').", t);
            }
            state.Consume();
            tuple.Members.Add(new CSingleAttribute
            {
                RmAttributeName = t.Text,
                SourceLine = t.Line,
                SourceColumn = t.Column,
            });
        }

        state.ExpectKeyword("matches");
        state.Expect(Adl2TokenKind.LBrace, "'{'");
        while (true)
        {
            Adl2TokenInfo t = state.Peek();
            if (t.Kind == Adl2TokenKind.RBrace)
            {
                state.Consume();
                break;
            }
            if (t.Kind == Adl2TokenKind.Comma || t.Kind == Adl2TokenKind.Semicolon)
            {
                state.Consume();
                continue;
            }
            // Each row is [ value1, value2, ... ]
            Adl2TokenInfo rowOpen = state.Expect(Adl2TokenKind.LBracket, "'['");
            CObjectTuple row = new()
            {
                SourceLine = rowOpen.Line,
                SourceColumn = rowOpen.Column,
            };
            while (true)
            {
                Adl2TokenInfo rt = state.Peek();
                if (rt.Kind == Adl2TokenKind.RBracket)
                {
                    state.Consume();
                    break;
                }
                if (rt.Kind == Adl2TokenKind.Comma || rt.Kind == Adl2TokenKind.Semicolon)
                {
                    state.Consume();
                    continue;
                }
                // Each member is a {...} primitive constraint block
                CObject member = ParseTupleMember(state);
                row.Members.Add(member);
            }
            tuple.Children.Add(row);
        }
        return tuple;
    }

    private static CObject ParseTupleMember(ParserState state)
    {
        Adl2TokenInfo openTok = state.Expect(Adl2TokenKind.LBrace, "'{'");
        // The body is one primitive constraint: an interval or a string list etc.
        // We delegate to ParsePrimitiveBody and wrap.
        CObject obj = ParsePrimitiveBody(state, openTok);
        state.Expect(Adl2TokenKind.RBrace, "'}'");
        return obj;
    }

    private static void ParseObjectList(ParserState state, CAttribute attr)
    {
        state.Expect(Adl2TokenKind.LBrace, "'{'");
        while (true)
        {
            Adl2TokenInfo t = state.Peek();
            if (t.Kind == Adl2TokenKind.RBrace)
            {
                state.Consume();
                return;
            }
            if (t.Kind == Adl2TokenKind.Eof)
            {
                throw state.Error("Unterminated object list (expected '}').", t);
            }
            if (t.Kind == Adl2TokenKind.Comma || t.Kind == Adl2TokenKind.Semicolon)
            {
                state.Consume();
                continue;
            }
            CObject child = ParseCObject(state);
            attr.Children.Add(child);
        }
    }

    private static CObject ParseCObject(ParserState state)
    {
        Adl2TokenInfo t = state.Peek();

        // Reference object dispatch by keyword
        if (t.Kind == Adl2TokenKind.Keyword)
        {
            switch (t.Value)
            {
                case "use_node":
                    return ParseArchetypeInternalRef(state);
                case "use_archetype":
                    return ParseComplexObjectProxy(state);
                case "allow_archetype":
                    return ParseArchetypeSlot(state);
            }
        }

        // Identifier: could be RM type name (CComplexObject / CArchetypeRoot)
        // or a primitive marker (boolean true/false in some dialects).
        if (t.Kind == Adl2TokenKind.Identifier)
        {
            // Boolean literals
            if (t.Text == "true" || t.Text == "false")
            {
                return ParseCBoolean(state);
            }
            return ParseComplexOrRoot(state);
        }

        // Open primitive forms.
        if (t.Kind == Adl2TokenKind.StringLiteral || t.Kind == Adl2TokenKind.RegexLiteral)
        {
            return ParseCString(state);
        }
        if (t.Kind == Adl2TokenKind.IntegerLiteral)
        {
            return ParseCInteger(state);
        }
        if (t.Kind == Adl2TokenKind.RealLiteral)
        {
            return ParseCReal(state);
        }
        if (t.Kind == Adl2TokenKind.IntervalDelim)
        {
            return ParseIntervalPrimitive(state);
        }
        if (t.Kind == Adl2TokenKind.AtCode || t.Kind == Adl2TokenKind.AcCode || t.Kind == Adl2TokenKind.LBracket)
        {
            return ParseCTerminologyCode(state);
        }
        if (t.Kind == Adl2TokenKind.Star)
        {
            // unconstrained primitive
            state.Consume();
            return new CString
            {
                RmTypeName = "ANY",
                SourceLine = t.Line,
                SourceColumn = t.Column,
            };
        }

        throw state.Error($"Unexpected token in object list: {t.Kind} '{t.Text}'.", t);
    }

    private static CObject ParseComplexOrRoot(ParserState state)
    {
        Adl2TokenInfo nameTok = state.Peek();
        // Look ahead: RM_TYPE_NAME [ac…] or RM_TYPE_NAME [openEHR-…]
        // → CArchetypeRoot
        Adl2TokenInfo next = state.PeekAt(1);
        if (next.Kind == Adl2TokenKind.AcCode)
        {
            // Could be CArchetypeRoot
            state.Consume();
            state.Consume();
            CArchetypeRoot root = new()
            {
                RmTypeName = nameTok.Text,
                NodeId = next.Value ?? next.Text,
                ArchetypeRef = next.Value ?? next.Text,
                SourceLine = nameTok.Line,
                SourceColumn = nameTok.Column,
            };
            TryParseOccurrences(state, root);
            if (state.Peek().Kind == Adl2TokenKind.Keyword
                && string.Equals(state.Peek().Value, "matches", StringComparison.Ordinal))
            {
                state.Consume();
                ParseAttributeBlock(state, root);
            }
            else if (state.Peek().Kind == Adl2TokenKind.LBrace)
            {
                ParseAttributeBlock(state, root);
            }
            return root;
        }
        return ParseCComplexObject(state);
    }

    private static ArchetypeInternalRef ParseArchetypeInternalRef(ParserState state)
    {
        Adl2TokenInfo kw = state.ExpectKeyword("use_node");
        Adl2TokenInfo rmTok = state.Expect(Adl2TokenKind.Identifier, "RM type name");
        ArchetypeInternalRef node = new()
        {
            RmTypeName = rmTok.Text,
            SourceLine = kw.Line,
            SourceColumn = kw.Column,
        };
        if (state.Peek().Kind == Adl2TokenKind.IdCode || state.Peek().Kind == Adl2TokenKind.AtCode)
        {
            Adl2TokenInfo idTok = state.Consume();
            node.NodeId = idTok.Value ?? idTok.Text;
        }
        TryParseOccurrences(state, node);
        // Target path: a series of PathSegment tokens. Use the raw source
        // slice so the leading '/' and any '[idN]' predicate are preserved
        // (Token.Text returns only the bare segment name via the Value
        // field, which would yield "dataeventsdata" for "/data[id2]/events[id7]/data[id4]").
        StringBuilder pathSb = new();
        while (state.PeekRaw().Kind == Adl2TokenKind.PathSegment)
        {
            Adl2TokenInfo seg = state.Tokens[state.Index];
            state.Index++;
            pathSb.Append(state.Source.AsSpan(seg.Start, seg.Length));
        }
        node.TargetPath = pathSb.ToString();
        return node;
    }

    private static CComplexObjectProxy ParseComplexObjectProxy(ParserState state)
    {
        Adl2TokenInfo kw = state.ExpectKeyword("use_archetype");
        Adl2TokenInfo rmTok = state.Expect(Adl2TokenKind.Identifier, "RM type name");
        CComplexObjectProxy node = new()
        {
            RmTypeName = rmTok.Text,
            SourceLine = kw.Line,
            SourceColumn = kw.Column,
        };
        if (state.Peek().Kind == Adl2TokenKind.IdCode || state.Peek().Kind == Adl2TokenKind.AtCode)
        {
            Adl2TokenInfo idTok = state.Consume();
            node.NodeId = idTok.Value ?? idTok.Text;
        }
        TryParseOccurrences(state, node);
        StringBuilder pathSb = new();
        while (state.PeekRaw().Kind == Adl2TokenKind.PathSegment)
        {
            Adl2TokenInfo seg = state.Tokens[state.Index];
            state.Index++;
            pathSb.Append(state.Source.AsSpan(seg.Start, seg.Length));
        }
        node.TargetPath = pathSb.ToString();
        return node;
    }

    private static ArchetypeSlot ParseArchetypeSlot(ParserState state)
    {
        Adl2TokenInfo kw = state.ExpectKeyword("allow_archetype");
        Adl2TokenInfo rmTok = state.Expect(Adl2TokenKind.Identifier, "RM type name");
        ArchetypeSlot slot = new()
        {
            RmTypeName = rmTok.Text,
            SourceLine = kw.Line,
            SourceColumn = kw.Column,
        };
        if (state.Peek().Kind == Adl2TokenKind.IdCode || state.Peek().Kind == Adl2TokenKind.AtCode)
        {
            Adl2TokenInfo idTok = state.Consume();
            slot.NodeId = idTok.Value ?? idTok.Text;
        }
        TryParseOccurrences(state, slot);
        // 'matches { include { ... } exclude { ... } }' (optional)
        if (state.Peek().Kind == Adl2TokenKind.Keyword
            && string.Equals(state.Peek().Value, "matches", StringComparison.Ordinal))
        {
            state.Consume();
            state.Expect(Adl2TokenKind.LBrace, "'{'");
            ParseSlotBody(state, slot);
            state.Expect(Adl2TokenKind.RBrace, "'}'");
        }
        else if (state.Peek().Kind == Adl2TokenKind.LBrace)
        {
            state.Consume();
            ParseSlotBody(state, slot);
            state.Expect(Adl2TokenKind.RBrace, "'}'");
        }
        else
        {
            // ADL2-style slot with bare include/exclude clauses (no braces)
            ParseSlotBareClauses(state, slot);
        }
        return slot;
    }

    private static void ParseSlotBody(ParserState state, ArchetypeSlot slot)
    {
        while (true)
        {
            Adl2TokenInfo t = state.Peek();
            if (t.Kind == Adl2TokenKind.RBrace || t.Kind == Adl2TokenKind.Eof)
            {
                return;
            }
            if (t.Kind == Adl2TokenKind.Keyword && t.Value == "include")
            {
                state.Consume();
                Assertion a = ReadAssertion(state);
                slot.Includes.Add(a);
                continue;
            }
            if (t.Kind == Adl2TokenKind.Keyword && t.Value == "exclude")
            {
                state.Consume();
                Assertion a = ReadAssertion(state);
                slot.Excludes.Add(a);
                continue;
            }
            // Unknown — skip to avoid infinite loop
            state.Consume();
        }
    }

    private static void ParseSlotBareClauses(ParserState state, ArchetypeSlot slot)
    {
        // Used when the slot is written as `allow_archetype ... \n include ... \n exclude ...`
        // We greedily consume include/exclude blocks until we see a token that doesn't fit.
        while (true)
        {
            Adl2TokenInfo t = state.Peek();
            if (t.Kind == Adl2TokenKind.Keyword && t.Value == "include")
            {
                state.Consume();
                slot.Includes.Add(ReadAssertion(state));
                continue;
            }
            if (t.Kind == Adl2TokenKind.Keyword && t.Value == "exclude")
            {
                state.Consume();
                slot.Excludes.Add(ReadAssertion(state));
                continue;
            }
            break;
        }
    }

    private static Assertion ReadAssertion(ParserState state)
    {
        // Capture raw source text from current token up to the next
        // top-level keyword (include/exclude/section keyword) or '}'.
        int startIdx = state.Index;
        // skip leading newlines
        while (startIdx < state.Tokens.Count && state.Tokens[startIdx].Kind == Adl2TokenKind.Newline)
        {
            startIdx++;
        }
        if (startIdx >= state.Tokens.Count)
        {
            return new Assertion();
        }
        int startPos = state.Tokens[startIdx].Start;
        int braceDepth = 0;
        int bracketDepth = 0;
        int endIdx = startIdx;
        for (int i = startIdx; i < state.Tokens.Count; i++)
        {
            Adl2TokenInfo t = state.Tokens[i];
            if (t.Kind == Adl2TokenKind.Eof)
            {
                endIdx = i;
                break;
            }
            if (braceDepth == 0 && bracketDepth == 0)
            {
                if (t.Kind == Adl2TokenKind.Keyword
                    && (t.Value == "include" || t.Value == "exclude"
                        || s_sectionKeywords.Contains(t.Value ?? string.Empty)))
                {
                    endIdx = i;
                    break;
                }
                if (t.Kind == Adl2TokenKind.RBrace)
                {
                    endIdx = i;
                    break;
                }
            }
            if (t.Kind == Adl2TokenKind.LBrace) braceDepth++;
            else if (t.Kind == Adl2TokenKind.RBrace) braceDepth--;
            else if (t.Kind == Adl2TokenKind.LBracket) bracketDepth++;
            else if (t.Kind == Adl2TokenKind.RBracket) bracketDepth--;
            endIdx = i + 1;
        }
        int endPos;
        if (endIdx < state.Tokens.Count && state.Tokens[endIdx].Kind != Adl2TokenKind.Eof)
        {
            endPos = state.Tokens[endIdx].Start;
        }
        else
        {
            endPos = state.Source.Length;
        }
        state.Index = endIdx;
        string raw = state.Source.Substring(startPos, endPos - startPos).Trim();
        return new Assertion { RawText = raw };
    }

    // ------------------------------------------------------------------
    // c_primitive_object parsing
    // ------------------------------------------------------------------

    private static CObject ParsePrimitiveBody(ParserState state, Adl2TokenInfo openTok)
    {
        // Called inside a '{ ... }' that constrains a primitive value.
        Adl2TokenInfo t = state.Peek();
        if (t.Kind == Adl2TokenKind.IntervalDelim)
        {
            // |min..max| — could be int or real
            return ParseIntervalPrimitive(state, openTok);
        }
        if (t.Kind == Adl2TokenKind.StringLiteral)
        {
            return ParseCStringInline(state, openTok);
        }
        if (t.Kind == Adl2TokenKind.RegexLiteral)
        {
            return ParseCStringInline(state, openTok);
        }
        if (t.Kind == Adl2TokenKind.IntegerLiteral)
        {
            return ParseCIntegerInline(state, openTok);
        }
        if (t.Kind == Adl2TokenKind.RealLiteral)
        {
            return ParseCRealInline(state, openTok);
        }
        // Empty body
        return new CString { RmTypeName = "ANY", SourceLine = openTok.Line, SourceColumn = openTok.Column };
    }

    private static CString ParseCString(ParserState state)
    {
        Adl2TokenInfo first = state.Peek();
        return ParseCStringInline(state, first);
    }

    private static CString ParseCStringInline(ParserState state, Adl2TokenInfo loc)
    {
        CString cs = new()
        {
            SourceLine = loc.Line,
            SourceColumn = loc.Column,
            RmTypeName = "String",
        };
        Adl2TokenInfo first = state.Peek();
        if (first.Kind == Adl2TokenKind.RegexLiteral)
        {
            cs.Pattern = first.Value ?? first.Text;
            state.Consume();
            return cs;
        }
        List<string> values = [];
        while (true)
        {
            Adl2TokenInfo t = state.Peek();
            if (t.Kind == Adl2TokenKind.StringLiteral)
            {
                values.Add(t.Value ?? t.Text);
                state.Consume();
            }
            else if (t.Kind == Adl2TokenKind.Comma)
            {
                state.Consume();
            }
            else
            {
                break;
            }
        }
        cs.EnumeratedValues = values;
        return cs;
    }

    private static CInteger ParseCInteger(ParserState state)
    {
        Adl2TokenInfo first = state.Peek();
        return ParseCIntegerInline(state, first);
    }

    private static CInteger ParseCIntegerInline(ParserState state, Adl2TokenInfo loc)
    {
        CInteger ci = new()
        {
            SourceLine = loc.Line,
            SourceColumn = loc.Column,
            RmTypeName = "Integer",
        };
        List<int> values = [];
        while (true)
        {
            Adl2TokenInfo t = state.Peek();
            if (t.Kind == Adl2TokenKind.IntegerLiteral)
            {
                if (int.TryParse(t.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                {
                    values.Add(v);
                }
                state.Consume();
            }
            else if (t.Kind == Adl2TokenKind.Comma)
            {
                state.Consume();
            }
            else
            {
                break;
            }
        }
        ci.EnumeratedValues = values;
        return ci;
    }

    private static CReal ParseCReal(ParserState state)
    {
        Adl2TokenInfo first = state.Peek();
        return ParseCRealInline(state, first);
    }

    private static CReal ParseCRealInline(ParserState state, Adl2TokenInfo loc)
    {
        CReal cr = new()
        {
            SourceLine = loc.Line,
            SourceColumn = loc.Column,
            RmTypeName = "Real",
        };
        List<double> values = [];
        while (true)
        {
            Adl2TokenInfo t = state.Peek();
            if (t.Kind == Adl2TokenKind.RealLiteral || t.Kind == Adl2TokenKind.IntegerLiteral)
            {
                if (double.TryParse(t.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                {
                    values.Add(v);
                }
                state.Consume();
            }
            else if (t.Kind == Adl2TokenKind.Comma)
            {
                state.Consume();
            }
            else
            {
                break;
            }
        }
        cr.EnumeratedValues = values;
        return cr;
    }

    private static CBoolean ParseCBoolean(ParserState state)
    {
        Adl2TokenInfo first = state.Peek();
        CBoolean cb = new()
        {
            RmTypeName = "Boolean",
            SourceLine = first.Line,
            SourceColumn = first.Column,
            TrueValid = false,
            FalseValid = false,
        };
        while (true)
        {
            Adl2TokenInfo t = state.Peek();
            if (t.Kind == Adl2TokenKind.Identifier && t.Text == "true")
            {
                cb.TrueValid = true;
                state.Consume();
            }
            else if (t.Kind == Adl2TokenKind.Identifier && t.Text == "false")
            {
                cb.FalseValid = true;
                state.Consume();
            }
            else if (t.Kind == Adl2TokenKind.Comma)
            {
                state.Consume();
            }
            else
            {
                break;
            }
        }
        if (!cb.TrueValid && !cb.FalseValid)
        {
            cb.TrueValid = true;
            cb.FalseValid = true;
        }
        return cb;
    }

    private static CObject ParseCTerminologyCode(ParserState state)
    {
        Adl2TokenInfo first = state.Peek();
        // Three accepted shapes:
        //  [ac0001]                                  -> AcCode token
        //  [ac0001; at0001]                          -> LBracket, ..., RBracket
        //  [at0001, at0002, ...] / [local::at0001…]  -> LBracket, ..., RBracket
        if (first.Kind == Adl2TokenKind.AcCode)
        {
            state.Consume();
            return new CTerminologyCode
            {
                RmTypeName = "CodePhrase",
                TerminologyId = "local",
                ValueSetRef = first.Value ?? first.Text,
                SourceLine = first.Line,
                SourceColumn = first.Column,
            };
        }
        if (first.Kind == Adl2TokenKind.AtCode)
        {
            state.Consume();
            CTerminologyCode tc = new()
            {
                RmTypeName = "CodePhrase",
                TerminologyId = "local",
                SourceLine = first.Line,
                SourceColumn = first.Column,
                EnumeratedValues = [first.Value ?? first.Text],
            };
            return tc;
        }
        if (first.Kind == Adl2TokenKind.LBracket)
        {
            state.Consume();
            CTerminologyCode tc = new()
            {
                RmTypeName = "CodePhrase",
                TerminologyId = "local",
                SourceLine = first.Line,
                SourceColumn = first.Column,
            };
            List<string> codes = [];
            string? assumed = null;
            string terminology = "local";
            bool seenSemicolon = false;
            bool seenColonColon = false;
            int idx = 0;
            while (true)
            {
                Adl2TokenInfo t = state.Peek();
                if (t.Kind == Adl2TokenKind.RBracket)
                {
                    state.Consume();
                    break;
                }
                if (t.Kind == Adl2TokenKind.Comma)
                {
                    state.Consume();
                    continue;
                }
                if (t.Kind == Adl2TokenKind.Semicolon)
                {
                    state.Consume();
                    seenSemicolon = true;
                    idx = 0; // reset; everything after ';' is the assumed value
                    continue;
                }
                if (t.Kind == Adl2TokenKind.Colon)
                {
                    state.Consume();
                    // Look for second colon (::)
                    if (state.Peek().Kind == Adl2TokenKind.Colon)
                    {
                        state.Consume();
                        seenColonColon = true;
                        if (codes.Count == 1)
                        {
                            terminology = codes[0];
                            codes.Clear();
                        }
                    }
                    continue;
                }
                if (t.Kind == Adl2TokenKind.Identifier
                    || t.Kind == Adl2TokenKind.AtCode
                    || t.Kind == Adl2TokenKind.AcCode
                    || t.Kind == Adl2TokenKind.IdCode)
                {
                    state.Consume();
                    string codeText = t.Value ?? t.Text;
                    if (seenSemicolon)
                    {
                        assumed = codeText;
                    }
                    else
                    {
                        codes.Add(codeText);
                    }
                    idx++;
                    continue;
                }
                throw state.Error($"Unexpected token in terminology code constraint: {t.Kind} '{t.Text}'.", t);
            }
            tc.TerminologyId = terminology;
            if (codes.Count == 1 && codes[0].StartsWith("ac", StringComparison.Ordinal) && !seenColonColon)
            {
                tc.ValueSetRef = codes[0];
            }
            else
            {
                tc.EnumeratedValues = codes;
            }
            if (assumed is not null)
            {
                ((CPrimitiveObject<string>)tc).DefaultValue = assumed;
            }
            return tc;
        }
        throw state.Error($"Expected terminology code (got {first.Kind} '{first.Text}').", first);
    }

    private static CObject ParseIntervalPrimitive(ParserState state, Adl2TokenInfo? loc = null)
    {
        Adl2TokenInfo openTok = state.Expect(Adl2TokenKind.IntervalDelim, "'|'");
        Adl2TokenInfo at = loc ?? openTok;
        // Need to detect whether this is an integer or real interval by
        // peeking ahead for any real literal token.
        // Snapshot index, peek for any real, restore.
        int saveIndex = state.Index;
        bool isReal = false;
        while (state.Index < state.Tokens.Count)
        {
            Adl2TokenInfo t = state.Tokens[state.Index];
            if (t.Kind == Adl2TokenKind.IntervalDelim || t.Kind == Adl2TokenKind.Eof)
            {
                break;
            }
            if (t.Kind == Adl2TokenKind.RealLiteral)
            {
                isReal = true;
                break;
            }
            state.Index++;
        }
        state.Index = saveIndex;

        if (isReal)
        {
            Interval<double> r = ParseRealInterval(state);
            state.Expect(Adl2TokenKind.IntervalDelim, "'|'");
            return new CReal
            {
                Range = r,
                RmTypeName = "Real",
                SourceLine = at.Line,
                SourceColumn = at.Column,
            };
        }
        else
        {
            Interval<long> r = ParseLongInterval(state);
            state.Expect(Adl2TokenKind.IntervalDelim, "'|'");
            return new CInteger
            {
                // Stored on CInteger.Range as Interval<int>. Downcast.
                Range = ConvertLongIntervalToInt(r),
                RmTypeName = "Integer",
                SourceLine = at.Line,
                SourceColumn = at.Column,
            };
        }
    }

    private static Interval<int>? ConvertLongIntervalToInt(Interval<long> r)
    {
        int? low = r.HasLower ? checked((int)r.Lower) : null;
        int? high = r.HasUpper ? checked((int)r.Upper) : null;
        return BuildInterval<int>(low, high, r.LowerIncluded, r.UpperIncluded, r.HasLower, r.HasUpper);
    }

    // ------------------------------------------------------------------
    // Interval parsing
    // ------------------------------------------------------------------

    private static Interval<int> ParseIntInterval(ParserState state)
    {
        // Forms accepted here (inside a `{...}` from cardinality/existence/occurrences):
        //   1..5
        //   1..*
        //   *..5
        //   *
        //   {N..M}, {N..*}, etc.
        //   {|interval|}
        // Returns Interval<int>.
        if (state.Peek().Kind == Adl2TokenKind.IntervalDelim)
        {
            state.Consume();
            Interval<long> r = ParseLongInterval(state);
            state.Expect(Adl2TokenKind.IntervalDelim, "'|'");
            return BuildInterval<int>(
                r.HasLower ? checked((int)r.Lower) : null,
                r.HasUpper ? checked((int)r.Upper) : null,
                r.LowerIncluded, r.UpperIncluded, r.HasLower, r.HasUpper)!;
        }
        return ConvertLongIntervalToInt(ParseLongInterval(state))!;
    }

    private static Interval<long> ParseLongInterval(ParserState state)
    {
        // Parses: [op] (value|*) [.. [op] (value|*)] where op ∈ {<, <=, >, >=}
        // Note: most ADL intervals use no comparison ops; bare N..M is shorthand for [N..M] inclusive.
        bool lowerOpen = false;
        bool upperOpen = false;
        bool lowerStar = false;
        bool upperStar = false;
        long lower = 0;
        long upper = 0;
        bool hasLower = false;
        bool hasUpper = false;

        // Lower part
        Adl2TokenInfo t = state.Peek();
        if (t.Kind == Adl2TokenKind.GreaterThan)
        {
            lowerOpen = true; state.Consume(); t = state.Peek();
        }
        else if (t.Kind == Adl2TokenKind.GreaterEqual)
        {
            state.Consume(); t = state.Peek();
        }
        if (t.Kind == Adl2TokenKind.Star)
        {
            lowerStar = true;
            state.Consume();
        }
        else if (t.Kind == Adl2TokenKind.IntegerLiteral)
        {
            lower = long.Parse(t.Text, CultureInfo.InvariantCulture);
            hasLower = true;
            state.Consume();
        }
        else
        {
            throw state.Error($"Expected integer or '*' (got {t.Kind} '{t.Text}').", t);
        }

        // '..' separator?
        if (state.Peek().Kind == Adl2TokenKind.Range)
        {
            state.Consume();
            t = state.Peek();
            if (t.Kind == Adl2TokenKind.LessThan)
            {
                upperOpen = true; state.Consume(); t = state.Peek();
            }
            else if (t.Kind == Adl2TokenKind.LessEqual)
            {
                state.Consume(); t = state.Peek();
            }
            if (t.Kind == Adl2TokenKind.Star)
            {
                upperStar = true;
                state.Consume();
            }
            else if (t.Kind == Adl2TokenKind.IntegerLiteral)
            {
                upper = long.Parse(t.Text, CultureInfo.InvariantCulture);
                hasUpper = true;
                state.Consume();
            }
            else
            {
                throw state.Error($"Expected integer or '*' (got {t.Kind} '{t.Text}').", t);
            }
        }
        else
        {
            // Single value: interval is {N..N}
            if (!lowerStar)
            {
                upper = lower;
                hasUpper = true;
            }
        }

        bool lowerIncluded = !lowerOpen;
        bool upperIncluded = !upperOpen;
        if (lowerStar) hasLower = false;
        if (upperStar) hasUpper = false;
        return BuildInterval<long>(
            hasLower ? lower : null,
            hasUpper ? upper : null,
            lowerIncluded, upperIncluded, hasLower, hasUpper)!;
    }

    private static Interval<double> ParseRealInterval(ParserState state)
    {
        bool lowerOpen = false;
        bool upperOpen = false;
        bool lowerStar = false;
        bool upperStar = false;
        double lower = 0;
        double upper = 0;
        bool hasLower = false;
        bool hasUpper = false;

        Adl2TokenInfo t = state.Peek();
        if (t.Kind == Adl2TokenKind.GreaterThan) { lowerOpen = true; state.Consume(); t = state.Peek(); }
        else if (t.Kind == Adl2TokenKind.GreaterEqual) { state.Consume(); t = state.Peek(); }

        if (t.Kind == Adl2TokenKind.Star)
        {
            lowerStar = true; state.Consume();
        }
        else if (t.Kind == Adl2TokenKind.RealLiteral || t.Kind == Adl2TokenKind.IntegerLiteral)
        {
            lower = double.Parse(t.Text, CultureInfo.InvariantCulture);
            hasLower = true;
            state.Consume();
        }
        else
        {
            throw state.Error($"Expected real or '*' (got {t.Kind} '{t.Text}').", t);
        }

        if (state.Peek().Kind == Adl2TokenKind.Range)
        {
            state.Consume();
            t = state.Peek();
            if (t.Kind == Adl2TokenKind.LessThan) { upperOpen = true; state.Consume(); t = state.Peek(); }
            else if (t.Kind == Adl2TokenKind.LessEqual) { state.Consume(); t = state.Peek(); }
            if (t.Kind == Adl2TokenKind.Star)
            {
                upperStar = true; state.Consume();
            }
            else if (t.Kind == Adl2TokenKind.RealLiteral || t.Kind == Adl2TokenKind.IntegerLiteral)
            {
                upper = double.Parse(t.Text, CultureInfo.InvariantCulture);
                hasUpper = true;
                state.Consume();
            }
            else
            {
                throw state.Error($"Expected real or '*' (got {t.Kind} '{t.Text}').", t);
            }
        }
        else
        {
            if (!lowerStar) { upper = lower; hasUpper = true; }
        }

        if (lowerStar) hasLower = false;
        if (upperStar) hasUpper = false;
        return BuildInterval<double>(
            hasLower ? lower : null,
            hasUpper ? upper : null,
            !lowerOpen, !upperOpen, hasLower, hasUpper)!;
    }

    private static Interval<T>? BuildInterval<T>(
        T? lower, T? upper, bool lowerInclusive, bool upperInclusive, bool hasLower, bool hasUpper)
        where T : struct, IComparable<T>
    {
        if (!hasLower && !hasUpper)
        {
            return Interval<T>.Unbounded();
        }
        if (hasLower && !hasUpper)
        {
            return lowerInclusive ? Interval<T>.AtLeast(lower!.Value) : Interval<T>.GreaterThan(lower!.Value);
        }
        if (!hasLower && hasUpper)
        {
            return upperInclusive ? Interval<T>.AtMost(upper!.Value) : Interval<T>.LessThan(upper!.Value);
        }
        if (lowerInclusive && upperInclusive)
        {
            return Interval<T>.Bounded(lower!.Value, upper!.Value);
        }
        if (lowerInclusive && !upperInclusive)
        {
            return Interval<T>.UpperOpen(lower!.Value, upper!.Value);
        }
        if (!lowerInclusive && upperInclusive)
        {
            return Interval<T>.LowerOpen(lower!.Value, upper!.Value);
        }
        return Interval<T>.Open(lower!.Value, upper!.Value);
    }
}
