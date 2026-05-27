namespace DotnetOpenEhr.Bmm;

/// <summary>
/// Thrown by <see cref="BmmParser"/> on malformed BMM input. Carries the
/// 1-based source line and column where the error was detected and the
/// dotted path of attributes traversed before the failure (for example,
/// <c>class_definitions.PARTY.properties.name.type</c>).
/// </summary>
public sealed class BmmParseException : Exception
{
    public BmmParseException(string message, int line, int column, string? path = null)
        : base(FormatMessage(message, line, column, path))
    {
        Line = line;
        Column = column;
        Path = path;
    }

    public BmmParseException(string message, int line, int column, string? path, Exception innerException)
        : base(FormatMessage(message, line, column, path), innerException)
    {
        Line = line;
        Column = column;
        Path = path;
    }

    public int Line { get; }
    public int Column { get; }
    public string? Path { get; }

    private static string FormatMessage(string message, int line, int column, string? path)
    {
        string head = $"BMM parse error at line {line}, column {column}";
        if (!string.IsNullOrEmpty(path))
        {
            head += $" (path '{path}')";
        }
        return $"{head}: {message}";
    }
}
