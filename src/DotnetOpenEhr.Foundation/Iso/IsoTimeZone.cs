namespace DotnetOpenEhr.Foundation.Iso;

/// <summary>
/// ISO 8601 timezone offset. Either UTC (<c>Z</c>) or a signed
/// <c>±HH[:MM]</c> offset. Preserves the original lexical form.
/// </summary>
/// <remarks>
/// Equality, <c>GetHashCode</c>, and <c>CompareTo</c> all operate on
/// the canonical normalized <see cref="ToTimeSpan"/> so <c>+00:00</c>,
/// <c>-00:00</c>, and <c>Z</c> are pairwise equal (M6).
/// </remarks>
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

    /// <summary>
    /// True when the offset normalizes to zero, regardless of sign or
    /// original lexical form (<c>Z</c>, <c>z</c>, <c>+00:00</c>,
    /// <c>-00:00</c>, <c>+00</c>, <c>-00</c> all return true).
    /// </summary>
    public bool IsUtc => Hours == 0 && Minutes == 0;

    public string OriginalLexicalForm { get; }

    public TimeSpan ToTimeSpan()
    {
        TimeSpan span = new TimeSpan(Hours, Minutes, 0);
        return IsNegative ? -span : span;
    }

    public static readonly IsoTimeZone Utc = new IsoTimeZone(0, 0, false, "Z");

    public static IsoTimeZone Parse(ReadOnlySpan<char> text)
        => Parse(text, IsoParseMode.FixAsPossible);

    public static IsoTimeZone Parse(ReadOnlySpan<char> text, IsoParseMode mode)
    {
        if (!TryParse(text, mode, out IsoTimeZone? value))
        {
            throw new FormatException($"'{text.ToString()}' is not a valid ISO 8601 timezone offset.");
        }
        return value;
    }

    public static bool TryParse(ReadOnlySpan<char> text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IsoTimeZone? value)
        => TryParse(text, IsoParseMode.FixAsPossible, out value);

    public static bool TryParse(
        ReadOnlySpan<char> text,
        IsoParseMode mode,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IsoTimeZone? value)
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
        if (hours < 0) return false;

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

        // M6 — mode-aware hours / minutes posture.
        bool isQuarterMinute = minutes is 0 or 15 or 30 or 45;
        bool negativeHoursOverflow = negative && hours > 12;
        bool positiveHoursOverflow = !negative && hours > 14;

        switch (mode)
        {
            case IsoParseMode.Strict:
                if (!isQuarterMinute) return false;
                if (negativeHoursOverflow || positiveHoursOverflow) return false;
                break;

            case IsoParseMode.Ostrich:
                // Ostrich preserves verbatim but still rejects egregiously
                // out-of-range hours that no real-world TZ uses.
                if (hours > 14) return false;
                break;

            case IsoParseMode.FixAsPossible:
                // Egregious overflow that even FixAsPossible can't sensibly
                // clamp (e.g. +99:99) — reject rather than silently
                // pretend it's +14:00.
                if (hours > 20) return false;
                if (!isQuarterMinute)
                {
                    minutes = RoundToNearest15(minutes);
                }
                if (negativeHoursOverflow)
                {
                    hours = 12;
                    minutes = 0;
                }
                else if (positiveHoursOverflow)
                {
                    hours = 14;
                    minutes = 0;
                }
                break;
        }

        try
        {
            string? preserved = mode == IsoParseMode.Ostrich ? text.ToString() : null;
            value = new IsoTimeZone(hours, minutes, negative, preserved);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static int RoundToNearest15(int minutes)
    {
        int rounded = ((minutes + 7) / 15) * 15;
        return rounded > 59 ? 45 : rounded;
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
    {
        if (other is null) return false;
        // M6 — equality on canonical normalized span so +00:00, -00:00,
        // and Z all compare equal.
        return ToTimeSpan() == other.ToTimeSpan();
    }

    public override bool Equals(object? obj) => Equals(obj as IsoTimeZone);

    public override int GetHashCode() => ToTimeSpan().GetHashCode();

    public override string ToString() => OriginalLexicalForm;

    private static string FormatCanonical(int hours, int minutes, bool isNegative)
    {
        if (hours == 0 && minutes == 0 && !isNegative) return "Z";
        char sign = isNegative ? '-' : '+';
        return $"{sign}{hours:D2}:{minutes:D2}";
    }
}
