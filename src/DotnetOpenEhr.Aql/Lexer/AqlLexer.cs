using System.Collections.Frozen;
using System.Text;

namespace DotnetOpenEhr.Aql.Lexer;

/// <summary>
/// Hand-written AQL tokenizer. Operates entirely on
/// <see cref="ReadOnlySpan{T}"/> over the source; allocates only when
/// materialising decoded lexeme contents (strings, paths, codes, HRIDs).
/// Tracks 1-based line / column for error reporting.
/// </summary>
/// <remarks>
/// // GRAMMAR: openEHR AQL ANTLR4 lexer grammar (current spec). Keywords
/// match case-insensitively. The lexer is mostly context-free; one
/// minor context flag is used to enable archetype-HRID and
/// terminology-code recognition immediately after a <c>[</c>.
/// </remarks>
public ref struct AqlLexer
{
    private readonly ReadOnlySpan<char> _source;
    private int _pos;
    private int _line;
    private int _column;
    private int _bracketDepth;
    private AqlTokenKind _lastKind;

    public AqlLexer(ReadOnlySpan<char> source)
    {
        _source = source;
        _pos = 0;
        _line = 1;
        _column = 1;
        _bracketDepth = 0;
        _lastKind = AqlTokenKind.EndOfFile;
    }

    public int Position => _pos;
    public int Line => _line;
    public int Column => _column;
    public bool IsEnd => _pos >= _source.Length;
    public ReadOnlySpan<char> Source => _source;

    /// <summary>
    /// Consume and return the next token. At end of input keeps
    /// returning <see cref="AqlTokenKind.EndOfFile"/> without advancing.
    /// </summary>
    public AqlToken NextToken()
    {
        SkipTrivia();

        if (_pos >= _source.Length)
        {
            return MakeToken(AqlTokenKind.EndOfFile, _pos, 0, _line, _column);
        }

        int startPos = _pos;
        int startLine = _line;
        int startCol = _column;
        char c = _source[_pos];

        // Single-character punctuation and multi-char operators.
        switch (c)
        {
            case '(':
                Advance(1);
                return Emit(AqlTokenKind.LeftParen, startPos, 1, startLine, startCol);
            case ')':
                Advance(1);
                return Emit(AqlTokenKind.RightParen, startPos, 1, startLine, startCol);
            case '[':
                _bracketDepth++;
                Advance(1);
                return Emit(AqlTokenKind.LeftBracket, startPos, 1, startLine, startCol);
            case ']':
                if (_bracketDepth > 0) _bracketDepth--;
                Advance(1);
                return Emit(AqlTokenKind.RightBracket, startPos, 1, startLine, startCol);
            case '{':
                Advance(1);
                return Emit(AqlTokenKind.LeftBrace, startPos, 1, startLine, startCol);
            case '}':
                Advance(1);
                return Emit(AqlTokenKind.RightBrace, startPos, 1, startLine, startCol);
            case ',':
                Advance(1);
                return Emit(AqlTokenKind.Comma, startPos, 1, startLine, startCol);
            case ';':
                Advance(1);
                return Emit(AqlTokenKind.Semicolon, startPos, 1, startLine, startCol);
            case '.':
                Advance(1);
                return Emit(AqlTokenKind.Dot, startPos, 1, startLine, startCol);
            case '=':
                Advance(1);
                return Emit(AqlTokenKind.Equals, startPos, 1, startLine, startCol);
            case '!':
                if (PeekAt(_pos + 1) == '=')
                {
                    Advance(2);
                    return Emit(AqlTokenKind.NotEqual, startPos, 2, startLine, startCol);
                }
                throw NewError("Unexpected '!' (only '!=' is supported).", startLine, startCol);
            case '<':
                if (PeekAt(_pos + 1) == '=')
                {
                    Advance(2);
                    return Emit(AqlTokenKind.LessEqual, startPos, 2, startLine, startCol);
                }
                Advance(1);
                return Emit(AqlTokenKind.LessThan, startPos, 1, startLine, startCol);
            case '>':
                if (PeekAt(_pos + 1) == '=')
                {
                    Advance(2);
                    return Emit(AqlTokenKind.GreaterEqual, startPos, 2, startLine, startCol);
                }
                Advance(1);
                return Emit(AqlTokenKind.GreaterThan, startPos, 1, startLine, startCol);
            case '+':
                Advance(1);
                return Emit(AqlTokenKind.Plus, startPos, 1, startLine, startCol);
            case '-':
                Advance(1);
                return Emit(AqlTokenKind.Minus, startPos, 1, startLine, startCol);
            case '*':
                Advance(1);
                return Emit(AqlTokenKind.Star, startPos, 1, startLine, startCol);
            case '|':
                if (PeekAt(_pos + 1) == '|')
                {
                    Advance(2);
                    return Emit(AqlTokenKind.Concat, startPos, 2, startLine, startCol);
                }
                throw NewError("Unexpected '|' (only '||' is supported).", startLine, startCol);
            case '/':
                // '/' followed by an identifier-start character is the
                // start of a path segment; followed by anything else it
                // is the division operator (rare in WHERE / SELECT
                // expressions but the spec allows it via STRING/Numeric
                // arithmetic).
                if (IsIdentStart(PeekAt(_pos + 1)))
                {
                    return ScanPathSegment(startPos, startLine, startCol);
                }
                Advance(1);
                return Emit(AqlTokenKind.Slash, startPos, 1, startLine, startCol);
            case '$':
                return ScanPlaceholder(startPos, startLine, startCol);
            case '\'':
            case '"':
                return ScanString(c, startPos, startLine, startCol);
        }

        if (IsDigit(c))
        {
            return ScanNumber(startPos, startLine, startCol);
        }

        if (IsIdentStart(c))
        {
            // Inside a '[' predicate we may see an archetype HRID
            // literal that contains '-' characters. Detect it before
            // committing to the plain identifier scan.
            if (_bracketDepth > 0 && LooksLikeArchetypeHrid())
            {
                return ScanArchetypeHrid(startPos, startLine, startCol);
            }
            return ScanIdentifierOrKeyword(startPos, startLine, startCol);
        }

        throw NewError($"Unexpected character '{c}'.", startLine, startCol);
    }

    // -- High-level scanners --------------------------------------------------

    private AqlToken ScanIdentifierOrKeyword(int startPos, int startLine, int startCol)
    {
        int p = _pos;
        while (p < _source.Length && IsIdentContinue(_source[p]))
        {
            p++;
        }
        int len = p - _pos;
        ReadOnlySpan<char> slice = _source.Slice(_pos, len);
        string text = slice.ToString();

        // ADL code shapes (`at0001`, `id3`, `ac0001`) may continue with
        // dotted version segments (`at0001.5`). The dot wouldn't be
        // accepted by IsIdentContinue, so we extend the scan here when
        // the prefix matches and a `.<digit>` tail is present.
        if (LooksLikeCodePrefix(text))
        {
            while (p + 1 < _source.Length && _source[p] == '.' && IsDigit(_source[p + 1]))
            {
                p++;
                while (p < _source.Length && IsDigit(_source[p]))
                {
                    p++;
                }
            }
            len = p - _pos;
            slice = _source.Slice(_pos, len);
            text = slice.ToString();
        }

        _pos = p;
        _column += len;

        // Match case-insensitive keywords.
        AqlTokenKind keyword = MatchKeyword(text);
        if (keyword != AqlTokenKind.EndOfFile)
        {
            return Emit(keyword, startPos, len, startLine, startCol, value: text);
        }

        // at-, id-, ac- code shapes: `at`/`id`/`ac` immediately followed
        // by digits (with optional '.' segments).
        if (TryMatchAdlCode(text, out AqlTokenKind codeKind))
        {
            return Emit(codeKind, startPos, len, startLine, startCol, value: text);
        }

        return Emit(AqlTokenKind.Identifier, startPos, len, startLine, startCol, value: text);
    }

    private static bool LooksLikeCodePrefix(string text)
    {
        if (text.Length < 3) return false;
        char a = text[0];
        char b = text[1];
        if (!((a == 'a' && b == 't') || (a == 'a' && b == 'c') || (a == 'i' && b == 'd')))
        {
            return false;
        }
        for (int i = 2; i < text.Length; i++)
        {
            if (text[i] < '0' || text[i] > '9') return false;
        }
        return true;
    }

    private AqlToken ScanArchetypeHrid(int startPos, int startLine, int startCol)
    {
        // GRAMMAR: AQL spec - ARCHETYPE_HRID =
        //   (NAMESPACE '::')? IDENT '-' IDENT '-' IDENT '.' CONCEPT '.v' VERSION_ID
        // VERSION_ID = DIGIT+ ('.' DIGIT+)* ( ('-rc'|'-alpha') ('.' DIGIT+)? )?
        // We greedily consume ASCII identifier characters, '-', '.', and
        // '::' segments, stopping at the closing ']' or other obvious
        // terminator.
        int p = _pos;
        while (p < _source.Length)
        {
            char ch = _source[p];
            if (IsIdentContinue(ch) || ch == '-' || ch == '.')
            {
                p++;
                continue;
            }
            if (ch == ':' && p + 1 < _source.Length && _source[p + 1] == ':')
            {
                p += 2;
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
        return Emit(AqlTokenKind.ArchetypeHridLiteral, startPos, len, startLine, startCol, value: text);
    }

    private AqlToken ScanPathSegment(int startPos, int startLine, int startCol)
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

        // Optional predicate of the form `[idN]`, `[atN]`, or `[acN]`
        // attached directly (no whitespace) - absorb it into the
        // PathSegment token. Anything more complex (e.g.
        // `[ehr_id/value=$x]` or `[openEHR-EHR-...]`) is left for the
        // parser, which will see the LeftBracket as the next token.
        string? embeddedNodeId = null;
        if (_pos < _source.Length && _source[_pos] == '[' && LooksLikeSimpleCodePredicate())
        {
            Advance(1); // '['
            int codeStart = _pos;
            while (_pos < _source.Length && _source[_pos] != ']')
            {
                Advance(1);
            }
            if (_pos >= _source.Length || _source[_pos] != ']')
            {
                throw NewError("Unterminated path segment predicate.", startLine, startCol);
            }
            embeddedNodeId = _source.Slice(codeStart, _pos - codeStart).ToString();
            Advance(1); // ']'
        }
        return new AqlToken(
            AqlTokenKind.PathSegment,
            _source.Slice(startPos, _pos - startPos),
            startPos,
            _pos - startPos,
            startLine,
            startCol,
            value: name,
            embeddedNodeId: embeddedNodeId);
    }

    private AqlToken ScanNumber(int startPos, int startLine, int startCol)
    {
        int p = _pos;
        while (p < _source.Length && IsDigit(_source[p]))
        {
            p++;
        }
        bool isReal = false;
        if (p < _source.Length && _source[p] == '.'
            && p + 1 < _source.Length && IsDigit(_source[p + 1]))
        {
            isReal = true;
            p++;
            while (p < _source.Length && IsDigit(_source[p]))
            {
                p++;
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
        return Emit(
            isReal ? AqlTokenKind.RealLiteral : AqlTokenKind.IntegerLiteral,
            startPos,
            len,
            startLine,
            startCol);
    }

    private AqlToken ScanString(char quote, int startPos, int startLine, int startCol)
    {
        // Consume opening quote.
        Advance(1);
        StringBuilder? builder = null;
        int rawStart = _pos;
        while (_pos < _source.Length)
        {
            char c = _source[_pos];
            if (c == quote)
            {
                ReadOnlySpan<char> slice = _source.Slice(rawStart, _pos - rawStart);
                string value = builder is null ? slice.ToString() : builder.Append(slice).ToString();
                Advance(1);
                return new AqlToken(
                    AqlTokenKind.StringLiteral,
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
            case 'a': builder.Append('\a'); Advance(1); break;
            case 'b': builder.Append('\b'); Advance(1); break;
            case 'f': builder.Append('\f'); Advance(1); break;
            case 'v': builder.Append('\v'); Advance(1); break;
            case '\\': builder.Append('\\'); Advance(1); break;
            case '"': builder.Append('"'); Advance(1); break;
            case '\'': builder.Append('\''); Advance(1); break;
            case '?': builder.Append('?'); Advance(1); break;
            default:
                throw NewError($"Invalid escape sequence '\\{esc}'.", startLine, startCol);
        }
    }

    private AqlToken ScanPlaceholder(int startPos, int startLine, int startCol)
    {
        // Consume the '$'.
        Advance(1);
        int nameStart = _pos;
        if (_pos >= _source.Length || !IsIdentStart(_source[_pos]))
        {
            throw NewError("Expected identifier after '$'.", startLine, startCol);
        }
        while (_pos < _source.Length && IsIdentContinue(_source[_pos]))
        {
            Advance(1);
        }
        string name = _source.Slice(nameStart, _pos - nameStart).ToString();
        return new AqlToken(
            AqlTokenKind.Placeholder,
            _source.Slice(startPos, _pos - startPos),
            startPos,
            _pos - startPos,
            startLine,
            startCol,
            value: name);
    }

    // -- Predicates / look-ahead helpers --------------------------------------

    private bool LooksLikeSimpleCodePredicate()
    {
        // Detect `[<prefix><digits>(.<digits>)*]` where prefix is one of
        // `at`, `id`, `ac`. Used by ScanPathSegment to fold the
        // predicate into a single PathSegment token.
        if (_pos + 3 >= _source.Length || _source[_pos] != '[')
        {
            return false;
        }
        int q = _pos + 1;
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

    private bool LooksLikeArchetypeHrid()
    {
        // Look ahead for the qualified-name '-' pattern. An HRID
        // contains at least one '-' inside an otherwise identifier-like
        // run, and is followed by '.' then more identifier chars. This
        // is enough to disambiguate from a bare identifier.
        int p = _pos;
        bool sawDash = false;
        while (p < _source.Length)
        {
            char ch = _source[p];
            if (IsIdentContinue(ch))
            {
                p++;
                continue;
            }
            if (ch == '-')
            {
                sawDash = true;
                p++;
                continue;
            }
            if (ch == ':' && p + 1 < _source.Length && _source[p + 1] == ':')
            {
                // Namespace separator - definitely an HRID.
                return true;
            }
            break;
        }
        if (!sawDash)
        {
            return false;
        }
        // Require the '-' to be followed eventually by a '.' before ']'.
        if (p < _source.Length && _source[p] == '.')
        {
            return true;
        }
        return false;
    }

    private static bool TryMatchAdlCode(string text, out AqlTokenKind kind)
    {
        // Match `at` / `id` / `ac` followed by one or more digits and
        // optional `.digits` segments. Anything else is a plain
        // identifier.
        if (text.Length < 3)
        {
            kind = AqlTokenKind.EndOfFile;
            return false;
        }
        char a = text[0];
        char b = text[1];
        AqlTokenKind candidate;
        if (a == 'a' && b == 't') candidate = AqlTokenKind.AtCode;
        else if (a == 'a' && b == 'c') candidate = AqlTokenKind.AcCode;
        else if (a == 'i' && b == 'd') candidate = AqlTokenKind.IdCode;
        else { kind = AqlTokenKind.EndOfFile; return false; }
        bool sawDigit = false;
        for (int i = 2; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch >= '0' && ch <= '9')
            {
                sawDigit = true;
                continue;
            }
            if (ch == '.' && sawDigit)
            {
                // require a digit following the dot
                if (i + 1 >= text.Length || text[i + 1] < '0' || text[i + 1] > '9')
                {
                    kind = AqlTokenKind.EndOfFile;
                    return false;
                }
                continue;
            }
            kind = AqlTokenKind.EndOfFile;
            return false;
        }
        if (!sawDigit)
        {
            kind = AqlTokenKind.EndOfFile;
            return false;
        }
        kind = candidate;
        return true;
    }

    // M9 — case-insensitive keyword table. `FrozenDictionary` with an
    // `OrdinalIgnoreCase` comparer matches without re-casing the input,
    // so each MatchKeyword call avoids the previous `text.ToUpperInvariant()`
    // allocation.
    private static readonly FrozenDictionary<string, AqlTokenKind> s_keywords =
        new Dictionary<string, AqlTokenKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["SELECT"] = AqlTokenKind.Select,
            ["FROM"] = AqlTokenKind.From,
            ["WHERE"] = AqlTokenKind.Where,
            ["ORDER"] = AqlTokenKind.Order,
            ["BY"] = AqlTokenKind.By,
            ["LIMIT"] = AqlTokenKind.Limit,
            ["OFFSET"] = AqlTokenKind.Offset,
            ["CONTAINS"] = AqlTokenKind.Contains,
            ["EHR"] = AqlTokenKind.Ehr,
            ["COMPOSITION"] = AqlTokenKind.Composition,
            ["AND"] = AqlTokenKind.And,
            ["OR"] = AqlTokenKind.Or,
            ["NOT"] = AqlTokenKind.Not,
            ["EXISTS"] = AqlTokenKind.Exists,
            ["MATCHES"] = AqlTokenKind.Matches,
            ["LIKE"] = AqlTokenKind.Like,
            ["IS"] = AqlTokenKind.Is,
            ["NULL"] = AqlTokenKind.Null,
            ["TRUE"] = AqlTokenKind.True,
            ["FALSE"] = AqlTokenKind.False,
            ["ASC"] = AqlTokenKind.Asc,
            ["ASCENDING"] = AqlTokenKind.Asc,
            ["DESC"] = AqlTokenKind.Desc,
            ["DESCENDING"] = AqlTokenKind.Desc,
            ["AS"] = AqlTokenKind.As,
            ["DISTINCT"] = AqlTokenKind.Distinct,
            ["TOP"] = AqlTokenKind.Top,
            ["BACKWARD"] = AqlTokenKind.Backward,
            ["FORWARD"] = AqlTokenKind.Forward,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    internal static AqlTokenKind MatchKeyword(string text)
        => s_keywords.TryGetValue(text, out AqlTokenKind kind)
            ? kind
            : AqlTokenKind.EndOfFile;

    // -- Trivia / low-level helpers -------------------------------------------

    private void SkipTrivia()
    {
        while (_pos < _source.Length)
        {
            char c = _source[_pos];
            if (c == ' ' || c == '\t')
            {
                Advance(1);
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
            if (c == '\n')
            {
                AdvanceNewline(1);
                continue;
            }
            // GRAMMAR: spec - '--' line comments through end-of-line.
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

    private AqlToken Emit(AqlTokenKind kind, int start, int length, int line, int column, string? value = null)
    {
        _lastKind = kind;
        ReadOnlySpan<char> span = length == 0 ? [] : _source.Slice(start, length);
        return new AqlToken(kind, span, start, length, line, column, value);
    }

    private AqlToken MakeToken(AqlTokenKind kind, int start, int length, int line, int column)
    {
        ReadOnlySpan<char> span = length == 0 ? [] : _source.Slice(start, length);
        return new AqlToken(kind, span, start, length, line, column);
    }

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

    private readonly char PeekAt(int idx)
        => idx < _source.Length ? _source[idx] : '\0';

    private static bool IsDigit(char c) => c >= '0' && c <= '9';

    private static bool IsIdentStart(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';

    private static bool IsIdentContinue(char c)
        => IsIdentStart(c) || IsDigit(c);

    private AqlLexException NewError(string message, int line, int column)
    {
        _ = _lastKind;
        return new AqlLexException(message, line, column);
    }
}
