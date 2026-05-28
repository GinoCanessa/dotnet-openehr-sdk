using DotnetOpenEhr.Aql;
using DotnetOpenEhr.Aql.Ast;
using Xunit;

namespace DotnetOpenEhr.Aql.Tests.Parser;

/// <summary>
/// Parser-level tests covering one positive + one negative per
/// top-level grammar production, all worked AQL examples from the
/// spec (with round-trip via <see cref="AqlPrinter"/>), and source
/// position tracking on error.
/// </summary>
public class AqlParserTests
{
    // ----------------------------------------------------------------
    // Top-level productions: positive + negative.
    // ----------------------------------------------------------------

    [Fact]
    public void Select_minimal()
    {
        AqlQuery q = AqlParser.Parse("SELECT c FROM EHR e");
        Assert.Single(q.Select.Columns);
        Assert.Equal("c", ((IdentifierExpression)q.Select.Columns[0].Expr).Name);
    }

    [Fact]
    public void Select_missing_columns_errors()
    {
        AqlParseException ex = Assert.Throws<AqlParseException>(() =>
            AqlParser.Parse("SELECT FROM EHR e"));
        Assert.True(ex.Column > 0);
    }

    [Fact]
    public void From_with_class_and_alias()
    {
        AqlQuery q = AqlParser.Parse("SELECT c FROM EHR e CONTAINS COMPOSITION c");
        Assert.Equal("EHR", q.From.Sources[0].RmTypeName);
        Assert.Equal("e", q.From.Sources[0].Alias);
        Assert.Single(q.From.Sources[0].Contains);
    }

    [Fact]
    public void From_missing_class_errors()
    {
        Assert.Throws<AqlParseException>(() => AqlParser.Parse("SELECT c FROM"));
    }

    [Fact]
    public void Where_simple_comparison()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e CONTAINS COMPOSITION c WHERE c/name/value = 'Vital Signs'");
        Assert.NotNull(q.Where);
        BinaryExpression bin = Assert.IsType<BinaryExpression>(q.Where!.Predicate);
        Assert.Equal(BinaryOp.Eq, bin.Op);
    }

    [Fact]
    public void Where_missing_predicate_errors()
    {
        Assert.Throws<AqlParseException>(() =>
            AqlParser.Parse("SELECT c FROM EHR e CONTAINS COMPOSITION c WHERE"));
    }

    [Fact]
    public void OrderBy_with_direction()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e CONTAINS COMPOSITION c ORDER BY c/context/start_time DESC");
        Assert.NotNull(q.OrderBy);
        Assert.Single(q.OrderBy!.Items);
        Assert.Equal(AqlOrderDirection.Descending, q.OrderBy.Items[0].Direction);
    }

    [Fact]
    public void OrderBy_missing_keyword_errors()
    {
        // 'BY' is required after ORDER.
        Assert.Throws<AqlParseException>(() =>
            AqlParser.Parse("SELECT c FROM EHR e CONTAINS COMPOSITION c ORDER c/uid/value"));
    }

    [Fact]
    public void Limit_and_offset()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e CONTAINS COMPOSITION c LIMIT 10 OFFSET 5");
        Assert.Equal(10, q.Limit);
        Assert.Equal(5, q.Offset);
    }

    [Fact]
    public void Limit_missing_count_errors()
    {
        Assert.Throws<AqlParseException>(() =>
            AqlParser.Parse("SELECT c FROM EHR e CONTAINS COMPOSITION c LIMIT"));
    }

    [Fact]
    public void Offset_standalone()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e CONTAINS COMPOSITION c OFFSET 7");
        Assert.Null(q.Limit);
        Assert.Equal(7, q.Offset);
    }

    [Fact]
    public void Offset_missing_amount_errors()
    {
        Assert.Throws<AqlParseException>(() =>
            AqlParser.Parse("SELECT c FROM EHR e CONTAINS COMPOSITION c OFFSET"));
    }

    [Fact]
    public void Contains_chain()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT o FROM EHR e CONTAINS COMPOSITION c CONTAINS OBSERVATION o");
        ClassExpression root = q.From.Sources[0];
        Assert.Single(root.Contains);
        ContainsClassExpression first = Assert.IsType<ContainsClassExpression>(root.Contains[0]);
        Assert.Equal("COMPOSITION", first.Class.RmTypeName);
        Assert.Single(first.Class.Contains);
    }

    [Fact]
    public void Contains_missing_inner_errors()
    {
        Assert.Throws<AqlParseException>(() =>
            AqlParser.Parse("SELECT c FROM EHR e CONTAINS"));
    }

    [Fact]
    public void Predicate_archetype_hrid()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT o FROM EHR e CONTAINS OBSERVATION o[openEHR-EHR-OBSERVATION.blood_pressure.v2]");
        ContainsClassExpression cc = Assert.IsType<ContainsClassExpression>(
            q.From.Sources[0].Contains[0]);
        Assert.NotNull(cc.Class.Predicate);
        LiteralExpression lit = Assert.IsType<LiteralExpression>(cc.Class.Predicate!.Body);
        Assert.Equal(AqlLiteralKind.ArchetypeHrid, lit.Kind);
        Assert.Equal("openEHR-EHR-OBSERVATION.blood_pressure.v2", lit.Value);
    }

    [Fact]
    public void Predicate_unterminated_errors()
    {
        Assert.Throws<AqlParseException>(() =>
            AqlParser.Parse(
                "SELECT o FROM EHR e CONTAINS OBSERVATION o[openEHR-EHR-OBSERVATION.blood_pressure.v2"));
    }

    [Fact]
    public void Placeholder_in_predicate()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e[ehr_id/value = $ehrUid] CONTAINS COMPOSITION c");
        Assert.NotNull(q.From.Sources[0].Predicate);
        BinaryExpression bin = Assert.IsType<BinaryExpression>(q.From.Sources[0].Predicate!.Body);
        LiteralExpression rhs = Assert.IsType<LiteralExpression>(bin.Right);
        Assert.Equal(AqlLiteralKind.Placeholder, rhs.Kind);
        Assert.Equal("ehrUid", rhs.Value);
    }

    [Fact]
    public void Placeholder_without_name_errors()
    {
        Assert.Throws<AqlLexException>(() =>
            AqlParser.Parse("SELECT c FROM EHR e WHERE c/x = $"));
    }

    [Fact]
    public void Distinct_keyword()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT DISTINCT c FROM EHR e CONTAINS COMPOSITION c");
        Assert.True(q.Select.Distinct);
    }

    [Fact]
    public void Top_with_direction()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT TOP 5 BACKWARD c FROM EHR e CONTAINS COMPOSITION c");
        Assert.Equal(5, q.Select.Top);
        Assert.Equal(AqlOrderDirection.Descending, q.Select.TopDirection);
    }

    [Fact]
    public void Top_missing_count_errors()
    {
        Assert.Throws<AqlParseException>(() =>
            AqlParser.Parse("SELECT TOP FROM EHR e"));
    }

    [Fact]
    public void Exists_expression()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e CONTAINS COMPOSITION c WHERE EXISTS c/content");
        ExistsExpression ex = Assert.IsType<ExistsExpression>(q.Where!.Predicate);
        Assert.IsType<PathExpression>(ex.Operand);
    }

    [Fact]
    public void Matches_value_list()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e CONTAINS COMPOSITION c "
            + "WHERE c/name/value MATCHES {'Vital Signs', 'BP', 'Encounter'}");
        MatchesExpression m = Assert.IsType<MatchesExpression>(q.Where!.Predicate);
        Assert.Equal(3, m.Values.Count);
    }

    [Fact]
    public void Matches_empty_list_errors_at_close_brace_position()
    {
        // Empty set is not allowed by the parser - at least one value
        // is required after '{' before '}'.
        Assert.Throws<AqlParseException>(() =>
            AqlParser.Parse(
                "SELECT c FROM EHR e WHERE c/x MATCHES {}"));
    }

    [Fact]
    public void Like_operator()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e CONTAINS COMPOSITION c "
            + "WHERE c/context/start_time LIKE '2019-0?-*'");
        BinaryExpression bin = Assert.IsType<BinaryExpression>(q.Where!.Predicate);
        Assert.Equal(BinaryOp.Like, bin.Op);
    }

    [Fact]
    public void Like_missing_operand_errors()
    {
        Assert.Throws<AqlParseException>(() =>
            AqlParser.Parse(
                "SELECT c FROM EHR e WHERE c/x LIKE"));
    }

    [Fact]
    public void Is_null_promotes_to_eq_null()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e CONTAINS COMPOSITION c WHERE c/foo IS NULL");
        BinaryExpression bin = Assert.IsType<BinaryExpression>(q.Where!.Predicate);
        Assert.Equal(BinaryOp.Eq, bin.Op);
        LiteralExpression r = Assert.IsType<LiteralExpression>(bin.Right);
        Assert.Equal(AqlLiteralKind.Null, r.Kind);
    }

    [Fact]
    public void Is_not_null_promotes_to_neq_null()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e CONTAINS COMPOSITION c WHERE c/foo IS NOT NULL");
        BinaryExpression bin = Assert.IsType<BinaryExpression>(q.Where!.Predicate);
        Assert.Equal(BinaryOp.NotEq, bin.Op);
    }

    [Fact]
    public void Path_with_multiple_segments_and_predicates()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT o/data[at0001]/events[at0006]/data[at0003]/items[at0004]/value/magnitude "
            + "FROM EHR e CONTAINS OBSERVATION o");
        PathExpression p = Assert.IsType<PathExpression>(q.Select.Columns[0].Expr);
        Assert.Equal("o", ((IdentifierExpression)p.Root).Name);
        Assert.Equal(6, p.Steps.Count);
        Assert.Equal("data", p.Steps[0].AttributeName);
        Assert.Equal("at0001", p.Steps[0].NodeIdPredicate);
        Assert.Equal("magnitude", p.Steps[5].AttributeName);
        Assert.Null(p.Steps[5].NodeIdPredicate);
    }

    [Fact]
    public void Function_call_count_star()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT count(*) FROM EHR e CONTAINS COMPOSITION c");
        FunctionCallExpression call = Assert.IsType<FunctionCallExpression>(q.Select.Columns[0].Expr);
        Assert.Equal("count", call.Name);
        Assert.Single(call.Arguments);
    }

    [Fact]
    public void Column_alias()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/name/value AS Name FROM EHR e CONTAINS COMPOSITION c");
        Assert.Equal("Name", q.Select.Columns[0].Alias);
    }

    // ----------------------------------------------------------------
    // Precedence / associativity.
    // ----------------------------------------------------------------

    [Fact]
    public void And_binds_tighter_than_or()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e WHERE c/a = 1 OR c/b = 2 AND c/c = 3");
        BinaryExpression top = Assert.IsType<BinaryExpression>(q.Where!.Predicate);
        Assert.Equal(BinaryOp.Or, top.Op);
        Assert.Equal(BinaryOp.And, ((BinaryExpression)top.Right).Op);
    }

    [Fact]
    public void Parens_override_or_and_precedence()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e WHERE (c/a = 1 OR c/b = 2) AND c/c = 3");
        BinaryExpression top = Assert.IsType<BinaryExpression>(q.Where!.Predicate);
        Assert.Equal(BinaryOp.And, top.Op);
        Assert.Equal(BinaryOp.Or, ((BinaryExpression)top.Left).Op);
    }

    [Fact]
    public void Multiplicative_binds_tighter_than_additive()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/x + c/y * c/z FROM EHR e CONTAINS COMPOSITION c");
        BinaryExpression top = Assert.IsType<BinaryExpression>(q.Select.Columns[0].Expr);
        Assert.Equal(BinaryOp.Plus, top.Op);
        Assert.Equal(BinaryOp.Multiply, ((BinaryExpression)top.Right).Op);
    }

    [Fact]
    public void Unary_negate_with_subtraction()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e WHERE c/x = -5");
        BinaryExpression bin = Assert.IsType<BinaryExpression>(q.Where!.Predicate);
        UnaryExpression neg = Assert.IsType<UnaryExpression>(bin.Right);
        Assert.Equal(UnaryOp.Negate, neg.Op);
    }

    [Fact]
    public void Not_binds_to_following_comparison()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e WHERE NOT c/x = 1");
        UnaryExpression un = Assert.IsType<UnaryExpression>(q.Where!.Predicate);
        Assert.Equal(UnaryOp.Not, un.Op);
        Assert.IsType<BinaryExpression>(un.Operand);
    }

    // ----------------------------------------------------------------
    // Worked examples (spec or representative). Each parses and
    // round-trips through the printer to a re-parseable form whose
    // AST is structurally equal to the original.
    // ----------------------------------------------------------------

    [Theory]
    [InlineData("SELECT c FROM EHR e CONTAINS COMPOSITION c")]
    [InlineData(
        "SELECT c/uid/value, "
        + "o/data[at0001]/events[at0006]/data[at0003]/items[at0004]/value/magnitude "
        + "FROM EHR e CONTAINS COMPOSITION c CONTAINS OBSERVATION o"
        + "[openEHR-EHR-OBSERVATION.blood_pressure.v2]")]
    [InlineData("SELECT c FROM EHR e CONTAINS COMPOSITION c WHERE c/name/value = 'Vital Signs'")]
    [InlineData(
        "SELECT c FROM EHR e CONTAINS COMPOSITION c "
        + "ORDER BY c/context/start_time DESC LIMIT 10 OFFSET 5")]
    [InlineData(
        "SELECT DISTINCT c/archetype_details/template_id/value "
        + "FROM EHR e CONTAINS COMPOSITION c")]
    [InlineData(
        "SELECT TOP 5 o/data/origin FROM EHR e CONTAINS OBSERVATION o WHERE EXISTS o/data")]
    [InlineData(
        "SELECT c FROM EHR e CONTAINS COMPOSITION c "
        + "WHERE c/name/value MATCHES {'Vital Signs', 'BP', 'Encounter'}")]
    [InlineData(
        "SELECT c FROM EHR e[ehr_id/value = $ehrId] CONTAINS COMPOSITION c "
        + "WHERE c/context/start_time > '2024-01-01T00:00:00Z'")]
    [InlineData(
        "SELECT o/data[at0001]/events[at0006]/data[at0003]/items[at0004]/value/magnitude, "
        + "o/data[at0001]/events[at0006]/data[at0003]/items[at0005]/value/magnitude "
        + "FROM EHR [ehr_id/value = '1234'] "
        + "CONTAINS COMPOSITION [openEHR-EHR-COMPOSITION.encounter.v1] "
        + "CONTAINS OBSERVATION o [openEHR-EHR-OBSERVATION.blood_pressure.v1] "
        + "WHERE o/data[at0001]/events[at0006]/data[at0003]/items[at0004]/value/magnitude >= 140 "
        + "OR o/data[at0001]/events[at0006]/data[at0003]/items[at0005]/value/magnitude >= 90")]
    [InlineData(
        "SELECT e/ehr_id/value FROM EHR e "
        + "CONTAINS COMPOSITION c[openEHR-EHR-COMPOSITION.administrative_encounter.v1] "
        + "CONTAINS ADMIN_ENTRY admission[openEHR-EHR-ADMIN_ENTRY.admission.v1] "
        + "WHERE NOT EXISTS c/content")]
    [InlineData(
        "SELECT c/name/value AS Name, c/context/start_time AS date_time, "
        + "c/composer/name AS Composer "
        + "FROM EHR e[ehr_id/value = $ehrUid] CONTAINS COMPOSITION c")]
    [InlineData("SELECT count(*) FROM EHR e CONTAINS COMPOSITION c")]
    [InlineData(
        "SELECT max(o/data/events/data/items/value/magnitude) AS maxValue "
        + "FROM EHR e CONTAINS OBSERVATION o")]
    [InlineData(
        "SELECT c FROM EHR e CONTAINS COMPOSITION c "
        + "ORDER BY c/context/start_time ASC, c/uid/value DESC")]
    [InlineData(
        "SELECT c FROM EHR e CONTAINS COMPOSITION c "
        + "WHERE c/foo IS NULL OR c/bar IS NOT NULL")]
    public void Worked_examples_parse_and_round_trip(string aql)
    {
        AqlQuery q1 = AqlParser.Parse(aql);
        Assert.NotNull(q1);
        string printed = AqlPrinter.Print(q1);
        AqlQuery q2 = AqlParser.Parse(printed);
        Assert.Equal(q1, q2);
    }

    // ----------------------------------------------------------------
    // Source position tracking.
    // ----------------------------------------------------------------

    [Fact]
    public void Error_reports_known_column()
    {
        // 'FOO' is unknown at the column where the SELECT body starts.
        // SELECT_FROM_... has SELECT at col 1, ' ' at col 7, then
        // FROM at col 8: column count well-defined.
        AqlParseException ex = Assert.Throws<AqlParseException>(() =>
            AqlParser.Parse("SELECT FROM EHR e"));
        Assert.Equal(1, ex.Line);
        Assert.Equal(8, ex.Column);
    }

    [Fact]
    public void Error_on_second_line_reports_correct_line()
    {
        // Newline between SELECT and the (invalid) body.
        AqlParseException ex = Assert.Throws<AqlParseException>(() =>
            AqlParser.Parse("SELECT c FROM EHR e\nORDER c/x"));
        Assert.Equal(2, ex.Line);
    }

    [Fact]
    public void Unexpected_trailing_tokens_error_at_column()
    {
        AqlParseException ex = Assert.Throws<AqlParseException>(() =>
            AqlParser.Parse("SELECT c FROM EHR e BOGUS"));
        Assert.Equal(1, ex.Line);
        Assert.Equal(21, ex.Column);
    }
}
