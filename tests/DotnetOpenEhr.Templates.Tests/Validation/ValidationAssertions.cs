using DotnetOpenEhr.Templates.Validation;
using Xunit;

namespace DotnetOpenEhr.Templates.Tests.Validation;

/// <summary>
/// Shared assertion helpers for validator tests. H12 (0604-04) raised
/// every <c>AssertSingleIssue</c> call site from
/// <see cref="DataTypeTests"/> and <see cref="StringPatternHardeningTests"/>
/// to also pin <see cref="ValidationIssue.Path"/> alongside the
/// <see cref="ValidationIssue.RuleId"/>, so a misrouted issue surfaces
/// as a test failure rather than silently passing.
/// </summary>
internal static class ValidationAssertions
{
    /// <summary>
    /// Asserts the issue stream contains exactly one issue with the
    /// given <paramref name="ruleId"/>, the expected
    /// <paramref name="expectedPath"/>, and the expected
    /// <paramref name="severity"/> (defaults to
    /// <see cref="ValidationSeverity.Error"/>).
    /// </summary>
    public static void AssertSingleIssue(
        IReadOnlyList<ValidationIssue> issues,
        string ruleId,
        string expectedPath,
        ValidationSeverity severity = ValidationSeverity.Error)
    {
        ValidationIssue only = Assert.Single(issues, i => i.RuleId == ruleId);
        Assert.Equal(severity, only.Severity);
        Assert.Equal(expectedPath, only.Path);
    }

    /// <summary>
    /// Asserts no issue in the stream carries the given
    /// <paramref name="ruleId"/>.
    /// </summary>
    public static void AssertNoIssue(IReadOnlyList<ValidationIssue> issues, string ruleId)
    {
        Assert.DoesNotContain(issues, i => i.RuleId == ruleId);
    }
}
