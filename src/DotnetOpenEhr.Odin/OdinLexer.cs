using System.Text;

namespace DotnetOpenEhr.Odin;

/// <summary>
/// Hand-written ODIN tokenizer. Operates entirely on
/// <see cref="ReadOnlySpan{T}"/> over the source; allocates only when
/// materialising string / character lexeme contents that contain escape
/// sequences. Tracks 1-based line / column for error reporting.
/// </summary>
/// <remarks>
/// // GRAMMAR: ODIN spec sections 3.1 - 3.5 (lexical), 7.1 (primitive
/// literals). Comments are <c>--</c> through end-of-line. The lexer is
/// context-free; structural disambiguation (e.g. interval vs. block,
/// terminology code vs. hash key) is the parser's job.
/// </remarks>
public ref struct OdinLexer
{
    private readonly ReadOnlySpan<char> _source;
    private int _pos;
    private int _line;
    private int _column;

    public OdinLexer(ReadOnlySpan<char> source)
    {
        _source = source;
        _pos = 0;
        _line = 1;
        _column = 1;
    }

    public int Position => _pos;
    public int Line => _line;
    public int Column => _column;
    public ReadOnlySpan<char> Source => _source;

    /// <summary>
    /// Capture lexer state to rewind on lookahead failures.
    /// </summary>
    public readonly LexerState SaveState() => new(_pos, _line, _column);

    public void RestoreState(LexerState state)
    {
        _pos = state.Position;
        _line = state.Line;
        _column = state.Column;
    }

    public readonly struct LexerState
    {
        public LexerState(int position, int line, int column)
        {
            Position = position;
            Line = line;
            Column = column;
        }

        public int Position { get; }
        public int Line { get; }
        public int Column { get; }
    }

    /// <summary>
    /// Consume and return the next token. Returns an
    /// <see cref="OdinTokenKind.EndOfFile"/> token at the end of input
    /// repeatedly without advancing.
    /// </summary>
    public OdinToken NextToken()
    {
        SkipTrivia();

        if (_pos >= _source.Length)
        {
            return MakeToken(OdinTokenKind.EndOfFile, _pos, 0, _line, _column);
        }

        int startPos = _pos;
        int startLine = _line;
        int startCol = _column;
        char c = _source[_pos];

        // Punctuation and operators.
        switch (c)
        {
            case '<':
                if (PeekAt(_pos + 1) == '=')
                {
                    Advance(2);
                    return MakeToken(OdinTokenKind.LessEqual, startPos, 2, startLine, startCol);
                }
                Advance(1);
                return MakeToken(OdinTokenKind.LeftAngle, startPos, 1, startLine, startCol);
            case '>':
                if (PeekAt(_pos + 1) == '=')
                {
                    Advance(2);
                    return MakeToken(OdinTokenKind.GreaterEqual, startPos, 2, startLine, startCol);
                }
                Advance(1);
                return MakeToken(OdinTokenKind.RightAngle, startPos, 1, startLine, startCol);
            case '(':
                Advance(1);
                return MakeToken(OdinTokenKind.LeftParen, startPos, 1, startLine, startCol);
            case ')':
                Advance(1);
                return MakeToken(OdinTokenKind.RightParen, startPos, 1, startLine, startCol);
            case '[':
                Advance(1);
                return MakeToken(OdinTokenKind.LeftBracket, startPos, 1, startLine, startCol);
            case ']':
                Advance(1);
                return MakeToken(OdinTokenKind.RightBracket, startPos, 1, startLine, startCol);
            case '=':
                Advance(1);
                return MakeToken(OdinTokenKind.Equals, startPos, 1, startLine, startCol);
            case ',':
                Advance(1);
                return MakeToken(OdinTokenKind.Comma, startPos, 1, startLine, startCol);
            case ';':
                Advance(1);
                return MakeToken(OdinTokenKind.Semicolon, startPos, 1, startLine, startCol);
            case '|':
                Advance(1);
                return MakeToken(OdinTokenKind.Pipe, startPos, 1, startLine, startCol);
            case '/':
                Advance(1);
                return MakeToken(OdinTokenKind.Slash, startPos, 1, startLine, startCol);
            case '@':
                Advance(1);
                return MakeToken(OdinTokenKind.AtSign, startPos, 1, startLine, startCol);
            case '\u00b1':
                Advance(1);
                return MakeToken(OdinTokenKind.PlusMinus, startPos, 1, startLine, startCol);
            case '*':
                // GRAMMAR: spec 7.2 - '*' is the unbounded sentinel inside
                // intervals (e.g. |0..*|, |*..5|). The lexer is
                // context-free; the parser rejects '*' outside intervals
                // and rejects the degenerate |*..*| form.
                Advance(1);
                return MakeToken(OdinTokenKind.Star, startPos, 1, startLine, startCol);
            case '+':
                // GRAMMAR: spec 7.2 - interval plus/minus form '+/-'.
                if (PeekAt(_pos + 1) == '/' && PeekAt(_pos + 2) == '-')
                {
                    Advance(3);
                    return MakeToken(OdinTokenKind.PlusMinus, startPos, 3, startLine, startCol);
                }
                throw NewError("Unexpected '+' (only the '+/-' interval form is supported).", startLine, startCol);
            case '.':
                if (PeekAt(_pos + 1) == '.' && PeekAt(_pos + 2) == '.')
                {
                    Advance(3);
                    return MakeToken(OdinTokenKind.Ellipsis, startPos, 3, startLine, startCol);
                }
                if (PeekAt(_pos + 1) == '.')
                {
                    Advance(2);
                    return MakeToken(OdinTokenKind.Range, startPos, 2, startLine, startCol);
                }
                throw NewError("Unexpected '.' (use '..' for interval ranges or '...' for void/continuation).", startLine, startCol);
            case '"':
                return ScanString(startPos, startLine, startCol);
            case '\'':
                return ScanChar(startPos, startLine, startCol);
        }

        // Numeric / date / time / datetime / duration / boolean / null /
        // identifier paths.
        if (c == '-' && _pos + 1 < _source.Length && IsDigit(_source[_pos + 1]))
        {
            return ScanNumericOrDateTime(startPos, startLine, startCol);
        }

        if (IsDigit(c))
        {
            return ScanNumericOrDateTime(startPos, startLine, startCol);
        }

        if (IsIdentStart(c))
        {
            return ScanIdentifierOrKeyword(startPos, startLine, startCol);
        }

        throw NewError($"Unexpected character '{c}'.", startLine, startCol);
    }

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
            // GRAMMAR: spec 3.5 - line comments start with '--' and run
            // to end-of-line.
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

    private OdinToken ScanString(int startPos, int startLine, int startCol)
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
                return new OdinToken(
                    OdinTokenKind.StringLiteral,
                    _source.Slice(startPos, _pos - startPos),
                    startPos,
                    _pos - startPos,
                    startLine,
                    startCol,
                    value);
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

    private OdinToken ScanChar(int startPos, int startLine, int startCol)
    {
        // Consume opening quote.
        Advance(1);
        if (_pos >= _source.Length)
        {
            throw NewError("Unterminated character literal.", startLine, startCol);
        }

        string value;
        if (_source[_pos] == '\\')
        {
            StringBuilder builder = new();
            AppendEscape(builder, startLine, startCol);
            value = builder.ToString();
        }
        else
        {
            value = _source[_pos].ToString();
            Advance(1);
        }

        if (_pos >= _source.Length || _source[_pos] != '\'')
        {
            throw NewError("Expected closing single quote on character literal.", startLine, startCol);
        }
        Advance(1);

        return new OdinToken(
            OdinTokenKind.CharLiteral,
            _source.Slice(startPos, _pos - startPos),
            startPos,
            _pos - startPos,
            startLine,
            startCol,
            value);
    }

    private void AppendEscape(StringBuilder builder, int startLine, int startCol)
    {
        // We arrive here with _source[_pos] == '\\'.
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
            case 'u':
                Advance(1);
                AppendUnicodeEscape(builder, startLine, startCol);
                break;
            default:
                throw NewError($"Invalid escape sequence '\\{esc}'.", startLine, startCol);
        }
    }

    private void AppendUnicodeEscape(StringBuilder builder, int startLine, int startCol)
    {
        // GRAMMAR: spec 3.1 - \uHHHH (4 hex) or \uHHHHHHHH (8 hex).
        int hexStart = _pos;
        int hexLen = 0;
        while (hexLen < 8 && _pos < _source.Length && IsHexDigit(_source[_pos]))
        {
            Advance(1);
            hexLen++;
        }
        if (hexLen != 4 && hexLen != 8)
        {
            throw NewError("Unicode escape requires exactly 4 or 8 hex digits.", startLine, startCol);
        }
        uint codePoint = 0;
        for (int i = 0; i < hexLen; i++)
        {
            codePoint = (codePoint << 4) | HexValue(_source[hexStart + i]);
        }
        if (codePoint > 0x10FFFF)
        {
            throw NewError("Unicode escape exceeds U+10FFFF.", startLine, startCol);
        }
        builder.Append(char.ConvertFromUtf32((int)codePoint));
    }

    private OdinToken ScanNumericOrDateTime(int startPos, int startLine, int startCol)
    {
        // Snapshot for rewind: numeric/date/datetime scanning is greedy
        // but well-defined once we look one or two chars ahead.
        int p = _pos;
        if (_source[p] == '-')
        {
            p++;
        }
        // All numeric forms start with at least one digit.
        int digitStart = p;
        while (p < _source.Length && IsDigit(_source[p]))
        {
            p++;
        }
        int integerDigits = p - digitStart;
        if (integerDigits == 0)
        {
            // Should not happen given guards before calling.
            throw NewError("Expected digit.", startLine, startCol);
        }

        // Date / datetime: ISO 8601 extended form requires 4-digit year
        // followed by '-'. We additionally allow yyyy alone via the
        // partial-form Time/Date rules? Per spec, single-year dates are
        // NOT supported, so a 4-digit integer remains an integer.
        if (integerDigits == 4 && p < _source.Length && _source[p] == '-')
        {
            // Try date.
            int rewind = _pos;
            int rewindLine = _line;
            int rewindCol = _column;
            if (TryScanIsoDate(startPos, startLine, startCol, out OdinToken dateToken))
            {
                // After a date we may have 'T' indicating a date-time.
                if (_pos < _source.Length && _source[_pos] == 'T')
                {
                    // Consume 'T' and continue scanning the time portion.
                    Advance(1);
                    if (TryScanIsoTimeTail(startPos, startLine, startCol, out OdinToken dtToken))
                    {
                        return dtToken;
                    }
                    // Failed to extend to date-time; rewind to date end.
                    _pos = dateToken.Start + dateToken.Length;
                    _line = rewindLine; // approximation; date spans single line
                    _column = rewindCol + dateToken.Length;
                    return dateToken;
                }
                return dateToken;
            }
            _pos = rewind;
            _line = rewindLine;
            _column = rewindCol;
        }

        // Time: hh:mm[:ss[(.|,)fff]][zone]
        if (integerDigits == 2 && p < _source.Length && _source[p] == ':')
        {
            int rewind = _pos;
            int rewindLine = _line;
            int rewindCol = _column;
            if (TryScanIsoTime(startPos, startLine, startCol, out OdinToken timeToken))
            {
                return timeToken;
            }
            _pos = rewind;
            _line = rewindLine;
            _column = rewindCol;
        }

        // Integer or real.
        _pos = p;
        _column += p - startPos;
        bool isReal = false;
        if (_pos < _source.Length && _source[_pos] == '.')
        {
            // Could be Range '..' instead of decimal point.
            if (_pos + 1 < _source.Length && _source[_pos + 1] == '.')
            {
                // Leave the '..' for the next token; numeric ends here.
            }
            else
            {
                isReal = true;
                Advance(1);
                int fracStart = _pos;
                while (_pos < _source.Length && IsDigit(_source[_pos]))
                {
                    Advance(1);
                }
                if (_pos == fracStart)
                {
                    throw NewError("Expected fractional digits after '.'.", startLine, startCol);
                }
            }
        }
        if (_pos < _source.Length && (_source[_pos] == 'e' || _source[_pos] == 'E'))
        {
            Advance(1);
            if (_pos < _source.Length && (_source[_pos] == '+' || _source[_pos] == '-'))
            {
                Advance(1);
            }
            int expStart = _pos;
            while (_pos < _source.Length && IsDigit(_source[_pos]))
            {
                Advance(1);
            }
            if (_pos == expStart)
            {
                throw NewError("Expected digits in numeric exponent.", startLine, startCol);
            }
        }

        ReadOnlySpan<char> lexeme = _source.Slice(startPos, _pos - startPos);
        OdinTokenKind kind = isReal ? OdinTokenKind.RealLiteral : OdinTokenKind.IntegerLiteral;
        return new OdinToken(kind, lexeme, startPos, lexeme.Length, startLine, startCol);
    }

    private bool TryScanIsoDate(int startPos, int startLine, int startCol, out OdinToken token)
    {
        // We've already validated 4 leading digits. _pos still at start.
        // Snapshot.
        int rewindPos = _pos;
        int rewindLine = _line;
        int rewindCol = _column;
        try
        {
            // Year
            Advance(4);
            if (_pos >= _source.Length || _source[_pos] != '-')
            {
                _pos = rewindPos; _line = rewindLine; _column = rewindCol;
                token = default;
                return false;
            }
            Advance(1);
            // Month: two digits or two '?'
            if (!TryAdvancePartialPair())
            {
                _pos = rewindPos; _line = rewindLine; _column = rewindCol;
                token = default;
                return false;
            }
            // Optional day.
            if (_pos < _source.Length && _source[_pos] == '-')
            {
                Advance(1);
                if (!TryAdvancePartialPair())
                {
                    _pos = rewindPos; _line = rewindLine; _column = rewindCol;
                    token = default;
                    return false;
                }
            }
            ReadOnlySpan<char> span = _source.Slice(startPos, _pos - startPos);
            token = new OdinToken(OdinTokenKind.DateLiteral, span, startPos, span.Length, startLine, startCol);
            return true;
        }
        catch
        {
            _pos = rewindPos; _line = rewindLine; _column = rewindCol;
            token = default;
            return false;
        }
    }

    private bool TryScanIsoTime(int startPos, int startLine, int startCol, out OdinToken token)
    {
        int rewindPos = _pos;
        int rewindLine = _line;
        int rewindCol = _column;
        // Two-digit hour already accounted for in the calling logic.
        Advance(2);
        if (_pos >= _source.Length || _source[_pos] != ':')
        {
            _pos = rewindPos; _line = rewindLine; _column = rewindCol;
            token = default;
            return false;
        }
        Advance(1);
        if (!TryAdvancePartialPair())
        {
            _pos = rewindPos; _line = rewindLine; _column = rewindCol;
            token = default;
            return false;
        }
        if (_pos < _source.Length && _source[_pos] == ':')
        {
            Advance(1);
            if (!TryAdvancePartialPair())
            {
                _pos = rewindPos; _line = rewindLine; _column = rewindCol;
                token = default;
                return false;
            }
            // Optional fractional seconds (',' or '.'). Only treat as
            // fractional separator when a digit actually follows; ','
            // can otherwise be a list separator (e.g. <08:30:00, 09:30:00>).
            if (_pos < _source.Length && (_source[_pos] == '.' || _source[_pos] == ',')
                && _pos + 1 < _source.Length && IsDigit(_source[_pos + 1]))
            {
                Advance(1);
                int fStart = _pos;
                while (_pos < _source.Length && IsDigit(_source[_pos]))
                {
                    Advance(1);
                }
                if (_pos == fStart)
                {
                    _pos = rewindPos; _line = rewindLine; _column = rewindCol;
                    token = default;
                    return false;
                }
            }
        }
        // Optional timezone Z or ±HH:MM or ±HHMM.
        if (_pos < _source.Length && (_source[_pos] == 'Z' || _source[_pos] == 'z'))
        {
            Advance(1);
        }
        else if (_pos < _source.Length && (_source[_pos] == '+' || _source[_pos] == '-'))
        {
            Advance(1);
            if (!TryAdvanceDigits(2))
            {
                _pos = rewindPos; _line = rewindLine; _column = rewindCol;
                token = default;
                return false;
            }
            if (_pos < _source.Length && _source[_pos] == ':')
            {
                Advance(1);
            }
            if (!TryAdvanceDigits(2))
            {
                _pos = rewindPos; _line = rewindLine; _column = rewindCol;
                token = default;
                return false;
            }
        }
        ReadOnlySpan<char> span = _source.Slice(startPos, _pos - startPos);
        token = new OdinToken(OdinTokenKind.TimeLiteral, span, startPos, span.Length, startLine, startCol);
        return true;
    }

    private bool TryScanIsoTimeTail(int startPos, int startLine, int startCol, out OdinToken token)
    {
        // We've consumed the date and the 'T' separator; now scan time.
        int rewindPos = _pos;
        int rewindLine = _line;
        int rewindCol = _column;
        if (_pos + 1 >= _source.Length || !IsDigit(_source[_pos]) || !IsDigit(_source[_pos + 1]))
        {
            _pos = rewindPos; _line = rewindLine; _column = rewindCol;
            token = default;
            return false;
        }
        Advance(2);
        if (_pos >= _source.Length || _source[_pos] != ':')
        {
            // ODIN partial date-time with hour-only is NOT supported by
            // spec 7.1.6.2; consider this a failure.
            _pos = rewindPos; _line = rewindLine; _column = rewindCol;
            token = default;
            return false;
        }
        Advance(1);
        if (!TryAdvancePartialPair())
        {
            _pos = rewindPos; _line = rewindLine; _column = rewindCol;
            token = default;
            return false;
        }
        if (_pos < _source.Length && _source[_pos] == ':')
        {
            Advance(1);
            if (!TryAdvancePartialPair())
            {
                _pos = rewindPos; _line = rewindLine; _column = rewindCol;
                token = default;
                return false;
            }
            if (_pos < _source.Length && (_source[_pos] == '.' || _source[_pos] == ',')
                && _pos + 1 < _source.Length && IsDigit(_source[_pos + 1]))
            {
                Advance(1);
                int fStart = _pos;
                while (_pos < _source.Length && IsDigit(_source[_pos]))
                {
                    Advance(1);
                }
                if (_pos == fStart)
                {
                    _pos = rewindPos; _line = rewindLine; _column = rewindCol;
                    token = default;
                    return false;
                }
            }
        }
        if (_pos < _source.Length && (_source[_pos] == 'Z' || _source[_pos] == 'z'))
        {
            Advance(1);
        }
        else if (_pos < _source.Length && (_source[_pos] == '+' || _source[_pos] == '-'))
        {
            Advance(1);
            if (!TryAdvanceDigits(2))
            {
                _pos = rewindPos; _line = rewindLine; _column = rewindCol;
                token = default;
                return false;
            }
            if (_pos < _source.Length && _source[_pos] == ':')
            {
                Advance(1);
            }
            if (!TryAdvanceDigits(2))
            {
                _pos = rewindPos; _line = rewindLine; _column = rewindCol;
                token = default;
                return false;
            }
        }
        ReadOnlySpan<char> span = _source.Slice(startPos, _pos - startPos);
        token = new OdinToken(OdinTokenKind.DateTimeLiteral, span, startPos, span.Length, startLine, startCol);
        return true;
    }

    private bool TryAdvancePartialPair()
    {
        if (_pos + 1 >= _source.Length) return false;
        char a = _source[_pos];
        char b = _source[_pos + 1];
        if ((IsDigit(a) && IsDigit(b)) || (a == '?' && b == '?'))
        {
            Advance(2);
            return true;
        }
        return false;
    }

    private bool TryAdvanceDigits(int count)
    {
        if (_pos + count > _source.Length) return false;
        for (int i = 0; i < count; i++)
        {
            if (!IsDigit(_source[_pos + i])) return false;
        }
        Advance(count);
        return true;
    }

    private OdinToken ScanIdentifierOrKeyword(int startPos, int startLine, int startCol)
    {
        // Identifier := letter/underscore { letter | digit | '_' }
        // Note: hyphens are not in the spec identifier grammar; the lexer
        // does not include them. They appear in terminology ids and are
        // handled by the parser when reading bracketed code bodies raw.
        Advance(1);
        while (_pos < _source.Length && IsIdentContinue(_source[_pos]))
        {
            Advance(1);
        }

        ReadOnlySpan<char> span = _source.Slice(startPos, _pos - startPos);

        // Duration: starts with 'P' followed by digits / 'T' / period
        // designators. Detect by examining the lexeme.
        if (IsDurationLexeme(span))
        {
            return new OdinToken(OdinTokenKind.DurationLiteral, span, startPos, span.Length, startLine, startCol);
        }

        // Boolean / null literals (case-insensitive per spec 7.1.5).
        if (span.Equals("True", StringComparison.OrdinalIgnoreCase) ||
            span.Equals("False", StringComparison.OrdinalIgnoreCase))
        {
            return new OdinToken(OdinTokenKind.BooleanLiteral, span, startPos, span.Length, startLine, startCol);
        }

        return new OdinToken(OdinTokenKind.Identifier, span, startPos, span.Length, startLine, startCol);
    }

    private static bool IsDurationLexeme(ReadOnlySpan<char> span)
    {
        // GRAMMAR: spec 7.1.6 - Pn[Yn][Mn][Wn][Dn][Tn[Hn][Mn][Sn]]
        // Single-char 'P' is reserved as a boolean-like? No: 'P' alone is
        // not legal; durations always have at least one designator.
        if (span.Length < 2 || span[0] != 'P') return false;
        bool sawAny = false;
        bool inTime = false;
        for (int i = 1; i < span.Length; i++)
        {
            char c = span[i];
            if (c == 'T')
            {
                if (inTime) return false;
                inTime = true;
                continue;
            }
            // Designator preceded by digits.
            int digitStart = i;
            while (i < span.Length && (IsDigit(span[i]) || span[i] == '.' || span[i] == ','))
            {
                i++;
            }
            if (i == digitStart) return false;
            if (i >= span.Length) return false;
            char designator = span[i];
            if (inTime)
            {
                if (designator != 'H' && designator != 'M' && designator != 'S') return false;
            }
            else
            {
                if (designator != 'Y' && designator != 'M' && designator != 'W' && designator != 'D') return false;
            }
            sawAny = true;
        }
        return sawAny;
    }

    private OdinToken MakeToken(OdinTokenKind kind, int start, int length, int line, int column)
    {
        ReadOnlySpan<char> span = length == 0
            ? []
            : _source.Slice(start, length);
        return new OdinToken(kind, span, start, length, line, column);
    }

    private void Advance(int count)
    {
        // L2 — no caller crosses a newline (newlines are routed through
        // AdvanceNewline). The previous per-character loop was equivalent
        // to a pair of additions.
        _pos += count;
        _column += count;
    }

    private void AdvanceNewline(int count)
    {
        _pos += count;
        _line++;
        _column = 1;
    }

    private readonly char PeekAt(int index)
        => index < _source.Length ? _source[index] : '\0';

    private static bool IsDigit(char c) => c >= '0' && c <= '9';

    private static bool IsHexDigit(char c)
        => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private static uint HexValue(char c)
    {
        if (c >= '0' && c <= '9') return (uint)(c - '0');
        if (c >= 'a' && c <= 'f') return (uint)(c - 'a' + 10);
        return (uint)(c - 'A' + 10);
    }

    private static bool IsIdentStart(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';

    private static bool IsIdentContinue(char c)
        => IsIdentStart(c) || IsDigit(c);

    private readonly OdinParseException NewError(string message, int line, int column)
        => new(message, line, column, BuildSnippet(_source, line, column));

    /// <summary>
    /// Returns up to <paramref name="maxLen"/> characters of source
    /// starting at 1-based (<paramref name="line"/>,
    /// <paramref name="column"/>), CR/LF-escaped, so the
    /// <c>(near '…')</c> suffix in <see cref="OdinParseException"/>
    /// fires for lexer-thrown errors. Returns the empty string when
    /// the position is out of range.
    /// </summary>
    private static string BuildSnippet(ReadOnlySpan<char> src, int line, int column, int maxLen = 24)
    {
        if (line <= 0 || column <= 0)
        {
            return string.Empty;
        }
        int offset = 0;
        int currentLine = 1;
        while (currentLine < line && offset < src.Length)
        {
            char c = src[offset];
            if (c == '\r')
            {
                currentLine++;
                offset++;
                if (offset < src.Length && src[offset] == '\n')
                {
                    offset++;
                }
            }
            else if (c == '\n')
            {
                currentLine++;
                offset++;
            }
            else
            {
                offset++;
            }
        }
        if (currentLine != line)
        {
            return string.Empty;
        }
        offset += column - 1;
        if (offset < 0 || offset >= src.Length)
        {
            return string.Empty;
        }
        int take = Math.Min(maxLen, src.Length - offset);
        ReadOnlySpan<char> slice = src.Slice(offset, take);
        if (slice.IndexOfAny('\r', '\n') < 0)
        {
            return slice.ToString();
        }
        StringBuilder sb = new(slice.Length + 4);
        foreach (char ch in slice)
        {
            switch (ch)
            {
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Scan a bracketed terminology code body starting at the current
    /// position (caller has already consumed <c>[</c>). On success,
    /// returns true and advances past the closing <c>]</c>. On failure,
    /// the lexer state is restored to its position prior to the call.
    /// </summary>
    /// <remarks>
    /// // GRAMMAR: spec 7.3.2. terminology_id is alphanumeric + '_' and may
    /// be followed by an optional <c>(version)</c>. The code is a token of
    /// non-bracket characters. We additionally accept the ADL2-style bare
    /// form <c>[at0001]</c> (no <c>::</c>), classifying it as a local code.
    /// SPEC: ambiguity here vs. hash-keys / refs - the parser falls back
    /// to bracket handling if this method fails.
    /// </remarks>
    public bool TryReadTerminologyCodeBody(out string terminologyId, out string codeString, out int endLine, out int endColumn)
    {
        LexerState saved = SaveState();
        terminologyId = string.Empty;
        codeString = string.Empty;
        endLine = _line;
        endColumn = _column;

        int idStart = _pos;
        // Read up to '::' or ']'; allow alphanumerics, '_', '-', '.', '(', ')'.
        while (_pos < _source.Length)
        {
            char c = _source[_pos];
            if (c == ']' || c == '[' || c == '\n' || c == '\r' || c == '"' || c == '\'' || c == '=' || c == ' ' || c == '\t')
            {
                break;
            }
            if (c == ':' && _pos + 1 < _source.Length && _source[_pos + 1] == ':')
            {
                break;
            }
            Advance(1);
        }

        if (_pos >= _source.Length)
        {
            RestoreState(saved);
            return false;
        }

        ReadOnlySpan<char> idSpan = _source.Slice(idStart, _pos - idStart);
        if (idSpan.IsEmpty || !IsValidTerminologyIdOrCode(idSpan))
        {
            RestoreState(saved);
            return false;
        }

        if (_pos < _source.Length && _source[_pos] == ']')
        {
            // Bare form [code].
            Advance(1);
            terminologyId = Values.OdinTerminologyCode.LocalTerminologyId;
            codeString = idSpan.ToString();
            endLine = _line;
            endColumn = _column;
            return true;
        }

        // Expect '::'
        if (_pos + 1 >= _source.Length || _source[_pos] != ':' || _source[_pos + 1] != ':')
        {
            RestoreState(saved);
            return false;
        }
        Advance(2);

        int codeStart = _pos;
        while (_pos < _source.Length && _source[_pos] != ']' && _source[_pos] != '\n' && _source[_pos] != '\r')
        {
            Advance(1);
        }
        if (_pos >= _source.Length || _source[_pos] != ']')
        {
            RestoreState(saved);
            return false;
        }
        ReadOnlySpan<char> codeSpan = _source.Slice(codeStart, _pos - codeStart);
        if (codeSpan.IsEmpty)
        {
            RestoreState(saved);
            return false;
        }
        Advance(1); // consume ']'
        terminologyId = idSpan.ToString();
        codeString = codeSpan.ToString();
        endLine = _line;
        endColumn = _column;
        return true;
    }

    private static bool IsValidTerminologyIdOrCode(ReadOnlySpan<char> span)
    {
        if (!IsIdentStart(span[0]) && !IsDigit(span[0])) return false;
        for (int i = 1; i < span.Length; i++)
        {
            char c = span[i];
            if (!(IsIdentContinue(c) || c == '-' || c == '.' || c == '(' || c == ')'))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Scan an arbitrary type-marker body between matching parentheses.
    /// Caller has already consumed the opening '('. Returns the raw text
    /// inside the parens and advances past the closing ')'.
    /// </summary>
    /// <remarks>
    /// // GRAMMAR: spec 5.6 - type markers may include qualified type
    /// names with '.' separators and generic '&lt; &gt;' brackets. We
    /// read raw chars and track nested parens / angles so we stop at the
    /// matching ')'.
    /// </remarks>
    public string ReadTypeMarkerBody()
    {
        int parenStartLine = _line;
        int parenStartCol = _column;
        int start = _pos;
        int parenDepth = 1;
        int angleDepth = 0;
        while (_pos < _source.Length)
        {
            char c = _source[_pos];
            if (c == '(')
            {
                parenDepth++;
                Advance(1);
            }
            else if (c == ')')
            {
                parenDepth--;
                if (parenDepth == 0)
                {
                    string body = _source.Slice(start, _pos - start).ToString();
                    Advance(1);
                    return body;
                }
                Advance(1);
            }
            else if (c == '<')
            {
                angleDepth++;
                Advance(1);
            }
            else if (c == '>')
            {
                if (angleDepth > 0) angleDepth--;
                Advance(1);
            }
            else if (c == '\n')
            {
                AdvanceNewline(1);
            }
            else if (c == '\r')
            {
                if (_pos + 1 < _source.Length && _source[_pos + 1] == '\n')
                {
                    AdvanceNewline(2);
                }
                else
                {
                    AdvanceNewline(1);
                }
            }
            else
            {
                Advance(1);
            }
        }
        throw NewError("Unterminated type marker.", parenStartLine, parenStartCol);
    }
}
