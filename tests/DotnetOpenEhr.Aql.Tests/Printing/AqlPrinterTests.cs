using System.Globalization;
using System.Threading;
using DotnetOpenEhr.Aql;
using DotnetOpenEhr.Aql.Ast;
using Xunit;

namespace DotnetOpenEhr.Aql.Tests.Printing;

/// <summary>
/// H10 — characterization tests for <see cref="AqlPrinter"/> covering
/// literal forms, parenthesisation, NOT-of-OR wrapping, and the
/// culture-invariance of real-literal formatting. These pin observable
/// output so a future formatting tweak surfaces as a deliberate test
/// update rather than a silent round-trip drift.
/// </summary>
public sealed class AqlPrinterTests
{
    [Theory]
    [InlineData("SELECT TRUE FROM EHR e", "true")]
    [InlineData("SELECT FALSE FROM EHR e", "false")]
    public void Boolean_literal_prints_lowercase(string source, string expectedToken)
    {
        AqlQuery q = AqlParser.Parse(source);
        string printed = AqlPrinter.Print(q);
        Assert.Contains(expectedToken, printed, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Parens_emitted_when_or_nested_inside_and()
    {
        // (a = 1 OR b = 2) AND c = 3 — the left OR must be parenthesised
        // since AND binds tighter than OR.
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM COMPOSITION c WHERE (c/x = 1 OR c/y = 2) AND c/z = 3");
        string printed = AqlPrinter.Print(q);
        Assert.Contains("(", printed, System.StringComparison.Ordinal);
        Assert.Contains(") AND ", printed, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Not_of_or_wraps_in_parens()
    {
        AqlQuery q = AqlParser.Parse(
            "SELECT c FROM COMPOSITION c WHERE NOT (c/x = 1 OR c/y = 2)");
        string printed = AqlPrinter.Print(q);
        Assert.Contains("NOT (", printed, System.StringComparison.Ordinal);
        Assert.Contains(" OR ", printed, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Real_literal_round_trips_via_R_format_under_de_DE()
    {
        // Reals are emitted via `.ToString("R", CultureInfo.InvariantCulture)`
        // — this must survive a culture that uses ',' as decimal separator.
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            AqlQuery q = AqlParser.Parse("SELECT 1.5 FROM EHR e");
            string printed = AqlPrinter.Print(q);
            // Invariant format uses '.', not ','.
            Assert.Contains("1.5", printed, System.StringComparison.Ordinal);
            Assert.DoesNotContain("1,5", printed, System.StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("SELECT c/x / 2 FROM EHR e", "/")]
    [InlineData("SELECT c FROM EHR e WHERE c/x != 1", "!=")]
    [InlineData("SELECT -1 FROM EHR e", "-1")]
    public void Divide_NotEq_Negate_print_canonical_form(string source, string expectedFragment)
    {
        AqlQuery q = AqlParser.Parse(source);
        string printed = AqlPrinter.Print(q);
        Assert.Contains(expectedFragment, printed, System.StringComparison.Ordinal);
    }
}
