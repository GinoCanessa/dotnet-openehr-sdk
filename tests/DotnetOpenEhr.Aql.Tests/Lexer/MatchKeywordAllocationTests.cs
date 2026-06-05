using DotnetOpenEhr.Aql.Ast;
using DotnetOpenEhr.Aql.Lexer;
using Xunit;

namespace DotnetOpenEhr.Aql.Tests.Lexer;

/// <summary>
/// M9 — verifies the keyword dispatch table is allocation-free per call.
/// Marked with <c>[Collection("AllocationTests")]</c> so the
/// <see cref="GC.GetAllocatedBytesForCurrentThread"/> measurement is
/// not perturbed by another test running on the same thread.
/// </summary>
[Collection(AllocationTestsCollection.Name)]
public sealed class MatchKeywordAllocationTests
{
    [Fact]
    public void MatchKeyword_DoesNotAllocate()
    {
        // Pre-allocated probe inputs (mix of hits + a miss + mixed casing).
        string[] inputs =
        [
            "SELECT", "from", "Where", "ContainS", "ORDER", "BY", "Limit",
            "OFFSET", "EHR", "COMPOSITION", "and", "OR", "NoT", "EXISTS",
            "matches", "LIKE", "is", "NULL", "TRUE", "false", "ASC",
            "ASCENDING", "DESC", "DESCENDING", "AS", "DISTINCT", "TOP",
            "BACKWARD", "FORWARD",
            "not_a_keyword",
        ];

        // Warm-up — JIT, frozen-dictionary cold path, any static
        // initializer that allocates on first use.
        for (int i = 0; i < 1000; i++)
        {
            foreach (string input in inputs)
            {
                _ = AqlLexer.MatchKeyword(input);
            }
        }

        // Measure.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        const int N = 1000;
        for (int i = 0; i < N; i++)
        {
            foreach (string input in inputs)
            {
                _ = AqlLexer.MatchKeyword(input);
            }
        }
        long after = GC.GetAllocatedBytesForCurrentThread();
        long delta = after - before;

        Assert.True(
            delta == 0,
            $"MatchKeyword allocated {delta} bytes over {N * inputs.Length} calls.");
    }
}
