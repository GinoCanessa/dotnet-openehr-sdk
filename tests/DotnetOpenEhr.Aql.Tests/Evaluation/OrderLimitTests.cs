using System.Runtime.CompilerServices;
using DotnetOpenEhr.Aql.Ast;
using DotnetOpenEhr.Aql.Evaluation;
using DotnetOpenEhr.Rm.Composition;
using Xunit;

namespace DotnetOpenEhr.Aql.Tests.Evaluation;

/// <summary>
/// Coverage for ORDER BY (single + multi-column, null handling),
/// LIMIT / OFFSET slicing, and the <see cref="AqlEvaluator.EvaluateAsync(AqlQuery, IAsyncEnumerable{Composition}, System.Threading.CancellationToken)"/>
/// streaming overload (parity, early-termination, cancellation, and
/// ORDER BY buffering).
/// </summary>
public class OrderLimitTests
{
    private static readonly AqlEvaluator Evaluator = new();

    private static List<Composition> FiveSortable()
    {
        return
        [
            CompositionBuilder.NewComposition("Charlie", "uid-c"),
            CompositionBuilder.NewComposition("Alpha", "uid-a"),
            CompositionBuilder.NewComposition("Echo", "uid-e"),
            CompositionBuilder.NewComposition("Bravo", "uid-b"),
            CompositionBuilder.NewComposition("Delta", "uid-d"),
        ];
    }

    // 1. ORDER BY ... ASC sorts as expected.
    [Fact]
    public void OrderBy_asc_sorts_ascending()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c ORDER BY c/name/value ASC");

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(
            q, FiveSortable(), ct: TestContext.Current.CancellationToken);

        Assert.Equal(
            ["uid-a", "uid-b", "uid-c", "uid-d", "uid-e"],
            rows.Select(r => (string?)r[0]));
    }

    // 2. ORDER BY ... DESC reverses.
    [Fact]
    public void OrderBy_desc_sorts_descending()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c ORDER BY c/name/value DESC");

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(
            q, FiveSortable(), ct: TestContext.Current.CancellationToken);

        Assert.Equal(
            ["uid-e", "uid-d", "uid-c", "uid-b", "uid-a"],
            rows.Select(r => (string?)r[0]));
    }

    // 3. Multi-column ORDER BY: primary tie broken by secondary.
    [Fact]
    public void OrderBy_multi_column_ties_resolved_by_secondary()
    {
        List<Composition> comps =
        [
            CompositionBuilder.NewComposition("Same", "uid-3"),
            CompositionBuilder.NewComposition("Same", "uid-1"),
            CompositionBuilder.NewComposition("Other", "uid-9"),
            CompositionBuilder.NewComposition("Same", "uid-2"),
        ];

        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c "
            + "ORDER BY c/name/value ASC, c/uid/value ASC");

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(
            q, comps, ct: TestContext.Current.CancellationToken);

        // "Other" < "Same" so the lone Other row comes first; the
        // three "Same" rows then break the tie by uid asc.
        Assert.Equal(
            ["uid-9", "uid-1", "uid-2", "uid-3"],
            rows.Select(r => (string?)r[0]));
    }

    // 4. LIMIT 2 OFFSET 1 over a 5-row projection returns rows 2-3.
    [Fact]
    public void Limit_offset_returns_window()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c "
            + "ORDER BY c/name/value ASC LIMIT 2 OFFSET 1");

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(
            q, FiveSortable(), ct: TestContext.Current.CancellationToken);

        Assert.Equal(["uid-b", "uid-c"], rows.Select(r => (string?)r[0]));
    }

    // 5. LIMIT 0 returns empty.
    [Fact]
    public void Limit_zero_returns_empty()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c LIMIT 0");

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(
            q, FiveSortable(), ct: TestContext.Current.CancellationToken);

        Assert.Empty(rows);
    }

    // 6. OFFSET past the row count returns empty.
    [Fact]
    public void Offset_past_row_count_returns_empty()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c OFFSET 99");

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(
            q, FiveSortable(), ct: TestContext.Current.CancellationToken);

        Assert.Empty(rows);
    }

    // 7. ORDER BY with null values: nulls sort last on ASC.
    [Fact]
    public void OrderBy_nulls_sort_last_on_asc()
    {
        List<Composition> comps =
        [
            CompositionBuilder.NewComposition("with-ctx-a", "uid-a", context: CompositionBuilder.NewContext()),
            CompositionBuilder.NewComposition("no-ctx-1", "uid-n1"),
            CompositionBuilder.NewComposition("with-ctx-b", "uid-b", context: CompositionBuilder.NewContext()),
            CompositionBuilder.NewComposition("no-ctx-2", "uid-n2"),
        ];

        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c "
            + "ORDER BY c/context/start_time/value ASC, c/uid/value ASC");

        IReadOnlyList<object?[]> rows = Evaluator.Evaluate(
            q, comps, ct: TestContext.Current.CancellationToken);

        // The two with-context rows have identical start_times so the
        // secondary key (uid asc) orders them; the two null-context
        // rows trail (asc null-last), again uid-asc-ordered.
        Assert.Equal(
            ["uid-a", "uid-b", "uid-n1", "uid-n2"],
            rows.Select(r => (string?)r[0]));
    }

    // 8. EvaluateAsync parity with synchronous overload.
    [Fact]
    public async Task EvaluateAsync_matches_sync_results()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c "
            + "ORDER BY c/name/value ASC LIMIT 3 OFFSET 1");
        List<Composition> comps = FiveSortable();

        IReadOnlyList<object?[]> syncRows = Evaluator.Evaluate(
            q, comps, ct: TestContext.Current.CancellationToken);
        List<object?[]> asyncRows = await ToListAsync(
            Evaluator.EvaluateAsync(
                q,
                ToAsyncEnumerable(comps, TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(syncRows.Count, asyncRows.Count);
        for (int i = 0; i < syncRows.Count; i++)
        {
            Assert.Equal(syncRows[i][0], asyncRows[i][0]);
        }
    }

    // 9. EvaluateAsync with LIMIT stops pulling from the source early.
    [Fact]
    public async Task EvaluateAsync_with_limit_short_circuits_source()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c LIMIT 3");
        CountingAsyncSource counting = new(FiveSortable());

        List<object?[]> rows = await ToListAsync(
            Evaluator.EvaluateAsync(q, counting.Enumerate(TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(3, rows.Count);
        // We needed only 3 of the 5 compositions to fill the limit;
        // the iterator should never have requested the rest.
        Assert.Equal(3, counting.YieldedCount);
    }

    // 10. EvaluateAsync cancellation throws OperationCanceledException.
    [Fact]
    public async Task EvaluateAsync_cancellation_throws_promptly()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c");
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (object?[] _ in Evaluator.EvaluateAsync(
                q, BlockingAfter(FiveSortable()[0], cts, cts.Token), cts.Token))
            {
                // Trigger cancellation after the first row is observed.
                cts.Cancel();
            }
        });
    }

    // 11. EvaluateAsync with ORDER BY buffers the stream before yielding.
    [Fact]
    public async Task EvaluateAsync_orderby_buffers_then_yields_sorted()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c/uid/value FROM EHR e CONTAINS COMPOSITION c ORDER BY c/name/value ASC");
        CountingAsyncSource counting = new(FiveSortable());

        List<object?[]> rows = await ToListAsync(
            Evaluator.EvaluateAsync(q, counting.Enumerate(TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ["uid-a", "uid-b", "uid-c", "uid-d", "uid-e"],
            rows.Select(r => (string?)r[0]));
        // Documented buffering: every source item is pulled before the
        // first row is yielded.
        Assert.Equal(5, counting.YieldedCount);
    }

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source, CancellationToken ct)
    {
        List<T> result = [];
        await foreach (T item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            result.Add(item);
        }
        return result;
    }

    // ----------------------------------------------------------------
    // Test helpers: minimal inline IAsyncEnumerable adapters.
    // ----------------------------------------------------------------

    private static async IAsyncEnumerable<Composition> ToAsyncEnumerable(
        IEnumerable<Composition> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (Composition c in source)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return c;
        }
    }

    private sealed class CountingAsyncSource
    {
        private readonly IReadOnlyList<Composition> _items;
        public int YieldedCount { get; private set; }

        public CountingAsyncSource(IReadOnlyList<Composition> items) { _items = items; }

        public async IAsyncEnumerable<Composition> Enumerate(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (Composition c in _items)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
                YieldedCount++;
                yield return c;
            }
        }
    }

    private static async IAsyncEnumerable<Composition> BlockingAfter(
        Composition first,
        CancellationTokenSource cts,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return first;
        // Wait for cancellation; the consumer cancels after seeing the
        // first row so this should observe the cancellation and throw.
        TaskCompletionSource tcs = new();
        using (cts.Token.Register(static s => ((TaskCompletionSource)s!).TrySetResult(), tcs))
        using (ct.Register(static s => ((TaskCompletionSource)s!).TrySetResult(), tcs))
        {
            await tcs.Task.ConfigureAwait(false);
        }
        ct.ThrowIfCancellationRequested();
        cts.Token.ThrowIfCancellationRequested();
        yield break;
    }
}
