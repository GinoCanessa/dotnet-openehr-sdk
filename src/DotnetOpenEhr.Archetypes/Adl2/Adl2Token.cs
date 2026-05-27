namespace DotnetOpenEhr.Archetypes.Adl2;

/// <summary>
/// A single lexed ADL2 token. <see cref="Span"/> points back into the
/// original source so callers can materialise the lexeme without an
/// allocation. <see cref="Value"/> holds the post-decoding string for
/// string / regex / code / HRID tokens (escapes resolved, bracket
/// noise stripped) and is <c>null</c> when the lexeme is the verbatim
/// source slice. <see cref="EmbeddedNodeId"/> is populated only for
/// <see cref="Adl2TokenKind.PathSegment"/> tokens that carry an inline
/// <c>[idN]</c> predicate, so the parser does not need to re-scan the
/// segment.
/// </summary>
public readonly ref struct Adl2Token
{
    public Adl2Token(
        Adl2TokenKind kind,
        ReadOnlySpan<char> span,
        int start,
        int length,
        int line,
        int column,
        string? value = null,
        string? embeddedNodeId = null)
    {
        Kind = kind;
        Span = span;
        Start = start;
        Length = length;
        Line = line;
        Column = column;
        Value = value;
        EmbeddedNodeId = embeddedNodeId;
    }

    public Adl2TokenKind Kind { get; }
    public ReadOnlySpan<char> Span { get; }
    public int Start { get; }
    public int Length { get; }
    public int Line { get; }
    public int Column { get; }
    public string? Value { get; }
    public string? EmbeddedNodeId { get; }

    /// <summary>
    /// Materialised lexeme. Returns <see cref="Value"/> when present,
    /// otherwise the verbatim source slice.
    /// </summary>
    public string Text => Value ?? Span.ToString();
}
