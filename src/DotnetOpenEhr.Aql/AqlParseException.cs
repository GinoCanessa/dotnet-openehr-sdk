namespace DotnetOpenEhr.Aql;

/// <summary>
/// Raised by <see cref="AqlParser"/> on malformed input. Carries the
/// 1-based <see cref="Line"/> / <see cref="Column"/> of the offending
/// token.
/// </summary>
public sealed class AqlParseException : Exception
{
    public AqlParseException(string message, int line, int column)
        : base($"AQL parse error at line {line}, column {column}: {message}")
    {
        Line = line;
        Column = column;
    }

    public int Line { get; }
    public int Column { get; }
}
