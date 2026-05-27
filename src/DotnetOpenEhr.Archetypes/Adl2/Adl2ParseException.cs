namespace DotnetOpenEhr.Archetypes.Adl2;

/// <summary>
/// Raised by <see cref="Adl2Parser"/> on malformed input. Carries the
/// 1-based <see cref="Line"/> / <see cref="Column"/> of the offending
/// token and, when available, the dotted ADL <see cref="Path"/> of the
/// node being parsed.
/// </summary>
public sealed class Adl2ParseException : Exception
{
    public Adl2ParseException(string message, int line, int column, string? path = null)
        : base($"ADL2 parse error at line {line}, column {column}{(path is null ? "" : $" ({path})")}: {message}")
    {
        Line = line;
        Column = column;
        Path = path;
    }

    public Adl2ParseException(string message, int line, int column, string? path, Exception inner)
        : base($"ADL2 parse error at line {line}, column {column}{(path is null ? "" : $" ({path})")}: {message}", inner)
    {
        Line = line;
        Column = column;
        Path = path;
    }

    public int Line { get; }
    public int Column { get; }
    public string? Path { get; }
}
