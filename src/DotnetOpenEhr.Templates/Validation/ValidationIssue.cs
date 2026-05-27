namespace DotnetOpenEhr.Templates.Validation;

/// <summary>
/// Severity classification for a <see cref="ValidationIssue"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><see cref="Error"/> — the issue means the Composition fails
///   conformance against the template.</item>
///   <item><see cref="Warning"/> — the rule emitted a soft signal but
///   the Composition is still considered conformant.</item>
///   <item><see cref="NotValidated"/> — the rule could not be evaluated
///   (typically because external context such as a terminology binding
///   was unavailable); callers should treat such nodes as
///   unverified, neither pass nor fail.</item>
/// </list>
/// </remarks>
public enum ValidationSeverity
{
    /// <summary>The Composition node violates a template rule.</summary>
    Error,

    /// <summary>A soft, non-blocking signal about the Composition node.</summary>
    Warning,

    /// <summary>The rule could not be evaluated against the node.</summary>
    NotValidated,
}

/// <summary>
/// A single finding produced by <see cref="OperationalTemplateValidator"/>:
/// the AQL path of the offending Composition node, the rule that fired,
/// its severity, and a human-readable explanation.
/// </summary>
public sealed record ValidationIssue(
    string Path,
    string RuleId,
    ValidationSeverity Severity,
    string Message);

/// <summary>
/// Canonical rule identifiers emitted by
/// <see cref="OperationalTemplateValidator"/>. Stable across releases
/// so callers can suppress or escalate by id.
/// </summary>
public static class ValidationRuleIds
{
    /// <summary>
    /// A Composition node was encountered that has no matching
    /// <c>CObject</c> at the corresponding position in the template.
    /// </summary>
    public const string NodeNotInTemplate = "STRUCT_001_NODE_NOT_IN_TEMPLATE";

    /// <summary>
    /// The number of children supplied for a
    /// <c>CMultipleAttribute</c> falls outside that attribute's
    /// <c>Cardinality.Interval</c>.
    /// </summary>
    public const string CardinalityViolation = "CARD_001_CARDINALITY_VIOLATION";

    /// <summary>
    /// The number of sibling instances of a given
    /// <c>node_id</c> at the current level falls outside the
    /// <c>CObject.Occurrences</c> interval declared for that node.
    /// </summary>
    public const string OccurrencesViolation = "OCC_001_OCCURRENCES_VIOLATION";
}
