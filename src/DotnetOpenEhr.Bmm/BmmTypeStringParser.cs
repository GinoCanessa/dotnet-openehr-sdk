namespace DotnetOpenEhr.Bmm;

/// <summary>
/// Parses BMM type-name strings into a <see cref="BmmType"/> tree.
/// Supported forms:
/// <list type="bullet">
///   <item><c>SIMPLE</c> — atomic class name</item>
///   <item><c>Container&lt;X&gt;</c> — recognised container roots: List,
///   Set, Array, Hash, P_List, P_Set, P_Array, P_Hash</item>
///   <item><c>Hash&lt;K,V&gt;</c> — two-argument container</item>
///   <item><c>Generic&lt;X, Y, ...&gt;</c> — any other root with type args</item>
/// </list>
/// Whitespace inside angle brackets is tolerated. No regex; hand-written
/// recursive-descent scan.
/// </summary>
internal static class BmmTypeStringParser
{
    public static BmmType Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        int pos = 0;
        BmmType type = ParseType(source, ref pos);
        SkipWhitespace(source, ref pos);
        if (pos != source.Length)
        {
            throw new FormatException(
                $"Unexpected trailing content in BMM type '{source}' at offset {pos}.");
        }
        return type;
    }

    private static BmmType ParseType(string s, ref int pos)
    {
        SkipWhitespace(s, ref pos);
        string root = ReadIdentifier(s, ref pos);
        if (root.Length == 0)
        {
            throw new FormatException($"Expected type identifier at offset {pos} of '{s}'.");
        }
        SkipWhitespace(s, ref pos);
        if (pos >= s.Length || s[pos] != '<')
        {
            return new BmmSimpleType(root);
        }
        pos++; // consume '<'
        List<BmmType> args = [];
        while (true)
        {
            SkipWhitespace(s, ref pos);
            BmmType arg = ParseType(s, ref pos);
            args.Add(arg);
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length)
            {
                throw new FormatException($"Unterminated generic type in BMM type '{s}'.");
            }
            if (s[pos] == ',')
            {
                pos++;
                continue;
            }
            if (s[pos] == '>')
            {
                pos++;
                break;
            }
            throw new FormatException($"Expected ',' or '>' at offset {pos} of BMM type '{s}'.");
        }
        if (BmmContainerType.IsContainerRoot(root))
        {
            return new BmmContainerType(root, args);
        }
        return new BmmGenericType(root, args);
    }

    private static string ReadIdentifier(string s, ref int pos)
    {
        int start = pos;
        while (pos < s.Length)
        {
            char c = s[pos];
            // BMM type names allow letters, digits, underscore. We also
            // accept '.' so qualified references like 'org.openehr.Foo'
            // tokenise as a single root (rare but harmless).
            if (char.IsLetterOrDigit(c) || c == '_' || c == '.')
            {
                pos++;
            }
            else
            {
                break;
            }
        }
        return s.Substring(start, pos - start);
    }

    private static void SkipWhitespace(string s, ref int pos)
    {
        while (pos < s.Length && char.IsWhiteSpace(s[pos]))
        {
            pos++;
        }
    }
}
