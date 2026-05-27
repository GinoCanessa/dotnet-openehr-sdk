using DotnetOpenEhr.Foundation;

namespace DotnetOpenEhr.Odin.Values;

/// <summary>
/// ODIN coded term scalar (spec 7.3.2). Represents the
/// <c>[terminology_id::code]</c> bracketed form. ADL2-style local codes
/// such as <c>[at0001]</c> (no terminology id) are parsed with the
/// reserved <see cref="LocalTerminologyId"/> placeholder so the round-trip
/// is loss-less; see the writer for emission rules.
/// </summary>
public sealed class OdinTerminologyCode : OdinValue
{
    /// <summary>
    /// Sentinel terminology id used by the parser for ADL2-style
    /// <c>[at0001]</c> bracketed local codes that omit the
    /// <c>terminology::</c> prefix.
    /// </summary>
    public const string LocalTerminologyId = "local";

    public OdinTerminologyCode(TerminologyCode value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    public TerminologyCode Value { get; set; }

    /// <summary>
    /// True if this code was parsed from the bare-bracket ADL2 form
    /// (<c>[at0001]</c>), where the terminology id was synthesized as
    /// <see cref="LocalTerminologyId"/>.
    /// </summary>
    public bool IsLocalForm => string.Equals(Value.TerminologyId, LocalTerminologyId, StringComparison.Ordinal);

    public override OdinKind Kind => OdinKind.TerminologyCode;
}
