using System.Diagnostics.CodeAnalysis;

namespace DotnetOpenEhr.Serialization.Json.Flat;

/// <summary>
/// An openEHR FLAT JSON path: a slash-delimited sequence of lowercase
/// identifier segments (optionally prefixed with a single underscore
/// for metadata segments such as <c>_end_time</c>), each segment
/// optionally suffixed with <c>:N</c> for a repeat index, terminated by
/// an optional <c>|attribute</c> suffix carrying the primitive-value
/// selector (<c>|magnitude</c>, <c>|unit</c>, <c>|code</c>,
/// <c>|value</c>, <c>|terminology</c>, ...).
/// </summary>
/// <remarks>
/// Storage is a single <see cref="string"/> reference plus the byte
/// offset at which the <c>|</c> attribute marker starts (or
/// <see cref="OriginalForm"/>.Length when there is no attribute).
/// Segment enumeration is allocation-free via the
/// <see cref="Enumerator"/> ref struct.
/// </remarks>
public readonly struct FlatPath : IEquatable<FlatPath>
{
    private readonly string _value;
    private readonly int _pipeIndex;

    private FlatPath(string value, int pipeIndex)
    {
        _value = value;
        _pipeIndex = pipeIndex;
    }

    /// <summary>The verbatim path string this value was parsed from.</summary>
    public string OriginalForm => _value ?? string.Empty;

    /// <summary>
    /// The attribute suffix including the leading <c>|</c>, e.g.
    /// <c>"|magnitude"</c>, or <see cref="string.Empty"/> when the path
    /// has no attribute.
    /// </summary>
    public string Attribute
    {
        get
        {
            if (_value is null) return string.Empty;
            return _pipeIndex < _value.Length ? _value.Substring(_pipeIndex) : string.Empty;
        }
    }

    /// <summary>
    /// The first segment of the path, by convention the template id.
    /// </summary>
    public string TemplateId
    {
        get
        {
            if (_value is null) return string.Empty;
            int slash = _value.IndexOf('/');
            int end = slash < 0 ? _pipeIndex : Math.Min(slash, _pipeIndex);
            return _value.Substring(0, end);
        }
    }

    /// <summary>Enumerates segments without allocation.</summary>
    public Enumerator Segments => new(this);

    /// <summary>
    /// Parses <paramref name="text"/> into a <see cref="FlatPath"/>.
    /// Throws <see cref="FormatException"/> with the 0-based offset of
    /// the first invalid character.
    /// </summary>
    public static FlatPath Parse(ReadOnlySpan<char> text)
    {
        if (!TryParseCore(text, out FlatPath path, out int errorAt, out string? message))
        {
            throw new FormatException($"Invalid FLAT path at offset {errorAt}: {message}");
        }
        return path;
    }

    /// <summary>
    /// Attempts to parse <paramref name="text"/>. Returns <c>false</c>
    /// without throwing on malformed input.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> text, [NotNullWhen(true)] out FlatPath? value)
    {
        if (TryParseCore(text, out FlatPath parsed, out _, out _))
        {
            value = parsed;
            return true;
        }
        value = null;
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => OriginalForm;

    /// <inheritdoc />
    public bool Equals(FlatPath other) => string.Equals(OriginalForm, other.OriginalForm, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is FlatPath other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(OriginalForm);

    /// <summary>Ordinal equality.</summary>
    public static bool operator ==(FlatPath left, FlatPath right) => left.Equals(right);

    /// <summary>Ordinal inequality.</summary>
    public static bool operator !=(FlatPath left, FlatPath right) => !left.Equals(right);

    private static bool TryParseCore(
        ReadOnlySpan<char> text,
        out FlatPath value,
        out int errorAt,
        out string? message)
    {
        value = default;
        errorAt = 0;
        message = null;

        if (text.Length == 0)
        {
            message = "empty path";
            return false;
        }

        int i = 0;
        int pipeIndex = -1;
        bool segmentStartedThisRun = false;
        bool seenAnySegment = false;

        // Local segment-validation state.
        bool inIdentifier = false;
        bool inIndex = false;
        bool identifierEmpty = true;
        bool indexEmpty = true;
        bool leadingUnderscoreOnly = false;

        while (i < text.Length)
        {
            char c = text[i];

            if (c == '|')
            {
                if (!segmentStartedThisRun || identifierEmpty || leadingUnderscoreOnly)
                {
                    errorAt = i;
                    message = "attribute marker '|' must follow a non-empty segment";
                    return false;
                }
                if (inIndex && indexEmpty)
                {
                    errorAt = i;
                    message = "':' must be followed by a digit";
                    return false;
                }
                pipeIndex = i;
                i++;
                if (!TryValidateAttribute(text, ref i, out errorAt, out message))
                {
                    return false;
                }
                if (i != text.Length)
                {
                    errorAt = i;
                    message = "trailing data after attribute suffix";
                    return false;
                }
                break;
            }

            if (c == '/')
            {
                if (!segmentStartedThisRun || identifierEmpty || leadingUnderscoreOnly)
                {
                    errorAt = i;
                    message = "segment separator '/' requires a preceding non-empty segment";
                    return false;
                }
                if (inIndex && indexEmpty)
                {
                    errorAt = i;
                    message = "':' must be followed by a digit";
                    return false;
                }
                seenAnySegment = true;
                segmentStartedThisRun = false;
                inIdentifier = false;
                inIndex = false;
                identifierEmpty = true;
                indexEmpty = true;
                leadingUnderscoreOnly = false;
                i++;
                continue;
            }

            if (c == ':')
            {
                if (!inIdentifier || identifierEmpty || leadingUnderscoreOnly)
                {
                    errorAt = i;
                    message = "':' must follow an identifier";
                    return false;
                }
                inIdentifier = false;
                inIndex = true;
                indexEmpty = true;
                i++;
                continue;
            }

            if (!segmentStartedThisRun)
            {
                segmentStartedThisRun = true;
                inIdentifier = true;
                identifierEmpty = true;
                leadingUnderscoreOnly = false;

                if (c == '_')
                {
                    // Underscore-prefixed metadata segment. Mark it so we
                    // know "_" alone is not a valid identifier.
                    leadingUnderscoreOnly = true;
                    identifierEmpty = false;
                    i++;
                    continue;
                }
            }

            if (inIdentifier)
            {
                if (IsIdentifierContinuationChar(c))
                {
                    identifierEmpty = false;
                    leadingUnderscoreOnly = false;
                    i++;
                    continue;
                }

                errorAt = i;
                message = $"invalid identifier character '{c}'";
                return false;
            }

            if (inIndex)
            {
                if (c is >= '0' and <= '9')
                {
                    indexEmpty = false;
                    i++;
                    continue;
                }

                errorAt = i;
                message = $"invalid digit in index '{c}'";
                return false;
            }

            errorAt = i;
            message = $"unexpected character '{c}'";
            return false;
        }

        if (pipeIndex < 0)
        {
            // Trailing-segment validation when no attribute was present.
            if (!segmentStartedThisRun || identifierEmpty || leadingUnderscoreOnly)
            {
                errorAt = text.Length;
                message = "path ends mid-segment";
                return false;
            }
            if (inIndex && indexEmpty)
            {
                errorAt = text.Length;
                message = "':' must be followed by a digit";
                return false;
            }
            seenAnySegment = true;
        }
        else
        {
            seenAnySegment = true;
        }

        if (!seenAnySegment)
        {
            errorAt = 0;
            message = "path contains no segments";
            return false;
        }

        string s = text.ToString();
        value = new FlatPath(s, pipeIndex < 0 ? s.Length : pipeIndex);
        return true;
    }

    private static bool TryValidateAttribute(
        ReadOnlySpan<char> text,
        ref int i,
        out int errorAt,
        out string? message)
    {
        errorAt = 0;
        message = null;

        // Attribute = lowercase identifier (letters / digits / '_'),
        // first char a letter or '_', optionally suffixed with ':N'
        // for an indexed sub-attribute (e.g. "|identifiers_assigner:0").
        if (i >= text.Length)
        {
            errorAt = i;
            message = "attribute name expected after '|'";
            return false;
        }
        char first = text[i];
        if (!(first is (>= 'a' and <= 'z') or '_'))
        {
            errorAt = i;
            message = $"attribute name must begin with a lower-case letter or '_', got '{first}'";
            return false;
        }
        i++;
        bool inIndex = false;
        bool indexEmpty = true;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '|' || c == '/')
            {
                errorAt = i;
                message = $"'{c}' is not allowed inside attribute name";
                return false;
            }
            if (c == ':')
            {
                if (inIndex)
                {
                    errorAt = i;
                    message = "duplicate ':' inside attribute index";
                    return false;
                }
                inIndex = true;
                indexEmpty = true;
                i++;
                continue;
            }
            if (inIndex)
            {
                if (c is >= '0' and <= '9')
                {
                    indexEmpty = false;
                    i++;
                    continue;
                }
                errorAt = i;
                message = $"invalid digit in attribute index '{c}'";
                return false;
            }
            if (!IsIdentifierContinuationChar(c))
            {
                errorAt = i;
                message = $"invalid attribute character '{c}'";
                return false;
            }
            i++;
        }
        if (inIndex && indexEmpty)
        {
            errorAt = i;
            message = "':' must be followed by a digit";
            return false;
        }
        return true;
    }

    private static bool IsIdentifierContinuationChar(char c)
        => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-' || c > 127 && char.IsLetter(c);

    /// <summary>
    /// Allocation-free enumerator over the slash-delimited segments of
    /// a <see cref="FlatPath"/> (excluding the optional attribute
    /// suffix). Each yielded span includes any <c>:N</c> repeat index
    /// suffix carried by that segment.
    /// </summary>
    public ref struct Enumerator
    {
        private readonly string _value;
        private readonly int _end;
        private int _start;
        private int _next;

        internal Enumerator(FlatPath path)
        {
            _value = path._value ?? string.Empty;
            _end = path._pipeIndex;
            _start = 0;
            _next = 0;
        }

        /// <summary>The current segment span.</summary>
        public ReadOnlySpan<char> Current
        {
            get
            {
                if (_value.Length == 0 || _end == 0) return [];
                return _value.AsSpan(_start, _next - _start);
            }
        }

        /// <summary>Required for <c>foreach</c> on a ref struct.</summary>
        public readonly Enumerator GetEnumerator() => this;

        /// <summary>Advances to the next segment.</summary>
        public bool MoveNext()
        {
            if (_value.Length == 0 || _end == 0) return false;
            int searchStart = _next;
            if (searchStart > 0)
            {
                if (searchStart >= _end) return false;
                if (_value[searchStart] == '/') searchStart++;
            }
            if (searchStart >= _end) return false;
            int slash = _value.IndexOf('/', searchStart, _end - searchStart);
            int segEnd = slash < 0 ? _end : slash;
            _start = searchStart;
            _next = segEnd;
            return true;
        }
    }
}
