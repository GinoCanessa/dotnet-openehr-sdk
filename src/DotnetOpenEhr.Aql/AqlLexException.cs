namespace DotnetOpenEhr.Aql;

/// <summary>
/// Raised by <see cref="Lexer.AqlLexer"/> on malformed input. Carries
/// 1-based <see cref="Line"/> and <see cref="Column"/> for diagnostics.
/// </summary>
public sealed class AqlLexException : Exception
{
    public AqlLexException(string message, int line, int column)
        : base($"AQL lex error at line {line}, column {column}: {message}")
    {
        Line = line;
        Column = column;
    }

    public int Line { get; }
    public int Column { get; }
}
