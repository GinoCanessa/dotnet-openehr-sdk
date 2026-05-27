using System.Text;

namespace DotnetOpenEhr.Archetypes.Adl2;

/// <summary>
/// Hand-written ADL2 tokenizer. Mirrors the shape of the Phase-5
/// <c>OdinLexer</c>: a <see cref="ReadOnlySpan{T}"/>-backed
/// <c>ref struct</c> that tracks 1-based line / column and allocates
/// only when materialising decoded lexeme contents (strings, regexes,
/// node-id predicates).
/// </summary>
/// <remarks>
/// <para>
/// // GRAMMAR: openEHR ADL 2.0.6 specification, sections 4 (top-level
/// archetype structure), 5 (cADL constraint syntax), 6 (rule
/// expressions), and 7 (terminology codes).
/// </para>
/// <para>
/// The lexer is mostly context-free. Two narrow context flags handle
/// ADL2's well-known structural quirks:
/// </para>
/// <list type="bullet">
///   <item>
///     <description><c>_expectArchetypeHrid</c> — set after one of the
///     <c>archetype</c> / <c>template</c> / <c>template_overlay</c> /
///     <c>operational_template</c> / <c>specialize</c> keywords and
///     any trailing <c>(metadata)</c> block, causing the next bare
///     identifier-like run to be lexed as
///     <see cref="Adl2TokenKind.ArchetypeHridLiteral"/> rather than
///     a sequence of <see cref="Adl2TokenKind.Identifier"/> tokens.</description>
///   </item>
///   <item>
///     <description><c>_inOdinSection</c> — set after a section header
///     keyword from the ODIN-bearing set (<c>language</c>,
///     <c>description</c>, <c>terminology</c>, <c>annotations</c>,
///     <c>default_value</c>) at top level (group depth zero), causing
///     any subsequent top-level <c>&lt;…&gt;</c> to be emitted as a
///     single <see cref="Adl2TokenKind.OdinBlock"/> token (the parser
///     re-tokenises the inner span with <c>OdinParser</c>). The flag
///     is cleared when another top-level section keyword is emitted.</description>
///   </item>
/// </list>
/// </remarks>
public ref struct Adl2Lexer
{
    private readonly ReadOnlySpan<char> _source;
    private int _pos;
    private int _line;
    private int _column;
    private int _groupDepth;
    private bool _expectArchetypeHrid;
    private bool _inOdinSection;
    private Adl2TokenKind _lastKind;

    // GRAMMAR: spec 4.1 - ADL2 section / structural keywords. Kept
    // case-sensitive per the spec.
    private static readonly HashSet<string> s_keywords = new(StringComparer.Ordinal)
    {
        "archetype", "template", "template_overlay", "operational_template",
        "differential", "specialize", "language", "description", "definition",
        "rules", "terminology", "annotations", "concept", "existence",
        "cardinality", "occurrences", "matches", "ordered", "unordered",
        "unique", "use_node", "use_archetype", "allow_archetype", "include",
        "exclude", "before", "after", "then", "assert", "for_each", "in",
        "where",
    };

    private static readonly HashSet<string> s_sectionKeywords = new(StringComparer.Ordinal)
    {
        "archetype", "template", "template_overlay", "operational_template",
        "specialize", "language", "description", "definition", "rules",
        "terminology", "annotations",
    };

    private static readonly HashSet<string> s_odinSectionKeywords = new(StringComparer.Ordinal)
    {
        "language", "description", "terminology", "annotations", "default_value",
    };

    private static readonly HashSet<string> s_archetypeHridStarters = new(StringComparer.Ordinal)
    {
        "archetype", "template", "template_overlay", "operational_template",
        "specialize",
    };

    public Adl2Lexer(ReadOnlySpan<char> source)
    {
        _source = source;
        _pos = 0;
        _line = 1;
        _column = 1;
        _groupDepth = 0;
        _expectArchetypeHrid = false;
        _inOdinSection = false;
        _lastKind = Adl2TokenKind.Eof;
    }

    public int Position => _pos;
    public int Line => _line;
    public int Column => _column;
    public ReadOnlySpan<char> Source => _source;

    /// <summary>
    /// Consume and return the next token. At end of input keeps
    /// returning <see cref="Adl2TokenKind.Eof"/> without advancing.
    /// </summary>
    public Adl2Token NextToken()
    {
        // Skip horizontal whitespace and line comments. Newlines are
        // tokens in their own right (per plan).
        SkipHorizontalTrivia();

        if (_pos >= _source.Length)
        {
            return MakeToken(Adl2TokenKind.Eof, _pos, 0, _line, _column);
        }

        int startPos = _pos;
        int startLine = _line;
        int startCol = _column;
        char c = _source[_pos];

        // Newlines as tokens. Collapse a run into a single Newline.
        if (c == '\r' || c == '\n')
        {
            int runStart = _pos;
            while (_pos < _source.Length)
            {
                char nc = _source[_pos];
                if (nc == '\r')
                {
                    if (_pos + 1 < _source.Length && _source[_pos + 1] == '\n')
                    {
                        AdvanceNewline(2);
                    }
                    else
                    {
                        AdvanceNewline(1);
                    }
                    continue;
                }
                if (nc == '\n')
                {
                    AdvanceNewline(1);
                    continue;
                }
                if (nc == ' ' || nc == '\t')
                {
                    // Allow blank-line runs to fold into one Newline.
                    Advance(1);
                    continue;
                }
                break;
            }
            Adl2Token nl = MakeToken(Adl2TokenKind.Newline, runStart, _pos - runStart, startLine, startCol);
            _lastKind = nl.Kind;
            return nl;
        }

        // Punctuation / operators.
        if (TryScanPunctuation(startPos, startLine, startCol, c, out Adl2Token punct))
        {
            _lastKind = punct.Kind;
            return punct;
        }

        // Numeric literals (allow leading '-' only when not following a
        // value: keeps `a-b` as identifier/minus/identifier in rules).
        if (IsDigit(c))
        {
            Adl2Token num = ScanNumber(startPos, startLine, startCol);
            _lastKind = num.Kind;
            return num;
        }

        // Strings.
        if (c == '"')
        {
            Adl2Token s = ScanString(startPos, startLine, startCol);
            _lastKind = s.Kind;
            return s;
        }

        // Identifiers and keywords (including ArchetypeHRID after a
        // header keyword).
        if (IsIdentStart(c))
        {
            if (_expectArchetypeHrid && _groupDepth == 0)
            {
                Adl2Token hrid = ScanArchetypeHrid(startPos, startLine, startCol);
                _expectArchetypeHrid = false;
                _lastKind = hrid.Kind;
                return hrid;
            }
            Adl2Token id = ScanIdentifierOrKeyword(startPos, startLine, startCol);
            _lastKind = id.Kind;
            return id;
        }

        throw NewError($"Unexpected character '{c}'.", startLine, startCol);
    }

    private bool TryScanPunctuation(int startPos, int startLine, int startCol, char c, out Adl2Token token)
    {
        switch (c)
        {
            case '(':
                _groupDepth++;
                Advance(1);
                token = MakeToken(Adl2TokenKind.LParen, startPos, 1, startLine, startCol);
                return true;
            case ')':
                if (_groupDepth > 0) _groupDepth--;
                Advance(1);
                token = MakeToken(Adl2TokenKind.RParen, startPos, 1, startLine, startCol);
                return true;
            case '{':
                _groupDepth++;
                Advance(1);
                token = MakeToken(Adl2TokenKind.LBrace, startPos, 1, startLine, startCol);
                return true;
            case '}':
                if (_groupDepth > 0) _groupDepth--;
                Advance(1);
                token = MakeToken(Adl2TokenKind.RBrace, startPos, 1, startLine, startCol);
                return true;
            case '[':
                if (LooksLikeTerminologyCode())
                {
                    token = ScanTerminologyCode(startPos, startLine, startCol);
                    return true;
                }
                _groupDepth++;
                Advance(1);
                token = MakeToken(Adl2TokenKind.LBracket, startPos, 1, startLine, startCol);
                return true;
            case ']':
                if (_groupDepth > 0) _groupDepth--;
                Advance(1);
                token = MakeToken(Adl2TokenKind.RBracket, startPos, 1, startLine, startCol);
                return true;
            case '|':
                Advance(1);
                token = MakeToken(Adl2TokenKind.IntervalDelim, startPos, 1, startLine, startCol);
                return true;
            case ',':
                Advance(1);
                token = MakeToken(Adl2TokenKind.Comma, startPos, 1, startLine, startCol);
                return true;
            case ';':
                Advance(1);
                token = MakeToken(Adl2TokenKind.Semicolon, startPos, 1, startLine, startCol);
                return true;
            case ':':
                Advance(1);
                token = MakeToken(Adl2TokenKind.Colon, startPos, 1, startLine, startCol);
                return true;
            case '=':
                if (PeekAt(_pos + 1) == '=')
                {
                    Advance(2);
                    token = MakeToken(Adl2TokenKind.Equals, startPos, 2, startLine, startCol);
                    return true;
                }
                Advance(1);
                token = MakeToken(Adl2TokenKind.Equals, startPos, 1, startLine, startCol);
                return true;
            case '!':
                if (PeekAt(_pos + 1) == '=')
                {
                    Advance(2);
                    token = MakeToken(Adl2TokenKind.NotEqual, startPos, 2, startLine, startCol);
                    return true;
                }
                throw NewError("Unexpected '!' (only '!=' is supported).", startLine, startCol);
            case '<':
                if (_inOdinSection && _groupDepth == 0)
                {
                    token = ScanOdinBlock(startPos, startLine, startCol);
                    return true;
                }
                if (PeekAt(_pos + 1) == '=')
                {
                    Advance(2);
                    token = MakeToken(Adl2TokenKind.LessEqual, startPos, 2, startLine, startCol);
                    return true;
                }
                Advance(1);
                token = MakeToken(Adl2TokenKind.LessThan, startPos, 1, startLine, startCol);
                return true;
            case '>':
                if (PeekAt(_pos + 1) == '=')
                {
                    Advance(2);
                    token = MakeToken(Adl2TokenKind.GreaterEqual, startPos, 2, startLine, startCol);
                    return true;
                }
                Advance(1);
                token = MakeToken(Adl2TokenKind.GreaterThan, startPos, 1, startLine, startCol);
                return true;
            case '+':
                Advance(1);
                token = MakeToken(Adl2TokenKind.Plus, startPos, 1, startLine, startCol);
                return true;
            case '-':
                if (PeekAt(_pos + 1) is char d1 && IsDigit(d1) && !FollowsValueToken())
                {
                    token = ScanNumber(startPos, startLine, startCol);
                    return true;
                }
                Advance(1);
                token = MakeToken(Adl2TokenKind.Minus, startPos, 1, startLine, startCol);
                return true;
            case '*':
                Advance(1);
                token = MakeToken(Adl2TokenKind.Star, startPos, 1, startLine, startCol);
                return true;
            case '.':
                if (PeekAt(_pos + 1) == '.')
                {
                    Advance(2);
                    token = MakeToken(Adl2TokenKind.Range, startPos, 2, startLine, startCol);
                    return true;
                }
                throw NewError("Unexpected '.' (use '..' for ranges).", startLine, startCol);
            case '/':
                char next = PeekAt(_pos + 1);
                if (IsIdentStart(next))
                {
                    token = ScanPathSegment(startPos, startLine, startCol);
                    return true;
                }
                if (FollowsValueToken())
                {
                    Advance(1);
                    token = MakeToken(Adl2TokenKind.Slash, startPos, 1, startLine, startCol);
                    return true;
                }
                token = ScanRegex(startPos, startLine, startCol);
                return true;
        }
        token = default;
        return false;
    }

    // -- High-level scanners --------------------------------------------------

    private Adl2Token ScanIdentifierOrKeyword(int startPos, int startLine, int startCol)
    {
        int p = _pos;
        while (p < _source.Length && IsIdentContinue(_source[p]))
        {
            p++;
        }
        int len = p - _pos;
        ReadOnlySpan<char> slice = _source.Slice(_pos, len);
        string text = slice.ToString();
        _pos = p;
        _column += len;

        if (s_keywords.Contains(text))
        {
            // Maintain context flags for HRID + ODIN-section dispatch.
            if (s_archetypeHridStarters.Contains(text))
            {
                _expectArchetypeHrid = true;
            }
            if (s_sectionKeywords.Contains(text) && _groupDepth == 0)
            {
                _inOdinSection = s_odinSectionKeywords.Contains(text);
            }
            return new Adl2Token(
                Adl2TokenKind.Keyword,
                _source.Slice(startPos, _pos - startPos),
                startPos,
                _pos - startPos,
                startLine,
                startCol,
                value: text);
        }

        return new Adl2Token(
            Adl2TokenKind.Identifier,
            _source.Slice(startPos, _pos - startPos),
            startPos,
            _pos - startPos,
            startLine,
            startCol,
            value: text);
    }

    private Adl2Token ScanArchetypeHrid(int startPos, int startLine, int startCol)
    {
        // GRAMMAR: spec 7 - archetype HRID = qualified-rm-entity '.'
        // concept-id '.' version-id [ '.' build-count ] [ '-' suffix ].
        // We greedily consume any ASCII identifier / dash / dot run,
        // letting the parser validate structure.
        int p = _pos;
        while (p < _source.Length)
        {
            char ch = _source[p];
            if (IsIdentContinue(ch) || ch == '-' || ch == '.')
            {
                // '.' followed by '.' would be the Range token; stop.
                if (ch == '.' && p + 1 < _source.Length && _source[p + 1] == '.')
                {
                    break;
                }
                p++;
                continue;
            }
            break;
        }
        int len = p - _pos;
        if (len == 0)
        {
            throw NewError("Empty archetype HRID literal.", startLine, startCol);
        }
        string text = _source.Slice(_pos, len).ToString();
        _pos = p;
        _column += len;
        return new Adl2Token(
            Adl2TokenKind.ArchetypeHridLiteral,
            _source.Slice(startPos, _pos - startPos),
            startPos,
            _pos - startPos,
            startLine,
            startCol,
            value: text);
    }

    private Adl2Token ScanNumber(int startPos, int startLine, int startCol)
    {
        int p = _pos;
        if (_source[p] == '-')
        {
            p++;
        }
        int digitStart = p;
        while (p < _source.Length && IsDigit(_source[p]))
        {
            p++;
        }
        if (p == digitStart)
        {
            throw NewError("Expected digit.", startLine, startCol);
        }
        bool isReal = false;
        if (p < _source.Length && _source[p] == '.')
        {
            // Range '..' wins over decimal point.
            if (p + 1 < _source.Length && _source[p + 1] == '.')
            {
                // leave the '..' for the next token
            }
            else if (p + 1 < _source.Length && IsDigit(_source[p + 1]))
            {
                isReal = true;
                p++;
                while (p < _source.Length && IsDigit(_source[p]))
                {
                    p++;
                }
            }
        }
        if (p < _source.Length && (_source[p] == 'e' || _source[p] == 'E'))
        {
            isReal = true;
            p++;
            if (p < _source.Length && (_source[p] == '+' || _source[p] == '-'))
            {
                p++;
            }
            int expStart = p;
            while (p < _source.Length && IsDigit(_source[p]))
            {
                p++;
            }
            if (p == expStart)
            {
                throw NewError("Expected digits in numeric exponent.", startLine, startCol);
            }
        }
        int len = p - _pos;
        _pos = p;
        _column += len;
        return MakeToken(
            isReal ? Adl2TokenKind.RealLiteral : Adl2TokenKind.IntegerLiteral,
            startPos,
            len,
            startLine,
            startCol);
    }

    private Adl2Token ScanString(int startPos, int startLine, int startCol)
    {
        // Consume opening quote.
        Advance(1);
        StringBuilder? builder = null;
        int rawStart = _pos;
        while (_pos < _source.Length)
        {
            char c = _source[_pos];
            if (c == '"')
            {
                ReadOnlySpan<char> slice = _source.Slice(rawStart, _pos - rawStart);
                string value = builder is null ? slice.ToString() : builder.Append(slice).ToString();
                Advance(1);
                return new Adl2Token(
                    Adl2TokenKind.StringLiteral,
                    _source.Slice(startPos, _pos - startPos),
                    startPos,
                    _pos - startPos,
                    startLine,
                    startCol,
                    value: value);
            }
            if (c == '\\')
            {
                builder ??= new StringBuilder();
                builder.Append(_source.Slice(rawStart, _pos - rawStart));
                AppendEscape(builder, startLine, startCol);
                rawStart = _pos;
                continue;
            }
            if (c == '\n')
            {
                AdvanceNewline(1);
                continue;
            }
            if (c == '\r')
            {
                if (_pos + 1 < _source.Length && _source[_pos + 1] == '\n')
                {
                    AdvanceNewline(2);
                }
                else
                {
                    AdvanceNewline(1);
                }
                continue;
            }
            Advance(1);
        }
        throw NewError("Unterminated string literal.", startLine, startCol);
    }

    private void AppendEscape(StringBuilder builder, int startLine, int startCol)
    {
        Advance(1);
        if (_pos >= _source.Length)
        {
            throw NewError("Unterminated escape sequence.", startLine, startCol);
        }
        char esc = _source[_pos];
        switch (esc)
        {
            case 'r': builder.Append('\r'); Advance(1); break;
            case 'n': builder.Append('\n'); Advance(1); break;
            case 't': builder.Append('\t'); Advance(1); break;
            case '\\': builder.Append('\\'); Advance(1); break;
            case '"': builder.Append('"'); Advance(1); break;
            case '\'': builder.Append('\''); Advance(1); break;
            case '/': builder.Append('/'); Advance(1); break;
            default:
                throw NewError($"Invalid escape sequence '\\{esc}'.", startLine, startCol);
        }
    }

    private Adl2Token ScanRegex(int startPos, int startLine, int startCol)
    {
        // Consume opening '/'.
        Advance(1);
        StringBuilder builder = new();
        int chunkStart = _pos;
        while (_pos < _source.Length)
        {
            char c = _source[_pos];
            if (c == '\\' && _pos + 1 < _source.Length)
            {
                builder.Append(_source.Slice(chunkStart, _pos - chunkStart));
                char nxt = _source[_pos + 1];
                if (nxt == '/' || nxt == '\\')
                {
                    builder.Append(nxt);
                }
                else
                {
                    // Preserve the escape verbatim for regex engines.
                    builder.Append('\\');
                    builder.Append(nxt);
                }
                Advance(2);
                chunkStart = _pos;
                continue;
            }
            if (c == '/')
            {
                builder.Append(_source.Slice(chunkStart, _pos - chunkStart));
                Advance(1);
                return new Adl2Token(
                    Adl2TokenKind.RegexLiteral,
                    _source.Slice(startPos, _pos - startPos),
                    startPos,
                    _pos - startPos,
                    startLine,
                    startCol,
                    value: builder.ToString());
            }
            if (c == '\n' || c == '\r')
            {
                throw NewError("Regex literal cannot span lines.", startLine, startCol);
            }
            Advance(1);
        }
        throw NewError("Unterminated regex literal.", startLine, startCol);
    }

    private bool LooksLikeTerminologyCode()
    {
        // Match [at...], [ac...], [id...] or [<archetype HRID with
        // node id>] forms. We only handle the simple at/ac/id codes
        // here; anything else is a plain LBracket.
        if (_pos + 2 >= _source.Length || _source[_pos] != '[')
        {
            return false;
        }
        int q = _pos + 1;
        // Match prefix: at | ac | id
        if (q + 1 >= _source.Length)
        {
            return false;
        }
        char a = _source[q];
        char b = _source[q + 1];
        bool isAt = a == 'a' && b == 't';
        bool isAc = a == 'a' && b == 'c';
        bool isId = a == 'i' && b == 'd';
        if (!isAt && !isAc && !isId)
        {
            return false;
        }
        int r = q + 2;
        bool sawDigit = false;
        while (r < _source.Length)
        {
            char ch = _source[r];
            if (IsDigit(ch) || ch == '.')
            {
                sawDigit = true;
                r++;
                continue;
            }
            break;
        }
        return sawDigit && r < _source.Length && _source[r] == ']';
    }

    private Adl2Token ScanTerminologyCode(int startPos, int startLine, int startCol)
    {
        // Consume '['.
        Advance(1);
        int prefixStart = _pos;
        // 'at' | 'ac' | 'id' guaranteed by LooksLikeTerminologyCode.
        Advance(2);
        int digitsStart = _pos;
        while (_pos < _source.Length && (IsDigit(_source[_pos]) || _source[_pos] == '.'))
        {
            Advance(1);
        }
        if (_pos >= _source.Length || _source[_pos] != ']')
        {
            throw NewError("Unterminated terminology code (expected ']').", startLine, startCol);
        }
        string codeBody = _source.Slice(prefixStart, _pos - prefixStart).ToString();
        Advance(1); // ']'
        Adl2TokenKind kind = codeBody[1] switch
        {
            't' => Adl2TokenKind.AtCode,
            'c' => Adl2TokenKind.AcCode,
            'd' => Adl2TokenKind.IdCode,
            _ => throw NewError($"Unrecognised terminology code prefix '{codeBody}'.", startLine, startCol),
        };
        _ = digitsStart;
        return new Adl2Token(
            kind,
            _source.Slice(startPos, _pos - startPos),
            startPos,
            _pos - startPos,
            startLine,
            startCol,
            value: codeBody);
    }

    private Adl2Token ScanPathSegment(int startPos, int startLine, int startCol)
    {
        // Consume the leading '/'.
        Advance(1);
        int nameStart = _pos;
        while (_pos < _source.Length && IsIdentContinue(_source[_pos]))
        {
            Advance(1);
        }
        if (_pos == nameStart)
        {
            throw NewError("Expected identifier after '/'.", startLine, startCol);
        }
        string name = _source.Slice(nameStart, _pos - nameStart).ToString();
        string? embeddedNodeId = null;
        // Optional predicate '[idN]' / '[atN]' / '[acN]' immediately
        // attached (no whitespace).
        if (_pos < _source.Length && _source[_pos] == '[' && LooksLikeTerminologyCode())
        {
            int predStart = _pos;
            Advance(1); // '['
            int prefixStart = _pos;
            Advance(2); // 'id' | 'at' | 'ac'
            while (_pos < _source.Length && (IsDigit(_source[_pos]) || _source[_pos] == '.'))
            {
                Advance(1);
            }
            if (_pos >= _source.Length || _source[_pos] != ']')
            {
                throw NewError("Unterminated path segment predicate.", startLine, startCol);
            }
            embeddedNodeId = _source.Slice(prefixStart, _pos - prefixStart).ToString();
            Advance(1); // ']'
            _ = predStart;
        }
        return new Adl2Token(
            Adl2TokenKind.PathSegment,
            _source.Slice(startPos, _pos - startPos),
            startPos,
            _pos - startPos,
            startLine,
            startCol,
            value: name,
            embeddedNodeId: embeddedNodeId);
    }

    private Adl2Token ScanOdinBlock(int startPos, int startLine, int startCol)
    {
        // Capture a balanced <…> span, ignoring '<'/'>' inside string
        // literals ("…") and regex literals (/…/).
        Advance(1); // opening '<'
        int depth = 1;
        while (_pos < _source.Length && depth > 0)
        {
            char c = _source[_pos];
            switch (c)
            {
                case '<':
                    depth++;
                    Advance(1);
                    break;
                case '>':
                    depth--;
                    Advance(1);
                    break;
                case '"':
                    SkipOdinString(startLine, startCol);
                    break;
                case '/':
                    // Regex inside ODIN? Cautious: ODIN does not have
                    // regex literals, only strings. Treat '/' as a
                    // plain character.
                    Advance(1);
                    break;
                case '-':
                    if (_pos + 1 < _source.Length && _source[_pos + 1] == '-')
                    {
                        // Line comment; consume to end of line.
                        while (_pos < _source.Length && _source[_pos] != '\n' && _source[_pos] != '\r')
                        {
                            Advance(1);
                        }
                    }
                    else
                    {
                        Advance(1);
                    }
                    break;
                case '\r':
                    if (_pos + 1 < _source.Length && _source[_pos + 1] == '\n')
                    {
                        AdvanceNewline(2);
                    }
                    else
                    {
                        AdvanceNewline(1);
                    }
                    break;
                case '\n':
                    AdvanceNewline(1);
                    break;
                default:
                    Advance(1);
                    break;
            }
        }
        if (depth != 0)
        {
            throw NewError("Unterminated ODIN block (missing '>').", startLine, startCol);
        }
        int len = _pos - startPos;
        ReadOnlySpan<char> span = _source.Slice(startPos, len);
        // Inner span excludes the outer '<' and '>'.
        string inner = _source.Slice(startPos + 1, len - 2).ToString();
        return new Adl2Token(
            Adl2TokenKind.OdinBlock,
            span,
            startPos,
            len,
            startLine,
            startCol,
            value: inner);
    }

    private void SkipOdinString(int startLine, int startCol)
    {
        // _source[_pos] == '"'.
        Advance(1);
        while (_pos < _source.Length)
        {
            char c = _source[_pos];
            if (c == '\\' && _pos + 1 < _source.Length)
            {
                Advance(2);
                continue;
            }
            if (c == '"')
            {
                Advance(1);
                return;
            }
            if (c == '\n')
            {
                AdvanceNewline(1);
                continue;
            }
            if (c == '\r')
            {
                if (_pos + 1 < _source.Length && _source[_pos + 1] == '\n')
                {
                    AdvanceNewline(2);
                }
                else
                {
                    AdvanceNewline(1);
                }
                continue;
            }
            Advance(1);
        }
        throw NewError("Unterminated string inside ODIN block.", startLine, startCol);
    }

    // -- Trivia and helpers ---------------------------------------------------

    private void SkipHorizontalTrivia()
    {
        while (_pos < _source.Length)
        {
            char c = _source[_pos];
            if (c == ' ' || c == '\t')
            {
                Advance(1);
                continue;
            }
            // GRAMMAR: '--' comments run to end of line.
            if (c == '-' && _pos + 1 < _source.Length && _source[_pos + 1] == '-')
            {
                while (_pos < _source.Length && _source[_pos] != '\n' && _source[_pos] != '\r')
                {
                    Advance(1);
                }
                continue;
            }
            break;
        }
    }

    private bool FollowsValueToken()
    {
        // After identifiers, literals, codes, paths, and closing brackets
        // the lexer is in "value position" — '/' becomes division
        // rather than a regex or path start.
        return _lastKind switch
        {
            Adl2TokenKind.Identifier or
            Adl2TokenKind.IntegerLiteral or
            Adl2TokenKind.RealLiteral or
            Adl2TokenKind.StringLiteral or
            Adl2TokenKind.RegexLiteral or
            Adl2TokenKind.AtCode or
            Adl2TokenKind.AcCode or
            Adl2TokenKind.IdCode or
            Adl2TokenKind.ArchetypeHridLiteral or
            Adl2TokenKind.PathSegment or
            Adl2TokenKind.RParen or
            Adl2TokenKind.RBracket or
            Adl2TokenKind.RBrace => true,
            _ => false,
        };
    }

    private static bool IsDigit(char c) => c >= '0' && c <= '9';

    private static bool IsIdentStart(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';

    private static bool IsIdentContinue(char c)
        => IsIdentStart(c) || IsDigit(c);

    private readonly char PeekAt(int idx)
        => idx < _source.Length ? _source[idx] : '\0';

    private void Advance(int n)
    {
        _pos += n;
        _column += n;
    }

    private void AdvanceNewline(int n)
    {
        _pos += n;
        _line++;
        _column = 1;
    }

    private Adl2Token MakeToken(Adl2TokenKind kind, int start, int length, int line, int column)
    {
        ReadOnlySpan<char> span = length == 0
            ? []
            : _source.Slice(start, length);
        return new Adl2Token(kind, span, start, length, line, column);
    }

    private Adl2LexException NewError(string message, int line, int column)
        => new(message, line, column);
}
