namespace DotnetOpenEhr.Aql;

/// <summary>
/// Raised by <see cref="Paths.ArchetypePathParser"/> (and the public
/// <see cref="ArchetypePath.Parse(System.ReadOnlySpan{char})"/> entry
/// points) on malformed archetype-path input. Archetype paths are
/// single-line, so we carry only a 1-based character
/// <see cref="Position"/> into the source span.
/// </summary>
public sealed class ArchetypePathParseException : Exception
{
    public ArchetypePathParseException(string message, int position)
        : base($"Archetype path parse error at position {position}: {message}")
    {
        Position = position;
    }

    public int Position { get; }
}
