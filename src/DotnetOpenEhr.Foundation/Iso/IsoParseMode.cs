namespace DotnetOpenEhr.Foundation.Iso;

/// <summary>
/// Controls leniency of <c>Iso*.Parse</c> / <c>TryParse</c> overloads
/// across the Foundation ISO 8601 types.
/// </summary>
/// <remarks>
/// The parameterless overloads default to <see cref="FixAsPossible"/>
/// per the SDK's documented "lenient-input, canonical-output" stance.
/// Wire-input deserialization paths should pass <see cref="Ostrich"/>
/// to preserve the wire's original lexical form for round-trip.
/// Validator paths should pass <see cref="Strict"/> so malformed-but-
/// fixable input is reported rather than silently corrected.
/// </remarks>
public enum IsoParseMode
{
    /// <summary>
    /// Reject any malformed input. Closest to "the spec said no".
    /// In this mode <c>IsoTimeZone</c> rejects minutes not in
    /// {0, 15, 30, 45} and negative timezones with hours &gt; 12.
    /// </summary>
    Strict,

    /// <summary>
    /// Preserve the wire's original lexical form verbatim — do not
    /// normalize, do not fix, do not reject. Suitable for round-trip
    /// scenarios where the SDK is a transparent pipe.
    /// </summary>
    Ostrich,

    /// <summary>
    /// Accept malformed-but-fixable input and normalize it to a
    /// canonical representation. The default for the parameterless
    /// overloads. In this mode <c>IsoTimeZone</c> rounds non-quarter
    /// minutes to the nearest 15-minute boundary and clamps negative
    /// timezones with hours &gt; 12 to <c>-12:00</c>.
    /// </summary>
    FixAsPossible,
}
