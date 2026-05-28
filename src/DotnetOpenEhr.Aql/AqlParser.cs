using System.Globalization;
using DotnetOpenEhr.Aql.Ast;
using DotnetOpenEhr.Aql.Lexer;

namespace DotnetOpenEhr.Aql;

/// <summary>
/// Hand-written, single-pass recursive-descent parser for openEHR AQL.
/// Consumes the token stream produced by <see cref="AqlLexer"/> and
/// builds an <see cref="AqlQuery"/> AST.
/// </summary>
/// <remarks>
/// // GRAMMAR: openEHR AQL ANTLR4 parser grammar (current spec). The
/// parser implements the expression precedence chain
///   OR &lt; AND &lt; NOT &lt; comparison &lt; concat &lt; additive
///   &lt; multiplicative &lt; unary &lt; primary
/// over a general Expression hierarchy, which is a superset of the
/// spec's <c>identifiedExpr</c>/<c>terminal</c> grammar but accepts
/// every spec-conformant query.
/// </remarks>
public static class AqlParser
{
    public static AqlQuery Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Parse(source.AsSpan());
    }

    public static AqlQuery Parse(ReadOnlySpan<char> source)
    {
        List<AqlTokenInfo> tokens = TokenizeAll(source);
        ParserState state = new(tokens);
        AqlQuery query = ParseQuery(state);
        Expect(state, AqlTokenKind.EndOfFile, "end of query");
        return query;
    }

    // ------------------------------------------------------------------
    // Token materialisation (AqlToken is a ref struct; snapshot into a
    // POCO so the parser can index/peek over it).
    // ------------------------------------------------------------------

    private sealed class AqlTokenInfo
    {
        public AqlTokenKind Kind { get; init; }
        public string Text { get; init; } = string.Empty;
        public string? Value { get; init; }
        public string? EmbeddedNodeId { get; init; }
        public int Line { get; init; }
        public int Column { get; init; }
    }

    private static List<AqlTokenInfo> TokenizeAll(ReadOnlySpan<char> source)
    {
        AqlLexer lexer = new(source);
        List<AqlTokenInfo> tokens = [];
        while (true)
        {
            AqlToken t = lexer.NextToken();
            tokens.Add(new AqlTokenInfo
            {
                Kind = t.Kind,
                Text = t.Span.ToString(),
                Value = t.Value,
                EmbeddedNodeId = t.EmbeddedNodeId,
                Line = t.Line,
                Column = t.Column,
            });
            if (t.Kind == AqlTokenKind.EndOfFile)
            {
                break;
            }
        }
        return tokens;
    }

    private sealed class ParserState
    {
        private readonly List<AqlTokenInfo> _tokens;
        public int Index;
        public ParserState(List<AqlTokenInfo> tokens)
        {
            _tokens = tokens;
            Index = 0;
        }
        public AqlTokenInfo Peek(int offset = 0)
            => _tokens[Math.Min(Index + offset, _tokens.Count - 1)];
        public AqlTokenInfo Consume() => _tokens[Index++];
        public bool At(AqlTokenKind kind) => Peek().Kind == kind;
        public bool MatchAndConsume(AqlTokenKind kind)
        {
            if (At(kind))
            {
                Index++;
                return true;
            }
            return false;
        }
    }

    private static AqlTokenInfo Expect(ParserState s, AqlTokenKind kind, string what)
    {
        AqlTokenInfo t = s.Peek();
        if (t.Kind != kind)
        {
            throw new AqlParseException(
                $"Expected {what} but found {Describe(t)}.",
                t.Line,
                t.Column);
        }
        return s.Consume();
    }

    private static string Describe(AqlTokenInfo t)
        => t.Kind switch
        {
            AqlTokenKind.EndOfFile => "end of input",
            AqlTokenKind.Identifier => $"identifier '{t.Value}'",
            AqlTokenKind.StringLiteral => $"string literal",
            AqlTokenKind.IntegerLiteral => $"integer literal '{t.Text}'",
            AqlTokenKind.RealLiteral => $"real literal '{t.Text}'",
            AqlTokenKind.Placeholder => $"placeholder '${t.Value}'",
            _ => $"'{t.Text}'",
        };

    // ------------------------------------------------------------------
    // Top-level production.
    // ------------------------------------------------------------------

    private static AqlQuery ParseQuery(ParserState s)
    {
        SelectClause select = ParseSelect(s);
        FromClause from = ParseFrom(s);
        WhereClause? where = s.At(AqlTokenKind.Where) ? ParseWhere(s) : null;
        OrderByClause? orderBy = s.At(AqlTokenKind.Order) ? ParseOrderBy(s) : null;
        int? limit = null;
        int? offset = null;
        if (s.MatchAndConsume(AqlTokenKind.Limit))
        {
            AqlTokenInfo n = Expect(s, AqlTokenKind.IntegerLiteral, "LIMIT count");
            limit = int.Parse(n.Text, CultureInfo.InvariantCulture);
            if (s.MatchAndConsume(AqlTokenKind.Offset))
            {
                AqlTokenInfo o = Expect(s, AqlTokenKind.IntegerLiteral, "OFFSET amount");
                offset = int.Parse(o.Text, CultureInfo.InvariantCulture);
            }
        }
        if (offset is null && s.MatchAndConsume(AqlTokenKind.Offset))
        {
            AqlTokenInfo o = Expect(s, AqlTokenKind.IntegerLiteral, "OFFSET amount");
            offset = int.Parse(o.Text, CultureInfo.InvariantCulture);
        }
        return new AqlQuery(select, from, where, orderBy, limit, offset);
    }

    // ------------------------------------------------------------------
    // SELECT.
    // ------------------------------------------------------------------

    private static SelectClause ParseSelect(ParserState s)
    {
        Expect(s, AqlTokenKind.Select, "SELECT");
        bool distinct = s.MatchAndConsume(AqlTokenKind.Distinct);
        int? top = null;
        AqlOrderDirection? topDir = null;
        if (s.MatchAndConsume(AqlTokenKind.Top))
        {
            AqlTokenInfo n = Expect(s, AqlTokenKind.IntegerLiteral, "TOP count");
            top = int.Parse(n.Text, CultureInfo.InvariantCulture);
            if (s.MatchAndConsume(AqlTokenKind.Forward))
            {
                topDir = AqlOrderDirection.Ascending;
            }
            else if (s.MatchAndConsume(AqlTokenKind.Backward))
            {
                topDir = AqlOrderDirection.Descending;
            }
        }
        List<ColumnExpression> cols = [];
        cols.Add(ParseColumn(s));
        while (s.MatchAndConsume(AqlTokenKind.Comma))
        {
            cols.Add(ParseColumn(s));
        }
        return new SelectClause(new AqlAstList<ColumnExpression>(cols), distinct, top, topDir);
    }

    private static ColumnExpression ParseColumn(ParserState s)
    {
        Expression expr = ParseExpression(s);
        string? alias = null;
        if (s.MatchAndConsume(AqlTokenKind.As))
        {
            AqlTokenInfo a = Expect(s, AqlTokenKind.Identifier, "alias identifier after AS");
            alias = a.Value;
        }
        return new ColumnExpression(expr, alias);
    }

    // ------------------------------------------------------------------
    // FROM.
    // ------------------------------------------------------------------

    private static FromClause ParseFrom(ParserState s)
    {
        Expect(s, AqlTokenKind.From, "FROM");
        List<ClassExpression> sources = [];
        sources.Add(ParseClassWithContains(s));
        // Multiple top-level class expressions joined by ',' are not
        // common but the AST supports them.
        while (s.MatchAndConsume(AqlTokenKind.Comma))
        {
            sources.Add(ParseClassWithContains(s));
        }
        return new FromClause(new AqlAstList<ClassExpression>(sources));
    }

    private static ClassExpression ParseClassWithContains(ParserState s)
    {
        ClassExpression head = ParseClassExpression(s);
        if (s.At(AqlTokenKind.Contains) || s.At(AqlTokenKind.Not))
        {
            List<ContainsExpression> contains = [];
            // Parse the full CONTAINS chain into a list. Logical
            // composition (AND/OR/NOT) is handled inside ParseContains.
            do
            {
                contains.Add(ParseContains(s));
            }
            while (s.At(AqlTokenKind.Contains) || s.At(AqlTokenKind.Not));
            head = head with { Contains = new AqlAstList<ContainsExpression>(contains) };
        }
        return head;
    }

    private static ClassExpression ParseClassExpression(ParserState s)
    {
        AqlTokenInfo type = s.Peek();
        if (type.Kind != AqlTokenKind.Identifier
            && type.Kind != AqlTokenKind.Ehr
            && type.Kind != AqlTokenKind.Composition)
        {
            throw new AqlParseException(
                $"Expected RM class name but found {Describe(type)}.",
                type.Line,
                type.Column);
        }
        s.Consume();
        string rmType = type.Value ?? type.Text;
        string? alias = null;
        if (s.At(AqlTokenKind.Identifier))
        {
            // Variable / alias name.
            alias = s.Consume().Value;
        }
        Predicate? predicate = null;
        if (s.MatchAndConsume(AqlTokenKind.LeftBracket))
        {
            predicate = ParsePredicateBody(s);
            Expect(s, AqlTokenKind.RightBracket, "']' to close predicate");
        }
        return new ClassExpression(rmType, alias, AqlAstList<ContainsExpression>.Empty, predicate);
    }

    private static Predicate ParsePredicateBody(ParserState s)
    {
        // Predicate bodies are either a bare archetype HRID literal, a
        // placeholder, or a boolean expression over identified paths.
        if (s.At(AqlTokenKind.ArchetypeHridLiteral))
        {
            AqlTokenInfo hrid = s.Consume();
            return new Predicate(new LiteralExpression(hrid.Value, AqlLiteralKind.ArchetypeHrid));
        }
        Expression body = ParseExpression(s);
        return new Predicate(body);
    }

    // CONTAINS chain.
    //
    // The spec allows boolean composition of class expressions inside
    // CONTAINS: `CONTAINS A OR (B AND C)`. We implement OR > AND >
    // primary precedence; NOT-CONTAINS is parsed as a unary negation
    // attached to a class expression.

    private static ContainsExpression ParseContains(ParserState s)
    {
        bool negated = false;
        if (s.MatchAndConsume(AqlTokenKind.Not))
        {
            negated = true;
        }
        Expect(s, AqlTokenKind.Contains, "CONTAINS");
        ContainsExpression inner = ParseContainsOr(s);
        return negated ? new ContainsNotExpression(inner) : inner;
    }

    private static ContainsExpression ParseContainsOr(ParserState s)
    {
        ContainsExpression left = ParseContainsAnd(s);
        while (s.MatchAndConsume(AqlTokenKind.Or))
        {
            ContainsExpression right = ParseContainsAnd(s);
            left = new ContainsOrExpression(left, right);
        }
        return left;
    }

    private static ContainsExpression ParseContainsAnd(ParserState s)
    {
        ContainsExpression left = ParseContainsPrimary(s);
        while (s.MatchAndConsume(AqlTokenKind.And))
        {
            ContainsExpression right = ParseContainsPrimary(s);
            left = new ContainsAndExpression(left, right);
        }
        return left;
    }

    private static ContainsExpression ParseContainsPrimary(ParserState s)
    {
        if (s.MatchAndConsume(AqlTokenKind.LeftParen))
        {
            ContainsExpression inner = ParseContainsOr(s);
            Expect(s, AqlTokenKind.RightParen, "')' to close CONTAINS group");
            return inner;
        }
        ClassExpression klass = ParseClassExpression(s);
        if (s.At(AqlTokenKind.Contains) || s.At(AqlTokenKind.Not))
        {
            List<ContainsExpression> nested = [];
            do
            {
                nested.Add(ParseContains(s));
            }
            while (s.At(AqlTokenKind.Contains) || s.At(AqlTokenKind.Not));
            klass = klass with { Contains = new AqlAstList<ContainsExpression>(nested) };
        }
        return new ContainsClassExpression(klass);
    }

    // ------------------------------------------------------------------
    // WHERE / ORDER BY.
    // ------------------------------------------------------------------

    private static WhereClause ParseWhere(ParserState s)
    {
        Expect(s, AqlTokenKind.Where, "WHERE");
        Expression expr = ParseExpression(s);
        return new WhereClause(expr);
    }

    private static OrderByClause ParseOrderBy(ParserState s)
    {
        Expect(s, AqlTokenKind.Order, "ORDER");
        Expect(s, AqlTokenKind.By, "BY");
        List<OrderByItem> items = [];
        items.Add(ParseOrderItem(s));
        while (s.MatchAndConsume(AqlTokenKind.Comma))
        {
            items.Add(ParseOrderItem(s));
        }
        return new OrderByClause(new AqlAstList<OrderByItem>(items));
    }

    private static OrderByItem ParseOrderItem(ParserState s)
    {
        Expression expr = ParseExpression(s);
        AqlOrderDirection dir = AqlOrderDirection.Ascending;
        if (s.MatchAndConsume(AqlTokenKind.Asc))
        {
            dir = AqlOrderDirection.Ascending;
        }
        else if (s.MatchAndConsume(AqlTokenKind.Desc))
        {
            dir = AqlOrderDirection.Descending;
        }
        return new OrderByItem(expr, dir);
    }

    // ------------------------------------------------------------------
    // Expression precedence chain:
    //   OR < AND < NOT < comparison < concat < additive
    //   < multiplicative < unary < primary
    // ------------------------------------------------------------------

    private static Expression ParseExpression(ParserState s) => ParseOr(s);

    private static Expression ParseOr(ParserState s)
    {
        Expression left = ParseAnd(s);
        while (s.MatchAndConsume(AqlTokenKind.Or))
        {
            Expression right = ParseAnd(s);
            left = new BinaryExpression(BinaryOp.Or, left, right);
        }
        return left;
    }

    private static Expression ParseAnd(ParserState s)
    {
        Expression left = ParseNot(s);
        while (s.MatchAndConsume(AqlTokenKind.And))
        {
            Expression right = ParseNot(s);
            left = new BinaryExpression(BinaryOp.And, left, right);
        }
        return left;
    }

    private static Expression ParseNot(ParserState s)
    {
        if (s.MatchAndConsume(AqlTokenKind.Not))
        {
            Expression inner = ParseNot(s);
            return new UnaryExpression(UnaryOp.Not, inner);
        }
        // EXISTS is a prefix operator at the same level as NOT.
        if (s.MatchAndConsume(AqlTokenKind.Exists))
        {
            Expression inner = ParseComparison(s);
            return new ExistsExpression(inner);
        }
        return ParseComparison(s);
    }

    private static Expression ParseComparison(ParserState s)
    {
        Expression left = ParseConcat(s);
        // Chains of comparisons are not allowed in AQL - only a single
        // operator. LIKE / MATCHES / IS NULL are handled here too.
        AqlTokenInfo t = s.Peek();
        switch (t.Kind)
        {
            case AqlTokenKind.Equals:
                s.Consume();
                return new BinaryExpression(BinaryOp.Eq, left, ParseConcat(s));
            case AqlTokenKind.NotEqual:
                s.Consume();
                return new BinaryExpression(BinaryOp.NotEq, left, ParseConcat(s));
            case AqlTokenKind.LessThan:
                s.Consume();
                return new BinaryExpression(BinaryOp.Lt, left, ParseConcat(s));
            case AqlTokenKind.LessEqual:
                s.Consume();
                return new BinaryExpression(BinaryOp.Lte, left, ParseConcat(s));
            case AqlTokenKind.GreaterThan:
                s.Consume();
                return new BinaryExpression(BinaryOp.Gt, left, ParseConcat(s));
            case AqlTokenKind.GreaterEqual:
                s.Consume();
                return new BinaryExpression(BinaryOp.Gte, left, ParseConcat(s));
            case AqlTokenKind.Like:
                s.Consume();
                return new BinaryExpression(BinaryOp.Like, left, ParseConcat(s));
            case AqlTokenKind.Matches:
                s.Consume();
                return ParseMatchesTail(s, left);
            case AqlTokenKind.Is:
                s.Consume();
                bool negated = s.MatchAndConsume(AqlTokenKind.Not);
                Expect(s, AqlTokenKind.Null, "NULL after IS");
                LiteralExpression nullLit = new(null, AqlLiteralKind.Null);
                BinaryExpression cmp = new(
                    negated ? BinaryOp.NotEq : BinaryOp.Eq,
                    left,
                    nullLit);
                return cmp;
            default:
                return left;
        }
    }

    private static Expression ParseMatchesTail(ParserState s, Expression subject)
    {
        // MATCHES is followed by either '{ v1, v2, ... }' or '{ URI }'
        // or a TERMINOLOGY(...) function call.
        if (s.MatchAndConsume(AqlTokenKind.LeftBrace))
        {
            List<Expression> values = [];
            values.Add(ParseExpression(s));
            while (s.MatchAndConsume(AqlTokenKind.Comma))
            {
                values.Add(ParseExpression(s));
            }
            Expect(s, AqlTokenKind.RightBrace, "'}' to close MATCHES value list");
            return new MatchesExpression(subject, new AqlAstList<Expression>(values));
        }
        // Function-call form: MATCHES TERMINOLOGY(...).
        Expression rhs = ParseConcat(s);
        return new BinaryExpression(BinaryOp.Matches, subject, rhs);
    }

    private static Expression ParseConcat(ParserState s)
    {
        Expression left = ParseAdditive(s);
        while (s.MatchAndConsume(AqlTokenKind.Concat))
        {
            Expression right = ParseAdditive(s);
            left = new BinaryExpression(BinaryOp.Concat, left, right);
        }
        return left;
    }

    private static Expression ParseAdditive(ParserState s)
    {
        Expression left = ParseMultiplicative(s);
        while (true)
        {
            if (s.MatchAndConsume(AqlTokenKind.Plus))
            {
                Expression right = ParseMultiplicative(s);
                left = new BinaryExpression(BinaryOp.Plus, left, right);
                continue;
            }
            if (s.MatchAndConsume(AqlTokenKind.Minus))
            {
                Expression right = ParseMultiplicative(s);
                left = new BinaryExpression(BinaryOp.Minus, left, right);
                continue;
            }
            break;
        }
        return left;
    }

    private static Expression ParseMultiplicative(ParserState s)
    {
        Expression left = ParseUnary(s);
        while (true)
        {
            if (s.MatchAndConsume(AqlTokenKind.Star))
            {
                Expression right = ParseUnary(s);
                left = new BinaryExpression(BinaryOp.Multiply, left, right);
                continue;
            }
            if (s.MatchAndConsume(AqlTokenKind.Slash))
            {
                Expression right = ParseUnary(s);
                left = new BinaryExpression(BinaryOp.Divide, left, right);
                continue;
            }
            break;
        }
        return left;
    }

    private static Expression ParseUnary(ParserState s)
    {
        if (s.MatchAndConsume(AqlTokenKind.Minus))
        {
            Expression inner = ParseUnary(s);
            return new UnaryExpression(UnaryOp.Negate, inner);
        }
        if (s.MatchAndConsume(AqlTokenKind.Plus))
        {
            return ParseUnary(s);
        }
        return ParsePrimary(s);
    }

    private static Expression ParsePrimary(ParserState s)
    {
        AqlTokenInfo t = s.Peek();
        switch (t.Kind)
        {
            case AqlTokenKind.LeftParen:
            {
                s.Consume();
                Expression inner = ParseExpression(s);
                Expect(s, AqlTokenKind.RightParen, "')' to close parenthesised expression");
                return inner;
            }
            case AqlTokenKind.IntegerLiteral:
                s.Consume();
                return new LiteralExpression(
                    long.Parse(t.Text, CultureInfo.InvariantCulture),
                    AqlLiteralKind.Integer);
            case AqlTokenKind.RealLiteral:
                s.Consume();
                return new LiteralExpression(
                    double.Parse(t.Text, CultureInfo.InvariantCulture),
                    AqlLiteralKind.Real);
            case AqlTokenKind.StringLiteral:
            {
                s.Consume();
                string raw = t.Value ?? string.Empty;
                AqlLiteralKind kind = ClassifyStringShape(raw);
                return new LiteralExpression(raw, kind);
            }
            case AqlTokenKind.True:
                s.Consume();
                return new LiteralExpression(true, AqlLiteralKind.Boolean);
            case AqlTokenKind.False:
                s.Consume();
                return new LiteralExpression(false, AqlLiteralKind.Boolean);
            case AqlTokenKind.Null:
                s.Consume();
                return new LiteralExpression(null, AqlLiteralKind.Null);
            case AqlTokenKind.Placeholder:
                s.Consume();
                return new LiteralExpression(t.Value, AqlLiteralKind.Placeholder);
            case AqlTokenKind.AtCode:
            case AqlTokenKind.IdCode:
            case AqlTokenKind.AcCode:
                s.Consume();
                return new LiteralExpression(t.Value, AqlLiteralKind.Code);
            case AqlTokenKind.Star:
                // Permit `*` as a degenerate primary, used by
                // `count(*)`. The Star may have been consumed as a
                // multiplicative operator at higher levels; here it
                // only appears as a function argument.
                s.Consume();
                return new IdentifierExpression("*");
            case AqlTokenKind.Identifier:
                return ParseIdentifierOrCall(s);
            default:
                throw new AqlParseException(
                    $"Unexpected {Describe(t)} in expression.",
                    t.Line,
                    t.Column);
        }
    }

    private static Expression ParseIdentifierOrCall(ParserState s)
    {
        AqlTokenInfo id = s.Consume();
        string name = id.Value ?? id.Text;
        Expression root;
        if (s.MatchAndConsume(AqlTokenKind.LeftParen))
        {
            // Function-call form: `name(arg1, arg2, ...)`.
            List<Expression> args = [];
            if (!s.At(AqlTokenKind.RightParen))
            {
                args.Add(ParseExpression(s));
                while (s.MatchAndConsume(AqlTokenKind.Comma))
                {
                    args.Add(ParseExpression(s));
                }
            }
            Expect(s, AqlTokenKind.RightParen, "')' to close function call");
            return new FunctionCallExpression(name, new AqlAstList<Expression>(args));
        }
        root = new IdentifierExpression(name);
        // Path tail: zero or more PathSegments. The first '/ident' is
        // a PathSegment token (the lexer already absorbed it); a bare
        // bracket predicate '[…]' attached directly to the identifier
        // is also part of the path's root step.
        List<PathStep>? steps = null;
        string? rootPredicate = null;
        if (s.At(AqlTokenKind.LeftBracket))
        {
            // Root-level predicate: looks like `c[ehr_id/value=$x]`.
            // We capture the textual / structural predicate as an
            // attribute of the root step. For the AST we wrap the
            // identifier as a single PathStep on the root with the
            // predicate attached.
            s.Consume();
            // For a simple node-id we capture the value; otherwise we
            // skip parsing further (the spec allows complex predicates
            // but those are only meaningful in FROM-clause class
            // expressions, not on identified paths in WHERE/SELECT).
            AqlTokenInfo first = s.Peek();
            if (first.Kind == AqlTokenKind.IdCode || first.Kind == AqlTokenKind.AtCode
                || first.Kind == AqlTokenKind.AcCode)
            {
                rootPredicate = first.Value;
                s.Consume();
            }
            else if (first.Kind == AqlTokenKind.ArchetypeHridLiteral)
            {
                rootPredicate = first.Value;
                s.Consume();
            }
            else
            {
                // Skip the bracket body verbatim.
                int depth = 1;
                while (depth > 0)
                {
                    AqlTokenInfo skip = s.Peek();
                    if (skip.Kind == AqlTokenKind.EndOfFile)
                    {
                        throw new AqlParseException(
                            "Unterminated identifier predicate.",
                            skip.Line,
                            skip.Column);
                    }
                    if (skip.Kind == AqlTokenKind.LeftBracket) depth++;
                    else if (skip.Kind == AqlTokenKind.RightBracket) depth--;
                    if (depth > 0)
                    {
                        s.Consume();
                    }
                }
            }
            Expect(s, AqlTokenKind.RightBracket, "']' to close predicate");
        }
        while (s.At(AqlTokenKind.PathSegment))
        {
            steps ??= [];
            AqlTokenInfo seg = s.Consume();
            steps.Add(new PathStep(seg.Value ?? seg.Text, seg.EmbeddedNodeId));
            // A PathSegment may be followed by a stand-alone predicate
            // bracket that the lexer did not fold into the segment
            // (e.g. when the predicate is more complex than [atN]).
            // Consume it for ordering hygiene, but for v1 we do not
            // bind it back to the segment.
            if (s.At(AqlTokenKind.LeftBracket))
            {
                s.Consume();
                int depth = 1;
                while (depth > 0)
                {
                    AqlTokenInfo skip = s.Peek();
                    if (skip.Kind == AqlTokenKind.EndOfFile)
                    {
                        throw new AqlParseException(
                            "Unterminated path-step predicate.",
                            skip.Line,
                            skip.Column);
                    }
                    if (skip.Kind == AqlTokenKind.LeftBracket) depth++;
                    else if (skip.Kind == AqlTokenKind.RightBracket) depth--;
                    if (depth > 0)
                    {
                        s.Consume();
                    }
                }
                Expect(s, AqlTokenKind.RightBracket, "']' to close path-step predicate");
            }
        }
        if (rootPredicate is not null)
        {
            // Wrap as a path expression so the root carries its
            // predicate alongside subsequent steps.
            steps ??= [];
            steps.Insert(0, new PathStep(name, rootPredicate));
            return new PathExpression(new IdentifierExpression(name), new AqlAstList<PathStep>(steps));
        }
        if (steps is { Count: > 0 })
        {
            return new PathExpression(root, new AqlAstList<PathStep>(steps));
        }
        return root;
    }

    private static AqlLiteralKind ClassifyStringShape(string raw)
    {
        // Promote string literals whose textual content is ISO 8601
        // date-time or duration to the corresponding literal kind so
        // downstream evaluators can short-circuit type coercion.
        if (LooksLikeDuration(raw))
        {
            return AqlLiteralKind.IsoDuration;
        }
        if (LooksLikeDateTime(raw))
        {
            return AqlLiteralKind.IsoDateTime;
        }
        return AqlLiteralKind.String;
    }

    private static bool LooksLikeDuration(string raw)
    {
        // GRAMMAR: ISO 8601 - Pn[Yn][Mn][Wn][Dn][Tn[Hn][Mn][Sn]] or
        // PnW. Conservative shape check.
        if (raw.Length < 2 || raw[0] != 'P')
        {
            return false;
        }
        bool sawDesignator = false;
        bool inTime = false;
        for (int i = 1; i < raw.Length; i++)
        {
            char ch = raw[i];
            if (ch == 'T')
            {
                if (inTime) return false;
                inTime = true;
                continue;
            }
            int digitStart = i;
            while (i < raw.Length && ((raw[i] >= '0' && raw[i] <= '9') || raw[i] == '.' || raw[i] == ','))
            {
                i++;
            }
            if (i == digitStart) return false;
            if (i >= raw.Length) return false;
            char des = raw[i];
            if (inTime)
            {
                if (des != 'H' && des != 'M' && des != 'S') return false;
            }
            else
            {
                if (des != 'Y' && des != 'M' && des != 'W' && des != 'D') return false;
            }
            sawDesignator = true;
        }
        return sawDesignator;
    }

    private static bool LooksLikeDateTime(string raw)
    {
        // Match a leading YYYY-MM-DD (extended ISO) or YYYYMMDD (basic).
        // Optionally followed by T HH:MM:SS / THHMMSS / fractional /
        // timezone. We accept the conservative super-set and let the
        // evaluator validate later.
        if (raw.Length < 8)
        {
            return false;
        }
        int p = 0;
        if (!IsDigitN(raw, p, 4)) return false;
        p += 4;
        if (p < raw.Length && raw[p] == '-')
        {
            p++;
        }
        if (!IsDigitN(raw, p, 2)) return false;
        p += 2;
        if (p < raw.Length && raw[p] == '-')
        {
            p++;
        }
        if (!IsDigitN(raw, p, 2)) return false;
        p += 2;
        if (p == raw.Length)
        {
            return true;
        }
        if (raw[p] != 'T' && raw[p] != ' ')
        {
            return false;
        }
        return true;
    }

    private static bool IsDigitN(string raw, int start, int n)
    {
        if (start + n > raw.Length) return false;
        for (int i = 0; i < n; i++)
        {
            char c = raw[start + i];
            if (c < '0' || c > '9') return false;
        }
        return true;
    }
}
