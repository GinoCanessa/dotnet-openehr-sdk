namespace DotnetOpenEhr.Foundation.Iso;

/// <summary>
/// openEHR / ISO 8601 partial-precision date+time. Combines an
/// <see cref="IsoDate"/> with an optional <see cref="IsoTime"/>; preserves
/// the original lexical form.
/// </summary>
public sealed class IsoDateTime : IEquatable<IsoDateTime>, IComparable<IsoDateTime>, IComparable
{
    public IsoDateTime(IsoDate date, IsoTime? time = null, string? originalLexicalForm = null)
    {
        ArgumentNullException.ThrowIfNull(date);
        Date = date;
        Time = time;
        OriginalLexicalForm = originalLexicalForm ?? FormatCanonical(date, time);
    }

    public IsoDate Date { get; }
    public IsoTime? Time { get; }

    public string OriginalLexicalForm { get; }

    public static IsoDateTime Parse(ReadOnlySpan<char> text)
    {
        if (!TryParse(text, out IsoDateTime? value))
        {
            throw new FormatException($"'{text.ToString()}' is not a valid ISO 8601 date-time.");
        }
        return value;
    }

    public static bool TryParse(ReadOnlySpan<char> text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IsoDateTime? value)
    {
        value = null;
        int tIndex = -1;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == 'T' || text[i] == 't')
            {
                tIndex = i;
                break;
            }
        }

        if (tIndex < 0)
        {
            if (!IsoDate.TryParse(text, out IsoDate? dateOnly)) return false;
            value = new IsoDateTime(dateOnly!, null, text.ToString());
            return true;
        }

        ReadOnlySpan<char> datePart = text.Slice(0, tIndex);
        ReadOnlySpan<char> timePart = text.Slice(tIndex + 1);
        if (!IsoDate.TryParse(datePart, out IsoDate? date)) return false;
        if (!IsoTime.TryParse(timePart, out IsoTime? time)) return false;

        value = new IsoDateTime(date!, time, text.ToString());
        return true;
    }

    public int CompareTo(IsoDateTime? other)
    {
        if (other is null) return 1;
        if (Date.Precision == IsoDatePrecision.Day
            && other.Date.Precision == IsoDatePrecision.Day
            && Time?.TimeZone is not null
            && other.Time?.TimeZone is not null)
        {
            decimal leftUtcSeconds = ToUtcTimelineSeconds(Date, Time);
            decimal rightUtcSeconds = ToUtcTimelineSeconds(other.Date, other.Time);
            return leftUtcSeconds.CompareTo(rightUtcSeconds);
        }

        int cmp = Date.CompareTo(other.Date);
        if (cmp != 0) return cmp;
        if (Time is null && other.Time is null) return 0;
        if (Time is null) return -1;
        if (other.Time is null) return 1;
        return Time.CompareTo(other.Time);
    }

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is IsoDateTime d) return CompareTo(d);
        throw new ArgumentException($"Object must be of type {nameof(IsoDateTime)}.", nameof(obj));
    }

    public bool Equals(IsoDateTime? other)
        => other is not null && Date.Equals(other.Date) && Equals(Time, other.Time);

    public override bool Equals(object? obj) => Equals(obj as IsoDateTime);

    public override int GetHashCode() => HashCode.Combine(Date, Time);

    public override string ToString() => OriginalLexicalForm;

    private static decimal ToUtcTimelineSeconds(IsoDate date, IsoTime time)
        => date.ToDateOnly().DayNumber * 86400m + time.ToReferenceDayUtcSeconds();

    private static string FormatCanonical(IsoDate date, IsoTime? time)
        => time is null ? date.OriginalLexicalForm : $"{date.OriginalLexicalForm}T{time.OriginalLexicalForm}";
}
