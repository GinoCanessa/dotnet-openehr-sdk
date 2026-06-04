namespace DotnetOpenEhr.Foundation.Iso;

/// <summary>
/// openEHR / ISO 8601 partial-precision date. May carry year only,
/// year + month, or year + month + day. Preserves the original lexical
/// form so canonical serializers can emit the exact source text.
/// </summary>
public sealed class IsoDate : IEquatable<IsoDate>, IComparable<IsoDate>, IComparable
{
    public IsoDate(int year, int? month = null, int? day = null, string? originalLexicalForm = null)
    {
        if (month is not null && (month.Value < 1 || month.Value > 12))
        {
            throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be between 1 and 12.");
        }

        if (day is not null)
        {
            if (month is null)
            {
                throw new ArgumentException("Day cannot be specified without month.", nameof(day));
            }
            int daysInMonth = DateTime.DaysInMonth(year, month.Value);
            if (day.Value < 1 || day.Value > daysInMonth)
            {
                throw new ArgumentOutOfRangeException(nameof(day), day,
                    $"Day {day.Value} is not valid for {year}-{month.Value:D2}.");
            }
        }

        Year = year;
        Month = month;
        Day = day;
        OriginalLexicalForm = originalLexicalForm ?? FormatCanonical(year, month, day);
    }

    public int Year { get; }
    public int? Month { get; }
    public int? Day { get; }

    public string OriginalLexicalForm { get; }

    public IsoDatePrecision Precision =>
        Day is not null ? IsoDatePrecision.Day :
        Month is not null ? IsoDatePrecision.Month :
        IsoDatePrecision.Year;

    public static IsoDate Parse(ReadOnlySpan<char> text)
    {
        if (!TryParse(text, out IsoDate? value))
        {
            throw new FormatException($"'{text.ToString()}' is not a valid ISO 8601 date.");
        }
        return value;
    }

    public static IsoDate Parse(ReadOnlySpan<char> text, IsoParseMode mode)
    {
        // Mode is reserved for future per-component leniency; today the
        // IsoDate grammar admits no per-mode variation.
        _ = mode;
        return Parse(text);
    }

    public static bool TryParse(ReadOnlySpan<char> text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IsoDate? value)
    {
        value = null;
        if (text.Length < 4) return false;

        if (!TryParseDigits(text.Slice(0, 4), out int year)) return false;

        if (text.Length == 4)
        {
            value = new IsoDate(year, null, null, text.ToString());
            return true;
        }

        bool extended;
        ReadOnlySpan<char> rest = text.Slice(4);
        if (rest[0] == '-')
        {
            extended = true;
            rest = rest.Slice(1);
        }
        else if (char.IsDigit(rest[0]))
        {
            extended = false;
        }
        else
        {
            return false;
        }

        if (rest.Length < 2) return false;
        if (!TryParseDigits(rest.Slice(0, 2), out int month)) return false;
        if (month < 1 || month > 12) return false;
        rest = rest.Slice(2);

        if (rest.IsEmpty)
        {
            try
            {
                value = new IsoDate(year, month, null, text.ToString());
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        if (extended)
        {
            if (rest[0] != '-') return false;
            rest = rest.Slice(1);
        }

        if (rest.Length != 2) return false;
        if (!TryParseDigits(rest, out int day)) return false;
        if (day < 1 || day > 31) return false;

        try
        {
            value = new IsoDate(year, month, day, text.ToString());
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public DateOnly ToDateOnly()
    {
        if (Precision != IsoDatePrecision.Day)
        {
            throw new InvalidOperationException(
                $"Cannot convert an IsoDate of precision {Precision} to a DateOnly.");
        }
        return new DateOnly(Year, Month!.Value, Day!.Value);
    }

    public int CompareTo(IsoDate? other)
    {
        if (other is null) return 1;
        int cmp = Year.CompareTo(other.Year);
        if (cmp != 0) return cmp;
        cmp = (Month ?? 0).CompareTo(other.Month ?? 0);
        if (cmp != 0) return cmp;
        return (Day ?? 0).CompareTo(other.Day ?? 0);
    }

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is IsoDate d) return CompareTo(d);
        throw new ArgumentException($"Object must be of type {nameof(IsoDate)}.", nameof(obj));
    }

    public bool Equals(IsoDate? other)
        => other is not null && Year == other.Year && Month == other.Month && Day == other.Day;

    public override bool Equals(object? obj) => Equals(obj as IsoDate);

    public override int GetHashCode() => HashCode.Combine(Year, Month, Day);

    public override string ToString() => OriginalLexicalForm;

    internal static bool TryParseDigits(ReadOnlySpan<char> span, out int value)
    {
        value = 0;
        if (span.IsEmpty) return false;
        int result = 0;
        foreach (char c in span)
        {
            if (c < '0' || c > '9') return false;
            result = result * 10 + (c - '0');
        }
        value = result;
        return true;
    }

    private static string FormatCanonical(int year, int? month, int? day)
    {
        if (day is not null) return $"{year:D4}-{month!.Value:D2}-{day.Value:D2}";
        if (month is not null) return $"{year:D4}-{month.Value:D2}";
        return $"{year:D4}";
    }
}

public enum IsoDatePrecision
{
    Year = 0,
    Month = 1,
    Day = 2,
}
