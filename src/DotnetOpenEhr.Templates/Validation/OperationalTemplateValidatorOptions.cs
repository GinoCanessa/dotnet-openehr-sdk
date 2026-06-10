using System.Collections.Concurrent;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// Optional caller-supplied regex compile cache. When null
    /// (default), the validator shares a process-global cache with all
    /// other default-options validators in the AppDomain — the same
    /// behaviour shipped before this option existed. When non-null, the
    /// validator uses the supplied dictionary instead, giving callers
    /// (especially tests) a private, observable cache without reaching
    /// into validator internals.
    /// </summary>
    /// <remarks>
    /// Keyed on <c>(Pattern, Timeout)</c> so two validator instances
    /// with different <see cref="RegexMatchTimeout"/> values can share
    /// the same dictionary safely. The dictionary is expected to be
    /// thread-safe; <see cref="ConcurrentDictionary{TKey,TValue}"/> is
    /// the intended type. Successful compiles are inserted via
    /// <c>GetOrAdd</c>; malformed-pattern compiles throw before insert
    /// and so do not poison the cache.
    /// </remarks>
    public ConcurrentDictionary<(string Pattern, TimeSpan Timeout), Regex>? RegexCache { get; init; }
}
