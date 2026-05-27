namespace DotnetOpenEhr.Foundation.Iso;

/// <summary>
/// openEHR / ISO 8601 partial-precision time of day. May carry hour only,
/// hour + minute, hour + minute + second, or hour + minute + second +
/// fractional, with an optional timezone. Preserves the original lexical
/// form for byte-equivalent serializer round-trip.
/// </summary>
public sealed class IsoTime : IEquatable<IsoTime>, IComparable<IsoTime>, IComparable
{
    public IsoTime(
        int hour,
        int? minute = null,
        int? second = null,
        decimal? fractionalSecond = null,
        IsoTimeZone? timeZone = null,
        string? originalLexicalForm = null)
    {
        if (hour < 0 || hour > 23) throw new ArgumentOutOfRangeException(nameof(hour));
        if (minute is { } m && (m < 0 || m > 59)) throw new ArgumentOutOfRangeException(nameof(minute));
        if (second is { } s && (s < 0 || s > 60)) throw new ArgumentOutOfRangeException(nameof(second));
        if (fractionalSecond is { } f && (f < 0m || f >= 1m))
        {
            throw new ArgumentOutOfRangeException(nameof(fractionalSecond),
                "Fractional second must be in [0, 1).");
        }
        if (fractionalSecond is not null && second is null)
        {
            throw new ArgumentException("Fractional second requires whole seconds.", nameof(fractionalSecond));
        }
        if (second is not null && minute is null)
        {
            throw new ArgumentException("Seconds require minutes.", nameof(second));
        }

        Hour = hour;
        Minute = minute;
        Second = second;
        FractionalSecond = fractionalSecond;
        TimeZone = timeZone;
        OriginalLexicalForm = originalLexicalForm ??
            FormatCanonical(hour, minute, second, fractionalSecond, timeZone);
    }

    public int Hour { get; }
    public int? Minute { get; }
    public int? Second { get; }
    public decimal? FractionalSecond { get; }
    public IsoTimeZone? TimeZone { get; }

    public string OriginalLexicalForm { get; }

    public IsoTimePrecision Precision =>
        FractionalSecond is not null ? IsoTimePrecision.FractionalSecond :
        Second is not null ? IsoTimePrecision.Second :
        Minute is not null ? IsoTimePrecision.Minute :
        IsoTimePrecision.Hour;

    public static IsoTime Parse(ReadOnlySpan<char> text)
    {
        if (!TryParse(text, out IsoTime? value))
        {
            throw new FormatException($"'{text.ToString()}' is not a valid ISO 8601 time.");
        }
        return value;
    }

    public static bool TryParse(ReadOnlySpan<char> text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IsoTime? value)
    {
        value = null;
        if (text.Length < 2) return false;
        if (!IsoDate.TryParseDigits(text.Slice(0, 2), out int hour)) return false;
        if (hour < 0 || hour > 23) return false;

        ReadOnlySpan<char> rest = text.Slice(2);
        int? minute = null, second = null;
        decimal? fractional = null;
        IsoTimeZone? zone = null;
        bool extended = false;

        int zoneIndex = FindTimeZoneIndex(rest);
        ReadOnlySpan<char> zoneSpan = [];
        if (zoneIndex >= 0)
        {
            zoneSpan = rest.Slice(zoneIndex);
            rest = rest.Slice(0, zoneIndex);
        }

        if (!rest.IsEmpty)
        {
            if (rest[0] == ':')
            {
                extended = true;
                rest = rest.Slice(1);
            }

            if (rest.Length < 2) return false;
            if (!IsoDate.TryParseDigits(rest.Slice(0, 2), out int mm)) return false;
            if (mm < 0 || mm > 59) return false;
            minute = mm;
            rest = rest.Slice(2);

            if (!rest.IsEmpty)
            {
                if (extended)
                {
                    if (rest[0] != ':') return false;
                    rest = rest.Slice(1);
                }

                if (rest.Length < 2) return false;
                if (!IsoDate.TryParseDigits(rest.Slice(0, 2), out int ss)) return false;
                if (ss < 0 || ss > 60) return false;
                second = ss;
                rest = rest.Slice(2);

                if (!rest.IsEmpty)
                {
                    if (rest[0] != '.' && rest[0] != ',') return false;
                    rest = rest.Slice(1);
                    if (rest.IsEmpty) return false;
                    foreach (char c in rest)
                    {
                        if (c < '0' || c > '9') return false;
                    }
                    decimal frac = decimal.Parse(
                        "0." + rest.ToString(),
                        System.Globalization.CultureInfo.InvariantCulture);
                    fractional = frac;
                }
            }
        }

        if (!zoneSpan.IsEmpty)
        {
            if (!IsoTimeZone.TryParse(zoneSpan, out zone)) return false;
        }

        try
        {
            value = new IsoTime(hour, minute, second, fractional, zone, text.ToString());
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static int FindTimeZoneIndex(ReadOnlySpan<char> text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == 'Z' || c == 'z' || c == '+' || c == '-')
            {
                return i;
            }
        }
        return -1;
    }

    public int CompareTo(IsoTime? other)
    {
        if (other is null) return 1;
        int cmp = Hour.CompareTo(other.Hour);
        if (cmp != 0) return cmp;
        cmp = (Minute ?? 0).CompareTo(other.Minute ?? 0);
        if (cmp != 0) return cmp;
        cmp = (Second ?? 0).CompareTo(other.Second ?? 0);
        if (cmp != 0) return cmp;
        return (FractionalSecond ?? 0m).CompareTo(other.FractionalSecond ?? 0m);
    }

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is IsoTime t) return CompareTo(t);
        throw new ArgumentException($"Object must be of type {nameof(IsoTime)}.", nameof(obj));
    }

    public bool Equals(IsoTime? other)
        => other is not null
        && Hour == other.Hour
        && Minute == other.Minute
        && Second == other.Second
        && FractionalSecond == other.FractionalSecond
        && Equals(TimeZone, other.TimeZone);

    public override bool Equals(object? obj) => Equals(obj as IsoTime);

    public override int GetHashCode()
        => HashCode.Combine(Hour, Minute, Second, FractionalSecond, TimeZone);

    public override string ToString() => OriginalLexicalForm;

    private static string FormatCanonical(
        int hour, int? minute, int? second, decimal? fractional, IsoTimeZone? zone)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(24);
        sb.Append(hour.ToString("D2"));
        if (minute is not null)
        {
            sb.Append(':');
            sb.Append(minute.Value.ToString("D2"));
            if (second is not null)
            {
                sb.Append(':');
                sb.Append(second.Value.ToString("D2"));
                if (fractional is not null)
                {
                    string fracText = fractional.Value.ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
                    int dotIndex = fracText.IndexOf('.');
                    sb.Append('.');
                    sb.Append(dotIndex >= 0 ? fracText.AsSpan(dotIndex + 1) : "0".AsSpan());
                }
            }
        }
        if (zone is not null) sb.Append(zone.OriginalLexicalForm);
        return sb.ToString();
    }
}

public enum IsoTimePrecision
{
    Hour = 0,
    Minute = 1,
    Second = 2,
    FractionalSecond = 3,
}
