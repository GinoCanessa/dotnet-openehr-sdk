using System.Xml;

namespace DotnetOpenEhr.Templates;

/// <summary>
/// Raised by <see cref="Opt14XmlParser"/> in strict mode when an OPT1.4
/// document violates the schema or carries constructs that cannot be
/// translated into the AOM2 type system this repo ships today. Extends
/// <see cref="InvalidOperationException"/> for consistency with the
/// thrown shape <see cref="Opt2Parser"/> uses on the OPT2 path, so
/// callers that wrap parser invocations in a single
/// <c>catch (InvalidOperationException)</c> work uniformly across both
/// formats.
/// </summary>
public sealed class Opt14ParseException : InvalidOperationException
{
    /// <summary>
    /// 1-based line number where the offending element appeared in
    /// the source document, or <c>0</c> when not available.
    /// </summary>
    public int LineNumber { get; }

    /// <summary>
    /// 1-based column position where the offending element appeared,
    /// or <c>0</c> when not available.
    /// </summary>
    public int LinePosition { get; }

    public Opt14ParseException(string message)
        : base(message)
    {
    }

    public Opt14ParseException(string message, int lineNumber, int linePosition)
        : base(FormatMessage(message, lineNumber, linePosition))
    {
        LineNumber = lineNumber;
        LinePosition = linePosition;
    }

    internal static Opt14ParseException AtElement(string message, System.Xml.Linq.XElement element)
    {
        if (element is IXmlLineInfo info && info.HasLineInfo())
        {
            return new Opt14ParseException(message, info.LineNumber, info.LinePosition);
        }
        return new Opt14ParseException(message);
    }

    private static string FormatMessage(string message, int line, int column)
        => line > 0 ? $"{message} (line {line}, column {column})" : message;
}
