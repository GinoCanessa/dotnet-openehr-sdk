namespace DotnetOpenEhr.Foundation.Iso;

/// <summary>
/// ISO 8601 timezone offset. Either UTC (<c>Z</c>) or a signed
/// <c>±HH[:MM]</c> offset. Preserves the original lexical form.
/// </summary>
public sealed class IsoTimeZone : IEquatable<IsoTimeZone>, IComparable<IsoTimeZone>, IComparable
{
    public IsoTimeZone(int hours, int minutes = 0, bool isNegative = false, string? originalLexicalForm = null)
    {
        if (hours < 0 || hours > 14) throw new ArgumentOutOfRangeException(nameof(hours));
        if (minutes < 0 || minutes > 59) throw new ArgumentOutOfRangeException(nameof(minutes));
        Hours = hours;
        Minutes = minutes;
        IsNegative = isNegative;
        OriginalLexicalForm = originalLexicalForm ?? FormatCanonical(hours, minutes, isNegative);
    }

    public int Hours { get; }
    public int Minutes { get; }
    public bool IsNegative { get; }

    public bool IsUtc => Hours == 0 && Minutes == 0 && OriginalLexicalForm.Equals("Z", StringComparison.OrdinalIgnoreCase);

    public string OriginalLexicalForm { get; }

    public TimeSpan ToTimeSpan()
    {
        TimeSpan span = new TimeSpan(Hours, Minutes, 0);
        return IsNegative ? -span : span;
    }

    public static readonly IsoTimeZone Utc = new IsoTimeZone(0, 0, false, "Z");

    public static IsoTimeZone Parse(ReadOnlySpan<char> text)
    {
        if (!TryParse(text, out IsoTimeZone? value))
        {
            throw new FormatException($"'{text.ToString()}' is not a valid ISO 8601 timezone offset.");
        }
        return value;
    }

    public static bool TryParse(ReadOnlySpan<char> text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IsoTimeZone? value)
    {
        value = null;
        if (text.IsEmpty) return false;

        if (text.Length == 1 && (text[0] == 'Z' || text[0] == 'z'))
        {
            value = new IsoTimeZone(0, 0, false, "Z");
            return true;
        }

        if (text[0] != '+' && text[0] != '-') return false;
        bool negative = text[0] == '-';
        ReadOnlySpan<char> body = text.Slice(1);

        if (body.Length < 2) return false;
        if (!IsoDate.TryParseDigits(body.Slice(0, 2), out int hours)) return false;
        if (hours < 0 || hours > 14) return false;

        int minutes = 0;
        ReadOnlySpan<char> rest = body.Slice(2);
        if (!rest.IsEmpty)
        {
            if (rest[0] == ':')
            {
                rest = rest.Slice(1);
            }
            if (rest.Length != 2) return false;
            if (!IsoDate.TryParseDigits(rest, out minutes)) return false;
            if (minutes < 0 || minutes > 59) return false;
        }

        try
        {
            value = new IsoTimeZone(hours, minutes, negative, text.ToString());
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public int CompareTo(IsoTimeZone? other)
    {
        if (other is null) return 1;
        return ToTimeSpan().CompareTo(other.ToTimeSpan());
    }

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is IsoTimeZone z) return CompareTo(z);
        throw new ArgumentException($"Object must be of type {nameof(IsoTimeZone)}.", nameof(obj));
    }

    public bool Equals(IsoTimeZone? other)
        => other is not null && Hours == other.Hours && Minutes == other.Minutes && IsNegative == other.IsNegative;

    public override bool Equals(object? obj) => Equals(obj as IsoTimeZone);

    public override int GetHashCode() => HashCode.Combine(Hours, Minutes, IsNegative);

    public override string ToString() => OriginalLexicalForm;

    private static string FormatCanonical(int hours, int minutes, bool isNegative)
    {
        if (hours == 0 && minutes == 0 && !isNegative) return "Z";
        char sign = isNegative ? '-' : '+';
        return $"{sign}{hours:D2}:{minutes:D2}";
    }
}
