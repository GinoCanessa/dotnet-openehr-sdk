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

    /// <summary>
    /// A <c>DvText</c>-shaped value violated the regex pattern declared
    /// in a <c>CString.Pattern</c> constraint.
    /// </summary>
    public const string StringPatternViolation = "STRING_001_PATTERN_VIOLATION";

    /// <summary>
    /// A <c>DvText</c>-shaped value is not present in the
    /// <c>CString.EnumeratedValues</c> allowed list.
    /// </summary>
    public const string StringNotInEnumeration = "STRING_002_NOT_IN_ENUMERATION";

    /// <summary>
    /// A numeric value (integer, real or duration component) falls
    /// outside the <c>Range</c> interval declared on the matching
    /// primitive constraint.
    /// </summary>
    public const string NumericOutOfRange = "NUMERIC_001_OUT_OF_RANGE";

    /// <summary>
    /// A numeric value is not a member of the
    /// <c>EnumeratedValues</c> list declared on the matching primitive
    /// constraint.
    /// </summary>
    public const string NumericNotInEnumeration = "NUMERIC_002_NOT_IN_ENUMERATION";

    /// <summary>
    /// A date / time / date-time / duration lexical value did not match
    /// the partial-precision pattern declared on the matching primitive
    /// constraint.
    /// </summary>
    public const string DateTimePatternViolation = "DATETIME_001_PATTERN_VIOLATION";

    /// <summary>
    /// A <c>DvQuantity.Units</c> string did not match any of the
    /// permitted units in the <c>CDvQuantity</c>-shaped constraint.
    /// </summary>
    public const string QuantityWrongUnits = "QUANTITY_001_WRONG_UNITS";

    /// <summary>
    /// A <c>DvQuantity.Magnitude</c> fell outside the matching
    /// <c>CQuantityItem.Magnitude</c> interval.
    /// </summary>
    public const string QuantityMagnitudeOutOfRange = "QUANTITY_002_MAGNITUDE_OUT_OF_RANGE";

    /// <summary>
    /// A <c>DvQuantity.Precision</c> fell outside the matching
    /// <c>CQuantityItem.Precision</c> interval.
    /// </summary>
    public const string QuantityPrecisionOutOfRange = "QUANTITY_003_PRECISION_OUT_OF_RANGE";

    /// <summary>
    /// A <c>DvOrdinal.Value</c> is not a member of the declared
    /// <c>CDvOrdinal</c> item set (or, in CComplexObject form, the
    /// <c>value</c>-attribute <c>CInteger.EnumeratedValues</c> list).
    /// </summary>
    public const string OrdinalNotInSet = "ORDINAL_001_NOT_IN_SET";

    /// <summary>
    /// A <c>DvCodedText.DefiningCode</c> code is not a member of the
    /// intra-template value set referenced by the matching
    /// <c>CTerminologyCode.ValueSetRef</c>.
    /// </summary>
    public const string CodeNotInValueSet = "TERM_001_CODE_NOT_IN_VALUE_SET";

    /// <summary>
    /// The matching <c>CTerminologyCode</c> declares a binding to an
    /// external terminology (SNOMED CT, LOINC, …) that this validator
    /// cannot resolve. Emitted as <see cref="ValidationSeverity.NotValidated"/>
    /// — neither pass nor fail.
    /// </summary>
    public const string BindingNotResolved = "TERM_002_BINDING_NOT_RESOLVED";
}
