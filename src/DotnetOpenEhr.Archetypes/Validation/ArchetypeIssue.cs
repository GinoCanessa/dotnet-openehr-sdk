namespace DotnetOpenEhr.Archetypes.Validation;

/// <summary>
/// Severity classification for an <see cref="ArchetypeIssue"/>.
/// </summary>
public enum ArchetypeIssueSeverity
{
    /// <summary>Informational only — no action required.</summary>
    Info,

    /// <summary>A non-fatal deviation from the model; the archetype is still usable.</summary>
    Warning,

    /// <summary>A genuine conformance failure that breaks model semantics.</summary>
    Error,
}

/// <summary>
/// A single issue raised by a validator while inspecting an
/// <see cref="DotnetOpenEhr.Archetypes.Aom2.Archetype"/>.
/// </summary>
/// <param name="Severity">Classification of how serious the issue is.</param>
/// <param name="Path">
/// ADL-style dotted/slashed path to the offending node, e.g.
/// <c>/data[id3]/events[id4]/data[id2]/items[id5]</c>. The root path is
/// <c>/</c>.
/// </param>
/// <param name="Code">
/// Stable issue code (see <see cref="ArchetypeIssueCodes"/>). Suitable for
/// tooling to filter on.
/// </param>
/// <param name="Message">Human-readable description of the problem.</param>
public sealed record ArchetypeIssue(
    ArchetypeIssueSeverity Severity,
    string Path,
    string Code,
    string Message);

/// <summary>
/// Stable string constants for every <see cref="ArchetypeIssue.Code"/>
/// produced by the validators in this namespace.
/// </summary>
public static class ArchetypeIssueCodes
{
    /// <summary>
    /// A <c>C_COMPLEX_OBJECT.rm_type_name</c> does not name any class in
    /// the supplied RM BMM.
    /// </summary>
    public const string UnknownRmType = "BMM_001_UNKNOWN_RM_TYPE";

    /// <summary>
    /// A <c>C_ATTRIBUTE.rm_attribute_name</c> is not declared (directly
    /// or inherited) on the enclosing class in the supplied RM BMM.
    /// </summary>
    public const string UnknownAttribute = "BMM_002_UNKNOWN_ATTRIBUTE";

    /// <summary>
    /// A <c>C_PRIMITIVE_OBJECT</c> constraint's kind (CString, CInteger,
    /// …) does not match the BMM-declared primitive type of the parent
    /// attribute.
    /// </summary>
    public const string TypeMismatch = "BMM_003_TYPE_MISMATCH";

    /// <summary>
    /// A child <c>C_COMPLEX_OBJECT</c>'s <c>rm_type_name</c> is not
    /// assignment-compatible with the BMM-declared element type of the
    /// parent attribute (container / generic root). Warning-only; rule
    /// is best-effort and only fires when generic resolution is
    /// tractable from the static BMM.
    /// </summary>
    public const string GenericParamMismatch = "BMM_004_GENERIC_PARAM_MISMATCH";
}
