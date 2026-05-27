namespace DotnetOpenEhr.Odin;

/// <summary>
/// Thrown by <see cref="OdinParser"/> on malformed input. Carries the
/// 1-based source line and column where the error was detected, plus an
/// optional snippet of the surrounding source for diagnostic output.
/// </summary>
public sealed class OdinParseException : Exception
{
    public OdinParseException(string message, int line, int column, string? snippet = null)
        : base(FormatMessage(message, line, column, snippet))
    {
        Line = line;
        Column = column;
        Snippet = snippet;
    }

    public int Line { get; }
    public int Column { get; }
    public string? Snippet { get; }

    private static string FormatMessage(string message, int line, int column, string? snippet)
        => snippet is null
            ? $"ODIN parse error at line {line}, column {column}: {message}"
            : $"ODIN parse error at line {line}, column {column}: {message} (near '{snippet}')";
}
