namespace DotnetOpenEhr.Aql.Lexer;

/// <summary>
/// A single lexed AQL token. <see cref="Span"/> points back into the
/// original source so callers can materialise the lexeme without an
/// allocation. <see cref="Value"/> holds the post-decoding string for
/// string / identifier / code tokens (escapes resolved, surrounding
/// brackets stripped). <see cref="EmbeddedNodeId"/> is populated only
/// for <see cref="AqlTokenKind.PathSegment"/> tokens that carry an
/// inline <c>[idN]</c> / <c>[atN]</c> / <c>[acN]</c> predicate, so the
/// parser does not need to re-scan the segment.
/// </summary>
public readonly ref struct AqlToken
{
    public AqlToken(
        AqlTokenKind kind,
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

    public AqlTokenKind Kind { get; }
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
