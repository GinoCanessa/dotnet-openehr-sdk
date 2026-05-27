namespace DotnetOpenEhr.Odin;

/// <summary>
/// Options controlling <see cref="OdinWriter"/> output.
/// </summary>
public sealed class OdinWriteOptions
{
    /// <summary>
    /// Default canonical ODIN output: 4-space indent, attribute / hash
    /// entries one-per-line, list items one-per-line, newline at end.
    /// </summary>
    public static OdinWriteOptions Default { get; } = new();

    /// <summary>
    /// Compact one-line output suitable for tests and inline log
    /// messages. Indentation is disabled and entries are separated by a
    /// single space. The output is still valid ODIN.
    /// </summary>
    public static OdinWriteOptions Compact { get; } = new()
    {
        Indent = false,
        InlineLists = true,
    };

    /// <summary>
    /// When true, emit indented multi-line output. Default: true.
    /// </summary>
    public bool Indent { get; init; } = true;

    /// <summary>
    /// Indent unit. Default: four spaces, per the canonical ODIN form.
    /// </summary>
    public string IndentUnit { get; init; } = "    ";

    /// <summary>
    /// When true, list items are written on a single line separated by
    /// ', '. When false, each item goes on its own line. Default: true
    /// (matches the spec's most common form, including
    /// <c>fruits = &lt;"pear", "cumquat"&gt;</c>).
    /// </summary>
    public bool InlineLists { get; init; } = true;

    /// <summary>
    /// Line separator. Default: <see cref="Environment.NewLine"/>.
    /// </summary>
    public string NewLine { get; init; } = "\n";
}
