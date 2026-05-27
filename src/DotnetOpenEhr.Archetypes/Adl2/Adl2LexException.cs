namespace DotnetOpenEhr.Archetypes.Adl2;

/// <summary>
/// Raised by <see cref="Adl2Lexer"/> on malformed input. Carries
/// 1-based <see cref="Line"/> and <see cref="Column"/> for diagnostics.
/// </summary>
public sealed class Adl2LexException : Exception
{
    public Adl2LexException(string message, int line, int column)
        : base($"ADL2 lex error at line {line}, column {column}: {message}")
    {
        Line = line;
        Column = column;
    }

    public int Line { get; }
    public int Column { get; }
}
