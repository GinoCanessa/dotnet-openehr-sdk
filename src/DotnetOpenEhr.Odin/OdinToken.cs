namespace DotnetOpenEhr.Odin;

/// <summary>
/// A single lexed ODIN token. <see cref="Span"/> points back into the
/// original source so callers can materialise the lexeme without an
/// allocation. <see cref="Value"/> holds the post-decoding string for
/// string / character literals (escape sequences resolved), and is null
/// for tokens whose lexeme is the source verbatim.
/// </summary>
public readonly ref struct OdinToken
{
    public OdinToken(
        OdinTokenKind kind,
        ReadOnlySpan<char> span,
        int start,
        int length,
        int line,
        int column,
        string? value = null)
    {
        Kind = kind;
        Span = span;
        Start = start;
        Length = length;
        Line = line;
        Column = column;
        Value = value;
    }

    public OdinTokenKind Kind { get; }
    public ReadOnlySpan<char> Span { get; }
    public int Start { get; }
    public int Length { get; }
    public int Line { get; }
    public int Column { get; }
    public string? Value { get; }

    /// <summary>
    /// Materialised lexeme. Returns <see cref="Value"/> when present
    /// (string / char tokens), otherwise the verbatim source slice.
    /// </summary>
    public string Text => Value ?? Span.ToString();
}
