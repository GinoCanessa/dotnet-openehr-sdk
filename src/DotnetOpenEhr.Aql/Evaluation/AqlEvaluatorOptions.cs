namespace DotnetOpenEhr.Aql.Evaluation;

/// <summary>
/// Runtime options for <see cref="AqlEvaluator"/>.
/// </summary>
public sealed class AqlEvaluatorOptions
{
    public static TimeSpan DefaultRegexTimeout { get; } = TimeSpan.FromMilliseconds(250);

    public TimeSpan RegexTimeout { get; init; } = DefaultRegexTimeout;
}
