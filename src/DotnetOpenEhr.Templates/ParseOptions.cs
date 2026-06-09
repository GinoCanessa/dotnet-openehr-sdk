namespace DotnetOpenEhr.Templates;

/// <summary>
/// Options that tweak the behaviour of <see cref="Opt14XmlParser"/>.
/// </summary>
/// <remarks>
/// V1 exposes exactly one knob: <see cref="Lenient"/>. Additional public
/// surface (e.g. an <c>OnIssue</c> callback for in-process diagnostics)
/// is deferred until a real downstream caller drives the requirement.
/// </remarks>
public sealed class ParseOptions
{
    /// <summary>
    /// When <see langword="false"/> (the default), the parser is
    /// strict: it throws on unknown element names, on
    /// <c>xsi:type</c> discriminators it does not recognise, on the
    /// root element being neither <c>{http://schemas.openehr.org/v1}template</c>
    /// nor <c>{http://schemas.openehr.org/v1}operational_template</c>,
    /// and on documents missing the canonical
    /// <c>http://schemas.openehr.org/v1</c> namespace declaration.
    /// <para>
    /// When <see langword="true"/>, the parser broadens its tolerance
    /// in two narrowly-scoped ways:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <description>Element-name lookup falls back to
    ///     local-name matching when the canonical-namespace lookup
    ///     misses, so documents that drop or remap the openEHR
    ///     namespace still parse.</description>
    ///   </item>
    ///   <item>
    ///     <description>Unknown elements and unknown <c>xsi:type</c>
    ///     discriminators are reported to the internal issue sink
    ///     and skipped instead of raising.</description>
    ///   </item>
    /// </list>
    /// <para>
    /// Lenient mode **never silently drops terminology entries** —
    /// every <c>&lt;term_definitions&gt;</c> child encountered during
    /// the harvest still has to land in either
    /// <see cref="OperationalTemplate.Terminology"/> or
    /// <see cref="OperationalTemplate.ComponentTerminologies"/>. The
    /// value-preservation invariant runs in both modes; failures are
    /// raised regardless of <see cref="Lenient"/>.
    /// </para>
    /// </summary>
    public bool Lenient { get; init; }
}
