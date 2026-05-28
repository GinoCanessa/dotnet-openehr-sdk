using System.Text.RegularExpressions;
using DotnetOpenEhr.Aql.Ast;
using DotnetOpenEhr.Aql.Evaluation;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Support;
using Xunit;

namespace DotnetOpenEhr.Aql.Tests.Evaluation;

/// <summary>
/// Tree-walking evaluator coverage: FROM/CONTAINS source binding,
/// path navigation (including <c>[atN]</c> node-id predicates),
/// WHERE filtering with three-valued logic, projection / DISTINCT,
/// EXISTS / MATCHES / LIKE, parameter binding, function calls, and
/// cancellation.
/// </summary>
public class CoreEvaluatorTests
{
    private static readonly AqlEvaluator Evaluator = new();

    private static List<Composition> ThreeNamedCompositions()
    {
        return
        [
            CompositionBuilder.NewComposition("Vital Signs", "uid-1", context: CompositionBuilder.NewContext()),
            CompositionBuilder.NewComposition("Encounter Note", "uid-2", context: CompositionBuilder.NewContext()),
            CompositionBuilder.NewComposition("Vital Signs", "uid-3"),
        ];
    }

    // ----------------------------------------------------------------
    // 1. SELECT c FROM EHR e CONTAINS COMPOSITION c → 1 row per Composition.
    // ----------------------------------------------------------------
    [Fact]
    public void Select_composition_alias_yields_one_row_per_composition()
    {
        AqlQuery q = AqlParser.Parse("SELECT c FROM EHR e CONTAINS COMPOSITION c");
        List<Composition> comps = ThreeNamedCompositions();

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(q, comps, ct: TestContext.Current.CancellationToken);

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.IsType<Composition>(r[0]));
    }

    // ----------------------------------------------------------------
    // 2. SELECT c/name/value → 3 string rows.
    // ----------------------------------------------------------------
    [Fact]
    public void Select_path_returns_scalar_strings()
    {
        AqlQuery q = AqlParser.Parse("SELECT c/name/value FROM EHR e CONTAINS COMPOSITION c");
        List<Composition> comps = ThreeNamedCompositions();

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(q, comps, ct: TestContext.Current.CancellationToken);

        Assert.Equal(3, rows.Count);
        Assert.Equal(["Vital Signs", "Encounter Note", "Vital Signs"], rows.Select(r => (string?)r[0]));
    }

    // ----------------------------------------------------------------
    // 3. WHERE c/name/value = 'Vital Signs' → only matching rows.
    // ----------------------------------------------------------------
    [Fact]
    public void Where_string_equality_filters_rows()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c WHERE c/name/value = 'Vital Signs'");
        List<Composition> comps = ThreeNamedCompositions();

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(q, comps, ct: TestContext.Current.CancellationToken);

        Assert.Equal(2, rows.Count);
        Assert.Equal(["uid-1", "uid-3"], rows.Select(r => (string?)r[0]));
    }

    // ----------------------------------------------------------------
    // 4. CONTAINS OBSERVATION o[openEHR-EHR-OBSERVATION.blood_pressure.v2]
    //    matches only Compositions whose Observation has that archetype id.
    // ----------------------------------------------------------------
    [Fact]
    public void Contains_with_archetype_predicate_filters_by_node_id()
    {
        Composition bp = CompositionBuilder.NewComposition(
            "BP Composition", "bp-1",
            content:
            [
                CompositionBuilder.NewBloodPressure(
                    "openEHR-EHR-OBSERVATION.blood_pressure.v2",
                    140, "mm[Hg]", 90, "mm[Hg]"),
            ]);
        Composition other = CompositionBuilder.NewComposition(
            "Other", "other-1",
            content:
            [
                CompositionBuilder.NewBloodPressure(
                    "openEHR-EHR-OBSERVATION.blood_pressure.v1",
                    120, "mm[Hg]", 80, "mm[Hg]"),
            ]);

        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c "
            + "CONTAINS OBSERVATION o[openEHR-EHR-OBSERVATION.blood_pressure.v2]");

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(q, [bp, other], ct: TestContext.Current.CancellationToken);

        Assert.Single(rows);
        Assert.Equal("bp-1", rows[0][0]);
    }

    // ----------------------------------------------------------------
    // 5. Path with [atN] predicates returns the systolic magnitude.
    // ----------------------------------------------------------------
    [Fact]
    public void Path_with_at_code_predicates_returns_systolic_magnitude()
    {
        Composition bp = CompositionBuilder.NewComposition(
            "BP", "bp",
            content:
            [
                CompositionBuilder.NewBloodPressure(
                    "openEHR-EHR-OBSERVATION.blood_pressure.v2",
                    142, "mm[Hg]", 88, "mm[Hg]"),
            ]);

        AqlQuery q = AqlParser.Parse(
            "SELECT o/data[at0001]/events[at0006]/data[at0003]/items[at0004]/value/magnitude "
            + "FROM EHR e CONTAINS COMPOSITION c CONTAINS OBSERVATION o");

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(q, [bp], ct: TestContext.Current.CancellationToken);

        Assert.Single(rows);
        Assert.Equal(142.0, (double)rows[0][0]!);
    }

    // ----------------------------------------------------------------
    // 6. WHERE on a path that never resolves returns 0 rows (3VL).
    // ----------------------------------------------------------------
    [Fact]
    public void Where_missing_path_does_not_pass_three_valued_logic()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e CONTAINS COMPOSITION c WHERE c/no_such_attribute/value > 5");
        List<Composition> comps = ThreeNamedCompositions();

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(q, comps, ct: TestContext.Current.CancellationToken);

        Assert.Empty(rows);
    }

    // ----------------------------------------------------------------
    // 7. Quantity unit-aware comparison: DvQuantity 140 mm[Hg] vs
    //    DvQuantity 16 kPa (= 120 mm[Hg]) → true; vs 20 kPa (= 150 mm[Hg]) → false.
    // ----------------------------------------------------------------
    [Fact]
    public void Quantity_comparison_is_unit_aware()
    {
        // For this test we directly invoke the evaluator with a hand-
        // built AQL query and exercise DvQuantity-vs-DvQuantity via
        // two parallel paths (sys and threshold) inside a Cluster.
        DvQuantity sys = new(140, "mm[Hg]");
        DvQuantity thresholdLow = new(16, "kPa");   // == 120 mm[Hg]
        DvQuantity thresholdHigh = new(20, "kPa");  // == 150 mm[Hg]

        int? cmpLow = Compare(sys, thresholdLow);
        int? cmpHigh = Compare(sys, thresholdHigh);

        Assert.NotNull(cmpLow);
        Assert.True(cmpLow!.Value > 0, "140 mm[Hg] should be greater than 16 kPa (120 mm[Hg])");
        Assert.NotNull(cmpHigh);
        Assert.True(cmpHigh!.Value < 0, "140 mm[Hg] should be less than 20 kPa (150 mm[Hg])");
    }

    /// <summary>
    /// Verifies the public evaluator wraps unit-aware comparison the
    /// same way for path-driven WHERE predicates: filters the BP rows
    /// whose magnitude is greater than 130 mm[Hg].
    /// </summary>
    [Fact]
    public void Where_quantity_magnitude_filters_high_bp()
    {
        Composition high = CompositionBuilder.NewComposition(
            "High BP", "high",
            content:
            [
                CompositionBuilder.NewBloodPressure(
                    "openEHR-EHR-OBSERVATION.blood_pressure.v2",
                    160, "mm[Hg]", 95, "mm[Hg]"),
            ]);
        Composition normal = CompositionBuilder.NewComposition(
            "Normal BP", "normal",
            content:
            [
                CompositionBuilder.NewBloodPressure(
                    "openEHR-EHR-OBSERVATION.blood_pressure.v2",
                    118, "mm[Hg]", 78, "mm[Hg]"),
            ]);

        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c "
            + "CONTAINS OBSERVATION o[openEHR-EHR-OBSERVATION.blood_pressure.v2] "
            + "WHERE o/data[at0001]/events[at0006]/data[at0003]/items[at0004]/value/magnitude > 130");

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(q, [high, normal], ct: TestContext.Current.CancellationToken);

        Assert.Single(rows);
        Assert.Equal("high", rows[0][0]);
    }

    // ----------------------------------------------------------------
    // 8. Multiple columns produce N-wide rows.
    // ----------------------------------------------------------------
    [Fact]
    public void Select_multiple_columns_produces_wide_rows()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value, c/name/value FROM EHR e CONTAINS COMPOSITION c");
        List<Composition> comps = ThreeNamedCompositions();

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(q, comps, ct: TestContext.Current.CancellationToken);

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal(2, r.Length));
        Assert.Equal("uid-1", rows[0][0]);
        Assert.Equal("Vital Signs", rows[0][1]);
    }

    // ----------------------------------------------------------------
    // 9. DISTINCT de-duplicates rows.
    // ----------------------------------------------------------------
    [Fact]
    public void Select_distinct_drops_duplicate_rows()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT DISTINCT c/name/value FROM EHR e CONTAINS COMPOSITION c");
        List<Composition> comps = ThreeNamedCompositions();

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(q, comps, ct: TestContext.Current.CancellationToken);

        Assert.Equal(2, rows.Count);
        HashSet<string?> names = [.. rows.Select(r => (string?)r[0])];
        Assert.Contains("Vital Signs", names);
        Assert.Contains("Encounter Note", names);
    }

    // ----------------------------------------------------------------
    // 10. MATCHES with a value set.
    // ----------------------------------------------------------------
    [Fact]
    public void Where_matches_value_set_returns_matching_rows()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c "
            + "WHERE c/name/value MATCHES {'Vital Signs', 'BP'}");
        List<Composition> comps = ThreeNamedCompositions();

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(q, comps, ct: TestContext.Current.CancellationToken);

        Assert.Equal(2, rows.Count);
        Assert.Equal(["uid-1", "uid-3"], rows.Select(r => (string?)r[0]));
    }

    // ----------------------------------------------------------------
    // 11. EXISTS filters by presence of the navigated value.
    // ----------------------------------------------------------------
    [Fact]
    public void Where_exists_filters_by_presence()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c WHERE EXISTS c/context");
        List<Composition> comps = ThreeNamedCompositions();

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(q, comps, ct: TestContext.Current.CancellationToken);

        // The third Composition was constructed without a context.
        Assert.Equal(2, rows.Count);
        Assert.Equal(["uid-1", "uid-2"], rows.Select(r => (string?)r[0]));
    }

    // ----------------------------------------------------------------
    // 12. Parameter binding via $name.
    // ----------------------------------------------------------------
    [Fact]
    public void Parameter_binding_substitutes_dollar_placeholder()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/name/value FROM EHR e CONTAINS COMPOSITION c WHERE c/uid/value = $cid");
        List<Composition> comps = ThreeNamedCompositions();
        Dictionary<string, object?> p = new() { ["cid"] = "uid-2" };

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(q, comps, p, TestContext.Current.CancellationToken);

        Assert.Single(rows);
        Assert.Equal("Encounter Note", rows[0][0]);
    }

    [Fact]
    public void Parameter_binding_missing_throws()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e CONTAINS COMPOSITION c WHERE c/uid/value = $cid");

        Assert.Throws<AqlEvaluationException>(() =>
            Evaluator.Evaluate(q, ThreeNamedCompositions(), ct: TestContext.Current.CancellationToken));
    }

    // ----------------------------------------------------------------
    // 13. Cancellation triggers OperationCanceledException promptly.
    // ----------------------------------------------------------------
    [Fact]
    public void Cancellation_throws_promptly_mid_iteration()
    {
        AqlQuery q = AqlParser.Parse("SELECT c FROM EHR e CONTAINS COMPOSITION c");
        using CancellationTokenSource cts = new();

        IEnumerable<Composition> Source()
        {
            for (int i = 0; i < 1_000_000; i++)
            {
                if (i == 5) cts.Cancel();
                yield return CompositionBuilder.NewComposition($"C{i}", $"u{i}");
            }
        }

        Assert.Throws<OperationCanceledException>(() => Evaluator.Evaluate(q, Source(), cts.Token));
    }

    // ----------------------------------------------------------------
    // 14. LIKE pattern with % and _ wildcards.
    // ----------------------------------------------------------------
    [Fact]
    public void Where_like_matches_with_percent_and_underscore_wildcards()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/name/value FROM EHR e CONTAINS COMPOSITION c WHERE c/name/value LIKE 'Vital _ign%'");
        List<Composition> comps = ThreeNamedCompositions();

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(q, comps, ct: TestContext.Current.CancellationToken);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("Vital Signs", r[0]));
    }

    [Fact]
    public void Matches_PathologicalRegex_ThrowsAqlEvaluationException()
    {
        AqlEvaluator evaluator = new(
            new AqlEvaluatorOptions { RegexTimeout = TimeSpan.FromMilliseconds(1) });
        string input = new string('a', 10_000) + "!";
        Composition composition = CompositionBuilder.NewComposition(input, "uid-pathological");
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM EHR e CONTAINS COMPOSITION c WHERE c/name/value MATCHES '^(a+)+$'");

        AqlEvaluationException ex = Assert.Throws<AqlEvaluationException>(
            () => evaluator.Evaluate(q, [composition], ct: TestContext.Current.CancellationToken));

        Assert.Contains("MATCHES", ex.Message, StringComparison.Ordinal);
        Assert.IsType<RegexMatchTimeoutException>(ex.InnerException);
    }

    [Fact]
    public void AqlEvaluatorOptions_RegexTimeout_IsConfigurable()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AqlEvaluator(new AqlEvaluatorOptions { RegexTimeout = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AqlEvaluator(new AqlEvaluatorOptions { RegexTimeout = Regex.InfiniteMatchTimeout }));

        AqlEvaluator evaluator = new(
            new AqlEvaluatorOptions { RegexTimeout = TimeSpan.FromSeconds(1) });
        AqlQuery q = AqlParser.Parse(
            "SELECT c/name/value FROM EHR e CONTAINS COMPOSITION c WHERE c/name/value LIKE 'Vital _ign%'");

        IReadOnlyList<object?[]> rows = evaluator.Evaluate(
            q,
            ThreeNamedCompositions(),
            ct: TestContext.Current.CancellationToken);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void Where_like_UsesTimeoutBoundRegexAcrossManyRows()
    {
        AqlEvaluator evaluator = new(
            new AqlEvaluatorOptions { RegexTimeout = TimeSpan.FromMilliseconds(50) });
        List<Composition> compositions = [];
        for (int i = 0; i < 256; i++)
        {
            string name = i % 2 == 0
                ? $"Batch {i.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                : $"Other {i.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            compositions.Add(CompositionBuilder.NewComposition(name, $"uid-{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
        }
        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c WHERE c/name/value LIKE 'Batch %'");

        IReadOnlyList<object?[]> rows = evaluator.Evaluate(
            q,
            compositions,
            ct: TestContext.Current.CancellationToken);

        Assert.Equal(128, rows.Count);
    }

    // ----------------------------------------------------------------
    // 15. count() over a path that resolves to a collection.
    // ----------------------------------------------------------------
    [Fact]
    public void Function_count_returns_collection_size()
    {
        Composition bp = CompositionBuilder.NewComposition(
            "BP", "bp",
            content:
            [
                CompositionBuilder.NewBloodPressure(
                    "openEHR-EHR-OBSERVATION.blood_pressure.v2",
                    140, "mm[Hg]", 90, "mm[Hg]"),
            ]);

        AqlQuery q = AqlParser.Parse(
            "SELECT count(o/data/events) FROM EHR e CONTAINS COMPOSITION c CONTAINS OBSERVATION o");

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(q, [bp], ct: TestContext.Current.CancellationToken);

        Assert.Single(rows);
        Assert.Equal(1L, (long)rows[0][0]!);
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    /// <summary>
    /// Public surface for verifying unit-aware comparison: routes
    /// through the evaluator by running a one-row WHERE filter and
    /// inspecting whether it produced a row.
    /// </summary>
    private static int? Compare(DvQuantity left, DvQuantity right)
    {
        Composition c = CompositionBuilder.NewComposition(
            "Cmp", "cmp",
            content:
            [
                new Observation
                {
                    ArchetypeNodeId = "openEHR-EHR-OBSERVATION.cmp.v0",
                    Name = new DvText("Cmp"),
                    Language = new CodePhrase(new TerminologyId { Value = "ISO_639-1" }, "en"),
                    Encoding = new CodePhrase(new TerminologyId { Value = "IANA_character-sets" }, "UTF-8"),
                    Subject = new PartySelf(),
                    Data = new History
                    {
                        ArchetypeNodeId = "at0001",
                        Name = new DvText("History"),
                        Origin = CompositionBuilder.NewContext().StartTime,
                        Events =
                        [
                            new PointEvent
                            {
                                ArchetypeNodeId = "at0006",
                                Name = new DvText("Any event"),
                                Time = CompositionBuilder.NewContext().StartTime,
                                Data = new ItemTree
                                {
                                    ArchetypeNodeId = "at0003",
                                    Name = new DvText("Tree"),
                                    Items =
                                    [
                                        new Element
                                        {
                                            ArchetypeNodeId = "at0010",
                                            Name = new DvText("L"),
                                            Value = left,
                                        },
                                        new Element
                                        {
                                            ArchetypeNodeId = "at0011",
                                            Name = new DvText("R"),
                                            Value = right,
                                        },
                                    ],
                                },
                            },
                        ],
                    },
                },
            ]);

        AqlEvaluator ev = new();
        AqlQuery gt = AqlParser.Parse(
            "SELECT c FROM EHR e CONTAINS COMPOSITION c CONTAINS OBSERVATION o "
            + "WHERE o/data[at0001]/events[at0006]/data[at0003]/items[at0010]/value "
            + "> o/data[at0001]/events[at0006]/data[at0003]/items[at0011]/value");
        AqlQuery lt = AqlParser.Parse(
            "SELECT c FROM EHR e CONTAINS COMPOSITION c CONTAINS OBSERVATION o "
            + "WHERE o/data[at0001]/events[at0006]/data[at0003]/items[at0010]/value "
            + "< o/data[at0001]/events[at0006]/data[at0003]/items[at0011]/value");
        bool greater = ev.Evaluate(gt, [c], ct: TestContext.Current.CancellationToken).Count == 1;
        bool less = ev.Evaluate(lt, [c], ct: TestContext.Current.CancellationToken).Count == 1;
        if (greater) return 1;
        if (less) return -1;
        return null;
    }
}
