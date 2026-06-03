using System.Text;

namespace DotnetOpenEhr.Aql.Paths;

/// <summary>
/// Hand-written recursive-descent parser for the archetype-path
/// subset used by <see cref="DotnetOpenEhr.Aql.ArchetypePath"/> and
/// <see cref="DotnetOpenEhr.Aql.ArchetypePathResolver"/>.
/// </summary>
/// <remarks>
/// <para>
/// Grammar (deliberately a strict subset of AQL — no <c>SELECT</c>,
/// no logical operators, no function calls, no parameters):
/// </para>
/// <code>
/// path        := '/' EOF                          // root only
///              | '/'? segment ('/' segment)*
/// segment     := identifier ('[' predicate ']')?
/// identifier  := [A-Za-z_][A-Za-z0-9_]*
/// predicate   := nodeId
///              | nodeId WS* ',' WS* string
///              | string
/// nodeId      := [A-Za-z0-9_\-.]+                 // atN / idN / acN / HRID
/// string      := '\'' ( [^'\\] | escape )* '\''
/// escape      := '\\' [\\'nrtabfv"?]              // matches AqlLexer.AppendEscape
/// WS          := ' ' | '\t'
/// </code>
/// <para>
/// Empty input is rejected (mapping configs that forget to set a path
/// should fail loudly rather than silently resolving to root). A
/// single <c>/</c> is the explicit way to address the root and returns
/// an empty segment array.
/// </para>
/// </remarks>
internal static class ArchetypePathParser
{
    internal static ArchetypePathSegment[] Parse(ReadOnlySpan<char> path)
    {
        if (!TryParseInternal(path, out ArchetypePathSegment[]? segments, out string? error, out int position))
        {
            throw new ArchetypePathParseException(error!, position);
        }
        return segments!;
    }

    internal static bool TryParse(
        ReadOnlySpan<char> path,
        out ArchetypePathSegment[]? segments,
        out string? error)
    {
        bool ok = TryParseInternal(path, out segments, out error, out _);
        if (!ok)
        {
            segments = null;
        }
        return ok;
    }

    private static bool TryParseInternal(
        ReadOnlySpan<char> path,
        out ArchetypePathSegment[]? segments,
        out string? error,
        out int errorPosition)
    {
        segments = null;
        error = null;
        errorPosition = 0;

        if (path.Length == 0)
        {
            error = "Archetype path is empty.";
            errorPosition = 1;
            return false;
        }

        int i = 0;

        // Optional leading slash.
        if (path[i] == '/')
        {
            i++;
            // Single '/' addresses the root.
            if (i == path.Length)
            {
                segments = [];
                return true;
            }
            // Reject '//...': we just consumed the leading slash and the
            // next char is another slash → empty segment.
            if (path[i] == '/')
            {
                error = "Empty path segment.";
                errorPosition = i + 1;
                return false;
            }
        }

        List<ArchetypePathSegment> collected = [];

        while (true)
        {
            if (i >= path.Length)
            {
                error = "Empty path segment.";
                errorPosition = i; // 1-based position of the offending '/'
                return false;
            }

            // Attribute identifier.
            int identStart = i;
            if (!IsIdentifierStart(path[i]))
            {
                error = $"Expected attribute identifier but found '{path[i]}'.";
                errorPosition = i + 1;
                return false;
            }
            i++;
            while (i < path.Length && IsIdentifierPart(path[i]))
            {
                i++;
            }
            string attributeName = path.Slice(identStart, i - identStart).ToString();

            // Optional predicate.
            ArchetypePathPredicate? predicate = null;
            if (i < path.Length && path[i] == '[')
            {
                int openBracket = i;
                i++; // consume '['
                if (!TryParsePredicate(path, ref i, openBracket, out predicate, out error, out errorPosition))
                {
                    return false;
                }
            }

            collected.Add(new ArchetypePathSegment(attributeName, predicate));

            // End of input.
            if (i >= path.Length)
            {
                break;
            }
            // Continue with another segment.
            if (path[i] == '/')
            {
                i++;
                if (i >= path.Length)
                {
                    // Trailing slash with no segment after.
                    error = "Empty path segment.";
                    errorPosition = i;
                    return false;
                }
                if (path[i] == '/')
                {
                    error = "Empty path segment.";
                    errorPosition = i + 1;
                    return false;
                }
                continue;
            }
            error = $"Unexpected character '{path[i]}' in archetype path.";
            errorPosition = i + 1;
            return false;
        }

        segments = [.. collected];
        return true;
    }

    private static bool TryParsePredicate(
        ReadOnlySpan<char> path,
        ref int i,
        int openBracket,
        out ArchetypePathPredicate? predicate,
        out string? error,
        out int errorPosition)
    {
        predicate = null;
        error = null;
        errorPosition = 0;

        SkipInlineWhitespace(path, ref i);
        if (i >= path.Length)
        {
            error = "Unterminated predicate.";
            errorPosition = openBracket + 1;
            return false;
        }

        string? nodeId = null;
        string? name = null;

        if (path[i] == '\'')
        {
            // Name-only predicate.
            if (!TryParseStringLiteral(path, ref i, out name, out error, out errorPosition))
            {
                return false;
            }
        }
        else
        {
            // Node-id (atN / idN / acN / HRID) potentially followed by , 'name'.
            if (!TryParseNodeId(path, ref i, out nodeId, out error, out errorPosition))
            {
                return false;
            }
            SkipInlineWhitespace(path, ref i);
            if (i < path.Length && path[i] == ',')
            {
                i++;
                SkipInlineWhitespace(path, ref i);
                if (i >= path.Length || path[i] != '\'')
                {
                    error = "Expected quoted name after ',' in predicate.";
                    errorPosition = i + 1;
                    return false;
                }
                if (!TryParseStringLiteral(path, ref i, out name, out error, out errorPosition))
                {
                    return false;
                }
            }
        }

        SkipInlineWhitespace(path, ref i);
        if (i >= path.Length || path[i] != ']')
        {
            error = "Unterminated predicate.";
            errorPosition = openBracket + 1;
            return false;
        }
        i++; // consume ']'

        predicate = new ArchetypePathPredicate(nodeId, name);
        return true;
    }

    private static bool TryParseNodeId(
        ReadOnlySpan<char> path,
        ref int i,
        out string? nodeId,
        out string? error,
        out int errorPosition)
    {
        nodeId = null;
        error = null;
        errorPosition = 0;

        int start = i;
        while (i < path.Length && IsNodeIdChar(path[i]))
        {
            i++;
        }
        if (i == start)
        {
            error = $"Expected node id but found '{path[i]}'.";
            errorPosition = i + 1;
            return false;
        }
        nodeId = path.Slice(start, i - start).ToString();
        return true;
    }

    private static bool TryParseStringLiteral(
        ReadOnlySpan<char> path,
        ref int i,
        out string? value,
        out string? error,
        out int errorPosition)
    {
        value = null;
        error = null;
        errorPosition = 0;

        int openQuote = i;
        i++; // consume opening quote
        StringBuilder builder = new();
        while (i < path.Length)
        {
            char c = path[i];
            if (c == '\'')
            {
                i++; // consume closing quote
                value = builder.ToString();
                return true;
            }
            if (c == '\\')
            {
                if (i + 1 >= path.Length)
                {
                    error = "Unterminated escape sequence in string literal.";
                    errorPosition = i + 1;
                    return false;
                }
                char esc = path[i + 1];
                char unescaped;
                switch (esc)
                {
                    case '\\': unescaped = '\\'; break;
                    case '\'': unescaped = '\''; break;
                    case '"': unescaped = '"'; break;
                    case 'n': unescaped = '\n'; break;
                    case 'r': unescaped = '\r'; break;
                    case 't': unescaped = '\t'; break;
                    case 'a': unescaped = '\a'; break;
                    case 'b': unescaped = '\b'; break;
                    case 'f': unescaped = '\f'; break;
                    case 'v': unescaped = '\v'; break;
                    case '?': unescaped = '?'; break;
                    default:
                        error = $"Invalid escape sequence '\\{esc}' in string literal.";
                        errorPosition = i + 1;
                        return false;
                }
                builder.Append(unescaped);
                i += 2;
                continue;
            }
            builder.Append(c);
            i++;
        }
        error = "Unterminated string literal.";
        errorPosition = openQuote + 1;
        return false;
    }

    private static void SkipInlineWhitespace(ReadOnlySpan<char> path, ref int i)
    {
        while (i < path.Length && (path[i] == ' ' || path[i] == '\t'))
        {
            i++;
        }
    }

    private static bool IsIdentifierStart(char c)
        => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_';

    private static bool IsIdentifierPart(char c)
        => IsIdentifierStart(c) || (c >= '0' && c <= '9');

    private static bool IsNodeIdChar(char c)
        => (c >= 'A' && c <= 'Z')
            || (c >= 'a' && c <= 'z')
            || (c >= '0' && c <= '9')
            || c == '_'
            || c == '-'
            || c == '.';
}
