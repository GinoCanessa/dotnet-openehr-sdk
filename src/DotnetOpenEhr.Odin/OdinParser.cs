using System.Globalization;
using DotnetOpenEhr.Foundation;
using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Odin.Values;

namespace DotnetOpenEhr.Odin;

/// <summary>
/// Recursive-descent ODIN parser. The static entrypoints accept either
/// a <see cref="string"/> or a <see cref="ReadOnlySpan{T}"/> over the
/// source text and return the parsed <see cref="OdinValue"/> tree.
/// </summary>
/// <remarks>
/// // GRAMMAR: top-level dispatch follows ODIN spec section 4.2 -
/// implicit / anonymous / identified object documents. Inner blocks
/// follow sections 5.1 - 5.6 (attribute object, container/hash, type
/// markers) and section 7 (leaf data).
/// </remarks>
public static class OdinParser
{
    public static OdinValue Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Parse(source.AsSpan());
    }

    public static OdinValue Parse(ReadOnlySpan<char> source)
    {
        OdinLexer lexer = new(source);
        ParserState state = new(lexer);
        OdinValue result = ParseDocument(ref state);
        state.ExpectEof();
        return result;
    }

    /// <summary>
    /// Parser state. Wraps a ref lexer plus a one-token lookahead buffer.
    /// Passed by ref through all parse_* methods.
    /// </summary>
    private ref struct ParserState
    {
        // Lexer is held as a value field (mutating its position works
        // because ParserState is itself passed by ref through all
        // parse methods).
        public OdinLexer Lexer;

        // Buffered next token (one-token lookahead).
        public OdinTokenSnapshot Current;
        public bool HasCurrent;

        public ParserState(OdinLexer lexer)
        {
            Lexer = lexer;
            Current = default;
            HasCurrent = false;
        }

        public OdinTokenSnapshot Peek()
        {
            if (!HasCurrent)
            {
                Current = OdinTokenSnapshot.From(Lexer.NextToken());
                HasCurrent = true;
            }
            return Current;
        }

        public OdinTokenSnapshot Consume()
        {
            OdinTokenSnapshot t = Peek();
            HasCurrent = false;
            return t;
        }

        public void Expect(OdinTokenKind kind, string what)
        {
            OdinTokenSnapshot t = Peek();
            if (t.Kind != kind)
            {
                throw new OdinParseException(
                    $"Expected {what} ({kind}) but found {t.Kind} '{Truncate(t.Text)}'.",
                    t.Line,
                    t.Column,
                    Truncate(t.Text));
            }
            HasCurrent = false;
        }

        public void ExpectEof()
        {
            OdinTokenSnapshot t = Peek();
            if (t.Kind != OdinTokenKind.EndOfFile)
            {
                throw new OdinParseException(
                    $"Unexpected trailing content: {t.Kind} '{Truncate(t.Text)}'.",
                    t.Line,
                    t.Column,
                    Truncate(t.Text));
            }
        }

        private static string Truncate(string s)
            => s.Length > 32 ? s.Substring(0, 32) + "…" : s;
    }

    /// <summary>
    /// Non-ref snapshot of an <see cref="OdinToken"/> so we can buffer
    /// one for lookahead without holding a ReadOnlySpan field.
    /// </summary>
    private readonly struct OdinTokenSnapshot
    {
        public OdinTokenSnapshot(OdinTokenKind kind, string text, int line, int column)
        {
            Kind = kind;
            Text = text;
            Line = line;
            Column = column;
        }

        public OdinTokenKind Kind { get; }
        public string Text { get; }
        public int Line { get; }
        public int Column { get; }

        public static OdinTokenSnapshot From(OdinToken token)
            => new(token.Kind, token.Text, token.Line, token.Column);
    }

    private static OdinValue ParseDocument(ref ParserState state)
    {
        // GRAMMAR: spec 4.1 / 4.2.1 - embedded fragment / implicit object
        // document. spec 4.2.2 - anonymous object. spec 4.2.3 - identified
        // object document.
        OdinTokenSnapshot first = state.Peek();
        if (first.Kind == OdinTokenKind.EndOfFile)
        {
            return new OdinObject();
        }
        if (first.Kind == OdinTokenKind.AtSign)
        {
            // SPEC: schema_identifier ::= '@' schema '=' URI ; we accept
            // and skip it. Phase 5 does not interpret schemas.
            state.Consume();
            OdinTokenSnapshot ident = state.Peek();
            if (ident.Kind != OdinTokenKind.Identifier)
            {
                throw new OdinParseException("Expected schema identifier after '@'.", ident.Line, ident.Column);
            }
            state.Consume();
            state.Expect(OdinTokenKind.Equals, "'='");
            // The URI is hard to tokenise; skip everything until newline by
            // collecting Identifier/Slash/Colon tokens isn't viable from
            // here. Bail out instead: declare unsupported for Phase 5.
            throw new OdinParseException(
                "Schema identifier headers are not supported by the Phase 5 ODIN parser.",
                ident.Line,
                ident.Column);
        }
        if (first.Kind == OdinTokenKind.LeftAngle)
        {
            // Anonymous object document: < ... >
            state.Consume();
            OdinValue inner = ParseBlockContents(ref state);
            state.Expect(OdinTokenKind.RightAngle, "'>'");
            return inner;
        }
        if (first.Kind == OdinTokenKind.LeftBracket)
        {
            // Identified object document: [key] = <...> [key] = <...> ...
            // Same parsing logic as a hash body but at top level.
            return ParseHashBody(ref state, requireBracket: true);
        }
        // Implicit object document: attribute_name '=' ... repeated.
        return ParseAttributeBody(ref state);
    }

    /// <summary>
    /// Parse the contents of a block (between '&lt;' and '&gt;'). Decides
    /// between void, scalar, list, interval, attribute object, or hash
    /// container based on lookahead.
    /// </summary>
    private static OdinValue ParseBlockContents(ref ParserState state)
    {
        OdinTokenSnapshot t = state.Peek();
        switch (t.Kind)
        {
            case OdinTokenKind.RightAngle:
                // Empty block "<>" - treat as empty object.
                return new OdinObject();
            case OdinTokenKind.Ellipsis:
                state.Consume();
                return OdinValue.Null;
            case OdinTokenKind.Pipe:
                return ParseInterval(ref state);
            case OdinTokenKind.LeftBracket:
                // Could be hash key ([key]=<...>) or terminology code
                // value ([id::code]). Distinguish by trying terminology
                // code scan first.
                {
                    OdinValue bracketed = ParseBracketStart(ref state);
                    // Only terminology codes can appear as list elements
                    // joined by ','; hashes never list-continue.
                    if (bracketed is OdinTerminologyCode)
                    {
                        return MaybeContinueAsList(ref state, bracketed);
                    }
                    return bracketed;
                }
            case OdinTokenKind.Identifier:
                {
                    // Attribute object iff next token after identifier is
                    // '='; else this is a bare identifier value (rare in
                    // ODIN; treat as a string scalar via the spec's URI
                    // / coded-term forms which we don't fully handle).
                    // Look at the second token by saving / restoring.
                    state.Consume();
                    OdinTokenSnapshot next = state.Peek();
                    if (next.Kind == OdinTokenKind.Equals)
                    {
                        // Rewind: we've consumed the identifier but the
                        // ParserState only buffers one token. We'll
                        // reconstruct attribute parsing by pushing the
                        // identifier into a deferred call.
                        return ParseAttributeBodyStartingWith(ref state, t);
                    }
                    throw new OdinParseException(
                        $"Unexpected identifier '{t.Text}'; expected '=' to start an attribute.",
                        t.Line,
                        t.Column);
                }
            default:
                return ParseLeafOrListStartingAt(ref state, t);
        }
    }

    private static OdinValue ParseBracketStart(ref ParserState state)
    {
        // Entry contract: state.Current is the '[' (HasCurrent == true).
        // The lexer's position is past the '[' already. We need to look at
        // what comes after '[' to decide whether this is a terminology
        // code value, a list of terminology codes, or a hash-key bracket.
        OdinTokenSnapshot openBracket = state.Consume();
        OdinLexer.LexerState afterOpen = state.Lexer.SaveState();

        // Quick check: a quoted key ('"' or "'") immediately after '['
        // can only be a hash-key bracket.
        int peekPos = afterOpen.Position;
        ReadOnlySpan<char> src = state.Lexer.Source;
        if (peekPos < src.Length && (src[peekPos] == '"' || src[peekPos] == '\''))
        {
            return ParseHashEntriesAfterOpenBracket(ref state, openBracket);
        }

        // Try terminology-code body. Lexer is right after '['.
        if (state.Lexer.TryReadTerminologyCodeBody(out string id, out string code, out _, out _))
        {
            // Look at next token: if it's '=', this was actually a hash
            // key with no '::' (bare identifier inside brackets); rewind
            // to right after '[' and parse as a hash.
            OdinLexer.LexerState afterCode = state.Lexer.SaveState();
            OdinToken next = state.Lexer.NextToken();
            if (next.Kind == OdinTokenKind.Equals)
            {
                state.Lexer.RestoreState(afterOpen);
                state.HasCurrent = false;
                return ParseHashEntriesAfterOpenBracket(ref state, openBracket);
            }
            // Not a hash key; this is a terminology code value. Restore
            // position to right after ']' so list-continuation parsing
            // (looking for ',') sees the right tokens.
            state.Lexer.RestoreState(afterCode);
            state.HasCurrent = false;
            return new OdinTerminologyCode(new TerminologyCode(id, code));
        }

        // Code-body scan failed. Treat as a hash key bracket; lexer needs
        // to point right after '[' for ParseHashEntriesAfterOpenBracket.
        state.Lexer.RestoreState(afterOpen);
        state.HasCurrent = false;
        return ParseHashEntriesAfterOpenBracket(ref state, openBracket);
    }

    private static OdinValue MaybeContinueAsList(ref ParserState state, OdinValue head)
    {
        if (state.Peek().Kind != OdinTokenKind.Comma) return head;
        List<OdinValue> items = [head];
        bool continuation = false;
        while (state.Peek().Kind == OdinTokenKind.Comma)
        {
            state.Consume();
            OdinTokenSnapshot t = state.Peek();
            if (t.Kind == OdinTokenKind.Ellipsis)
            {
                state.Consume();
                continuation = true;
                break;
            }
            OdinValue item = t.Kind == OdinTokenKind.LeftBracket
                ? ParseBracketStart(ref state)
                : ParseLeafToken(ref state, t);
            items.Add(item);
        }
        return new OdinList(items) { HasContinuationMarker = continuation };
    }

    /// <summary>
    /// Parse hash entries when the caller has already consumed the first
    /// '['. Reads the first key + value, then continues with normal
    /// '[' key ']' '=' value loop.
    /// </summary>
    private static OdinValue ParseHashEntriesAfterOpenBracket(ref ParserState state, OdinTokenSnapshot openBracket)
    {
        Dictionary<string, OdinValue> entries = new(StringComparer.Ordinal);
        OdinKind keyKind = OdinKind.String;

        OdinTokenSnapshot keyToken = state.Peek();
        (string firstKey, OdinKind firstKeyKind) = ConsumeKey(ref state, keyToken);
        keyKind = firstKeyKind;
        state.Expect(OdinTokenKind.RightBracket, "']'");
        state.Expect(OdinTokenKind.Equals, "'='");
        entries[firstKey] = ParseTypedBlock(ref state);
        if (state.Peek().Kind == OdinTokenKind.Semicolon)
        {
            state.Consume();
        }

        // Continue reading subsequent entries.
        while (state.Peek().Kind == OdinTokenKind.LeftBracket)
        {
            state.Consume();
            OdinTokenSnapshot k = state.Peek();
            (string keyText, _) = ConsumeKey(ref state, k);
            state.Expect(OdinTokenKind.RightBracket, "']'");
            if (entries.ContainsKey(keyText))
            {
                throw new OdinParseException(
                    $"Duplicate hash key '{keyText}' (validity rule VDOBU).",
                    k.Line,
                    k.Column);
            }
            state.Expect(OdinTokenKind.Equals, "'='");
            entries[keyText] = ParseTypedBlock(ref state);
            if (state.Peek().Kind == OdinTokenKind.Semicolon)
            {
                state.Consume();
            }
        }

        _ = openBracket; // retained for future error-message use
        return new OdinHash(entries) { KeyKind = keyKind };
    }

    private static OdinValue ParseInterval(ref ParserState state)
    {
        // GRAMMAR: spec 7.2 - interval forms inside |...|.
        OdinTokenSnapshot openPipe = state.Consume();
        OdinTokenSnapshot t = state.Peek();

        OdinValue? lower = null;
        OdinValue? upper = null;
        bool upperIncluded = true;

        if (t.Kind == OdinTokenKind.GreaterEqual)
        {
            state.Consume();
            lower = ParseIntervalEndpoint(ref state);
            // Allow optional explicit unbounded upper: |>=N..*|.
            OdinTokenSnapshot afterLower = state.Peek();
            if (afterLower.Kind == OdinTokenKind.Range)
            {
                state.Consume();
                ExpectStar(ref state);
                ExpectPipe(ref state, openPipe);
                return new OdinInterval(lower, true, null, true);
            }
            ExpectPipe(ref state, openPipe);
            return new OdinInterval(lower, true, null, true);
        }
        if (t.Kind == OdinTokenKind.LessEqual)
        {
            state.Consume();
            upper = ParseIntervalEndpoint(ref state);
            ExpectPipe(ref state, openPipe);
            return new OdinInterval(null, true, upper, true);
        }
        if (t.Kind == OdinTokenKind.LeftAngle)
        {
            // |<N|
            state.Consume();
            upper = ParseIntervalEndpoint(ref state);
            ExpectPipe(ref state, openPipe);
            return new OdinInterval(null, true, upper, false);
        }
        if (t.Kind == OdinTokenKind.Star)
        {
            // |*..M| - unbounded lower, finite upper.
            state.Consume();
            OdinTokenSnapshot afterStar = state.Peek();
            if (afterStar.Kind != OdinTokenKind.Range)
            {
                throw new OdinParseException(
                    $"Expected '..' after '*' in interval; found {afterStar.Kind} '{afterStar.Text}'.",
                    afterStar.Line,
                    afterStar.Column);
            }
            state.Consume();
            OdinTokenSnapshot beforeUpper = state.Peek();
            if (beforeUpper.Kind == OdinTokenKind.Star)
            {
                throw new OdinParseException(
                    "Interval '|*..*|' is not valid: at least one bound must be finite.",
                    beforeUpper.Line,
                    beforeUpper.Column);
            }
            if (beforeUpper.Kind == OdinTokenKind.LeftAngle)
            {
                state.Consume();
                upperIncluded = false;
            }
            upper = ParseIntervalEndpoint(ref state);
            ExpectPipe(ref state, openPipe);
            return new OdinInterval(null, true, upper, upperIncluded);
        }

        bool hasLowerOpenMarker = false;
        if (t.Kind == OdinTokenKind.RightAngle)
        {
            // |>N..M| or |>N|
            state.Consume();
            hasLowerOpenMarker = true;
        }

        // Parse first endpoint.
        lower = ParseIntervalEndpoint(ref state);

        // Now expect either Range '..' or '|' (single-sided), or
        // PlusMinus (|N ±M|).
        OdinTokenSnapshot afterFirst = state.Peek();
        if (afterFirst.Kind == OdinTokenKind.Pipe)
        {
            // |>N| (open lower) or |N| (point, treated as included
            // single-sided lower).
            state.Consume();
            return new OdinInterval(lower, !hasLowerOpenMarker, null, true);
        }
        if (afterFirst.Kind == OdinTokenKind.PlusMinus)
        {
            state.Consume();
            OdinValue delta = ParseIntervalEndpoint(ref state);
            ExpectPipe(ref state, openPipe);
            OdinValue lo = ApplyPlusMinus(lower, delta, subtract: true);
            OdinValue hi = ApplyPlusMinus(lower, delta, subtract: false);
            return new OdinInterval(lo, true, hi, true);
        }
        if (afterFirst.Kind != OdinTokenKind.Range)
        {
            throw new OdinParseException(
                $"Expected '..' or '|' inside interval, found {afterFirst.Kind} '{afterFirst.Text}'.",
                afterFirst.Line,
                afterFirst.Column);
        }
        state.Consume();
        OdinTokenSnapshot beforeUpper2 = state.Peek();
        if (beforeUpper2.Kind == OdinTokenKind.Star)
        {
            // |N..*| - finite lower, unbounded upper.
            state.Consume();
            ExpectPipe(ref state, openPipe);
            return new OdinInterval(lower, !hasLowerOpenMarker, null, true);
        }
        if (beforeUpper2.Kind == OdinTokenKind.LeftAngle)
        {
            state.Consume();
            upperIncluded = false;
        }
        upper = ParseIntervalEndpoint(ref state);
        ExpectPipe(ref state, openPipe);
        return new OdinInterval(lower, !hasLowerOpenMarker, upper, upperIncluded);
    }

    private static void ExpectStar(ref ParserState state)
    {
        OdinTokenSnapshot t = state.Peek();
        if (t.Kind != OdinTokenKind.Star)
        {
            throw new OdinParseException(
                $"Expected '*' as unbounded upper sentinel; found {t.Kind} '{t.Text}'.",
                t.Line,
                t.Column);
        }
        state.Consume();
    }

    private static void ExpectPipe(ref ParserState state, OdinTokenSnapshot openPipe)
    {
        OdinTokenSnapshot t = state.Peek();
        if (t.Kind != OdinTokenKind.Pipe)
        {
            throw new OdinParseException(
                $"Expected closing '|' for interval opened at line {openPipe.Line}; found {t.Kind} '{t.Text}'.",
                t.Line,
                t.Column);
        }
        state.Consume();
    }

    private static OdinValue ApplyPlusMinus(OdinValue? center, OdinValue delta, bool subtract)
    {
        if (center is null)
        {
            throw new InvalidOperationException("Plus/minus interval missing center value.");
        }
        switch (center)
        {
            case OdinInteger ci when delta is OdinInteger di:
                return new OdinInteger(subtract ? ci.Value - di.Value : ci.Value + di.Value);
            case OdinReal cr when delta is OdinReal dr:
                return new OdinReal(subtract ? cr.Value - dr.Value : cr.Value + dr.Value);
            case OdinReal cr2 when delta is OdinInteger di2:
                return new OdinReal(subtract ? cr2.Value - di2.Value : cr2.Value + di2.Value);
            case OdinInteger ci2 when delta is OdinReal dr2:
                return new OdinReal(subtract ? ci2.Value - dr2.Value : ci2.Value + dr2.Value);
            default:
                throw new InvalidOperationException(
                    $"Plus/minus interval not supported for {center.Kind} ± {delta.Kind}.");
        }
    }

    private static OdinValue ParseIntervalEndpoint(ref ParserState state)
    {
        OdinTokenSnapshot t = state.Peek();
        return ParseLeafToken(ref state, t);
    }

    private static OdinValue ParseAttributeBody(ref ParserState state)
    {
        OdinTokenSnapshot t = state.Peek();
        if (t.Kind != OdinTokenKind.Identifier)
        {
            throw new OdinParseException(
                $"Expected attribute name; found {t.Kind} '{t.Text}'.",
                t.Line,
                t.Column);
        }
        state.Consume();
        return ParseAttributeBodyStartingWith(ref state, t);
    }

    /// <summary>
    /// We've already consumed the first attribute name <paramref name="first"/>.
    /// Continue parsing the rest of the attributes.
    /// </summary>
    private static OdinValue ParseAttributeBodyStartingWith(ref ParserState state, OdinTokenSnapshot first)
    {
        Dictionary<string, OdinValue> attrs = new(StringComparer.Ordinal);
        ParseAttribute(ref state, first, attrs);
        while (true)
        {
            OdinTokenSnapshot t = state.Peek();
            if (t.Kind == OdinTokenKind.Semicolon)
            {
                state.Consume();
                continue;
            }
            if (t.Kind == OdinTokenKind.Identifier)
            {
                state.Consume();
                ParseAttribute(ref state, t, attrs);
                continue;
            }
            break;
        }
        return new OdinObject(attrs);
    }

    private static void ParseAttribute(ref ParserState state, OdinTokenSnapshot name, Dictionary<string, OdinValue> attrs)
    {
        if (attrs.ContainsKey(name.Text))
        {
            throw new OdinParseException(
                $"Duplicate attribute '{name.Text}' (validity rule VDATU).",
                name.Line,
                name.Column);
        }
        state.Expect(OdinTokenKind.Equals, "'='");
        OdinValue value = ParseTypedBlock(ref state);
        attrs[name.Text] = value;
    }

    private static OdinValue ParseTypedBlock(ref ParserState state)
    {
        string? typeMarker = TryReadTypeMarker(ref state);
        OdinTokenSnapshot t = state.Peek();
        if (t.Kind != OdinTokenKind.LeftAngle)
        {
            throw new OdinParseException(
                $"Expected '<' to open a block; found {t.Kind} '{t.Text}'.",
                t.Line,
                t.Column);
        }
        state.Consume();
        OdinValue inner = ParseBlockContents(ref state);
        OdinTokenSnapshot close = state.Peek();
        if (close.Kind != OdinTokenKind.RightAngle)
        {
            throw new OdinParseException(
                $"Expected '>' to close block; found {close.Kind} '{close.Text}'.",
                close.Line,
                close.Column);
        }
        state.Consume();
        if (typeMarker is not null)
        {
            inner.TypeMarker = typeMarker;
        }
        return inner;
    }

    private static string? TryReadTypeMarker(ref ParserState state)
    {
        OdinTokenSnapshot t = state.Peek();
        if (t.Kind != OdinTokenKind.LeftParen) return null;
        // Drop the buffered '(' and ask the lexer to read raw to ')'.
        state.HasCurrent = false;
        // We need to consume the '(' that's in the lexer. Since the
        // buffered token was the '(', the lexer has already advanced
        // past it - so call ReadTypeMarkerBody directly.
        return state.Lexer.ReadTypeMarkerBody();
    }

    private static OdinValue ParseHashBody(ref ParserState state, bool requireBracket)
    {
        Dictionary<string, OdinValue> entries = new(StringComparer.Ordinal);
        OdinKind keyKind = OdinKind.String;
        bool keyKindSet = false;

        while (true)
        {
            OdinTokenSnapshot t = state.Peek();
            if (t.Kind != OdinTokenKind.LeftBracket)
            {
                if (requireBracket && entries.Count == 0)
                {
                    throw new OdinParseException(
                        $"Expected '[' to open a hash key; found {t.Kind} '{t.Text}'.",
                        t.Line,
                        t.Column);
                }
                break;
            }
            state.Consume();
            OdinTokenSnapshot keyToken = state.Peek();
            (string keyText, OdinKind thisKeyKind) = ConsumeKey(ref state, keyToken);
            if (!keyKindSet)
            {
                keyKind = thisKeyKind;
                keyKindSet = true;
            }
            state.Expect(OdinTokenKind.RightBracket, "']'");
            if (entries.ContainsKey(keyText))
            {
                throw new OdinParseException(
                    $"Duplicate hash key '{keyText}' (validity rule VDOBU).",
                    keyToken.Line,
                    keyToken.Column);
            }
            state.Expect(OdinTokenKind.Equals, "'='");
            OdinValue value = ParseTypedBlock(ref state);
            entries[keyText] = value;
            // Optional ';' separator.
            if (state.Peek().Kind == OdinTokenKind.Semicolon)
            {
                state.Consume();
            }
        }

        OdinHash hash = new(entries) { KeyKind = keyKind };
        return hash;
    }

    private static (string keyText, OdinKind keyKind) ConsumeKey(ref ParserState state, OdinTokenSnapshot t)
    {
        switch (t.Kind)
        {
            case OdinTokenKind.StringLiteral:
                state.Consume();
                return (t.Text, OdinKind.String);
            case OdinTokenKind.IntegerLiteral:
                state.Consume();
                return (t.Text, OdinKind.Integer);
            case OdinTokenKind.DateLiteral:
                state.Consume();
                return (t.Text, OdinKind.Date);
            case OdinTokenKind.TimeLiteral:
                state.Consume();
                return (t.Text, OdinKind.Time);
            case OdinTokenKind.DateTimeLiteral:
                state.Consume();
                return (t.Text, OdinKind.DateTime);
            case OdinTokenKind.Identifier:
                // SPEC: bare identifier keys (e.g. [foo]) are not in the
                // ODIN spec but ADL2 emits them in some terminology
                // bindings; treat as a string key.
                state.Consume();
                return (t.Text, OdinKind.String);
            default:
                throw new OdinParseException(
                    $"Expected primitive key inside '[...]'; found {t.Kind} '{t.Text}'.",
                    t.Line,
                    t.Column);
        }
    }

    private static OdinValue ParseLeafOrListStartingAt(ref ParserState state, OdinTokenSnapshot first)
    {
        OdinValue head = ParseLeafToken(ref state, first);
        // Check for list continuation.
        OdinTokenSnapshot next = state.Peek();
        if (next.Kind != OdinTokenKind.Comma) return head;

        List<OdinValue> items = [head];
        bool continuation = false;
        while (state.Peek().Kind == OdinTokenKind.Comma)
        {
            state.Consume();
            OdinTokenSnapshot t = state.Peek();
            if (t.Kind == OdinTokenKind.Ellipsis)
            {
                state.Consume();
                continuation = true;
                break;
            }
            OdinValue item;
            if (t.Kind == OdinTokenKind.LeftBracket)
            {
                item = ParseBracketStart(ref state);
            }
            else
            {
                item = ParseLeafToken(ref state, t);
            }
            items.Add(item);
        }
        OdinList list = new(items) { HasContinuationMarker = continuation };
        return list;
    }

    private static OdinValue ParseLeafToken(ref ParserState state, OdinTokenSnapshot t)
    {
        switch (t.Kind)
        {
            case OdinTokenKind.StringLiteral:
                state.Consume();
                return new OdinString(t.Text);
            case OdinTokenKind.CharLiteral:
                // Char literals are stored as a length-1 string.
                state.Consume();
                return new OdinString(t.Text);
            case OdinTokenKind.IntegerLiteral:
                state.Consume();
                return new OdinInteger(ParseInteger(t));
            case OdinTokenKind.RealLiteral:
                state.Consume();
                return new OdinReal(ParseReal(t));
            case OdinTokenKind.BooleanLiteral:
                state.Consume();
                return new OdinBoolean(string.Equals(t.Text, "true", StringComparison.OrdinalIgnoreCase));
            case OdinTokenKind.DateLiteral:
                state.Consume();
                return ParseDateToken(t);
            case OdinTokenKind.TimeLiteral:
                state.Consume();
                return ParseTimeToken(t);
            case OdinTokenKind.DateTimeLiteral:
                state.Consume();
                return ParseDateTimeToken(t);
            case OdinTokenKind.DurationLiteral:
                state.Consume();
                return ParseDurationToken(t);
            case OdinTokenKind.LeftBracket:
                return ParseBracketStart(ref state);
            case OdinTokenKind.Pipe:
                return ParseInterval(ref state);
            default:
                throw new OdinParseException(
                    $"Expected a leaf value; found {t.Kind} '{t.Text}'.",
                    t.Line,
                    t.Column);
        }
    }

    private static long ParseInteger(OdinTokenSnapshot t)
    {
        // GRAMMAR: spec 7.1.3 allows '25', '300000', '29e6'.
        string text = t.Text;
        int eIdx = text.IndexOfAny(['e', 'E']);
        try
        {
            if (eIdx < 0)
            {
                return long.Parse(text, NumberStyles.Integer | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            }
            long mantissa = long.Parse(text.AsSpan(0, eIdx), NumberStyles.Integer | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            int exp = int.Parse(text.AsSpan(eIdx + 1), NumberStyles.Integer | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            long mul = 1;
            for (int i = 0; i < Math.Abs(exp); i++)
            {
                mul = checked(mul * 10);
            }
            return exp >= 0 ? checked(mantissa * mul) : mantissa / mul;
        }
        catch (OverflowException)
        {
            throw new OdinParseException($"Integer literal '{text}' overflows Int64.", t.Line, t.Column);
        }
        catch (FormatException)
        {
            throw new OdinParseException($"Malformed integer literal '{text}'.", t.Line, t.Column);
        }
    }

    private static double ParseReal(OdinTokenSnapshot t)
    {
        if (!double.TryParse(t.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            throw new OdinParseException($"Malformed real literal '{t.Text}'.", t.Line, t.Column);
        }
        return value;
    }

    private static OdinValue ParseDateToken(OdinTokenSnapshot t)
    {
        // SPEC: ODIN partial-form dates using '??' are not representable
        // as IsoDate; fall back to OdinString preserving the lexical form.
        if (t.Text.IndexOf('?') >= 0)
        {
            return new OdinString(t.Text);
        }
        if (!IsoDate.TryParse(t.Text.AsSpan(), out IsoDate? date))
        {
            throw new OdinParseException($"Malformed date literal '{t.Text}'.", t.Line, t.Column);
        }
        return new OdinDate(date);
    }

    private static OdinValue ParseTimeToken(OdinTokenSnapshot t)
    {
        if (t.Text.IndexOf('?') >= 0)
        {
            return new OdinString(t.Text);
        }
        if (!IsoTime.TryParse(t.Text.AsSpan(), out IsoTime? time))
        {
            throw new OdinParseException($"Malformed time literal '{t.Text}'.", t.Line, t.Column);
        }
        return new OdinTime(time);
    }

    private static OdinValue ParseDateTimeToken(OdinTokenSnapshot t)
    {
        if (t.Text.IndexOf('?') >= 0)
        {
            return new OdinString(t.Text);
        }
        if (!IsoDateTime.TryParse(t.Text.AsSpan(), out IsoDateTime? dt))
        {
            throw new OdinParseException($"Malformed date-time literal '{t.Text}'.", t.Line, t.Column);
        }
        return new OdinDateTime(dt);
    }

    private static OdinValue ParseDurationToken(OdinTokenSnapshot t)
    {
        if (!IsoDuration.TryParse(t.Text.AsSpan(), out IsoDuration? d))
        {
            throw new OdinParseException($"Malformed duration literal '{t.Text}'.", t.Line, t.Column);
        }
        return new OdinDuration(d);
    }
}
