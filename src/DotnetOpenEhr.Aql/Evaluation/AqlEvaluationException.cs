namespace DotnetOpenEhr.Aql.Evaluation;

/// <summary>
/// Exception type thrown by <see cref="AqlEvaluator"/> when a parsed
/// AQL query cannot be evaluated against the supplied source — for
/// example, an unknown function name, a missing parameter binding, or
/// a path step that requires reflection over an unsupported RM type.
/// </summary>
public sealed class AqlEvaluationException : Exception
{
    public AqlEvaluationException(string message) : base(message) { }

    public AqlEvaluationException(string message, Exception inner) : base(message, inner) { }
}
