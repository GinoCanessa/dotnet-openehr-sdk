namespace DotnetOpenEhr.Templates.Validation;

/// <summary>
/// Configuration knobs for <see cref="OperationalTemplateValidator"/>.
/// </summary>
/// <remarks>
/// The validator only honours options for behaviour that can be
/// tuned per-instance without changing the validation rules
/// themselves. Today that's the regex match timeout used by
/// <see cref="ValidationRuleIds.StringPatternViolation"/>.
/// </remarks>
public sealed class OperationalTemplateValidatorOptions
{
    /// <summary>
    /// Maximum wall-clock time a single regex match against a
    /// <c>CString.Pattern</c> may take before the validator emits a
    /// <see cref="ValidationSeverity.NotValidated"/> issue. Defaults
    /// to 1 second.
    /// </summary>
    /// <remarks>
    /// Pass <see cref="TimeSpan.Zero"/> to opt out of timeout
    /// enforcement (equivalent to <c>Regex.InfiniteMatchTimeout</c>).
    /// Negative values throw at validator-construction time.
    /// </remarks>
    public TimeSpan RegexMatchTimeout { get; init; } = TimeSpan.FromSeconds(1);
}
