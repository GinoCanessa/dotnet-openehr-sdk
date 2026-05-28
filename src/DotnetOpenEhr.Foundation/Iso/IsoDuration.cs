namespace DotnetOpenEhr.Foundation.Iso;

/// <summary>
/// openEHR / ISO 8601 duration. Supports the openEHR-relevant subset
/// <c>P[nY][nM][nW][nD][T[nH][nM][nS]]</c> where the seconds component may
/// be fractional. Preserves the original lexical form.
/// </summary>
public sealed class IsoDuration : IEquatable<IsoDuration>, IComparable<IsoDuration>, IComparable
{
    public IsoDuration(
        int years = 0,
        int months = 0,
        int weeks = 0,
        int days = 0,
        int hours = 0,
        int minutes = 0,
        decimal seconds = 0m,
        bool isNegative = false,
        string? originalLexicalForm = null)
    {
        if (years < 0 || months < 0 || weeks < 0 || days < 0 || hours < 0 || minutes < 0 || seconds < 0m)
        {
            throw new ArgumentException("Duration components must be non-negative; use isNegative for the sign.");
        }

        Years = years;
        Months = months;
        Weeks = weeks;
        Days = days;
        Hours = hours;
        Minutes = minutes;
        Seconds = seconds;
        IsNegative = isNegative;
        OriginalLexicalForm = originalLexicalForm
            ?? FormatCanonical(years, months, weeks, days, hours, minutes, seconds, isNegative);
    }

    public int Years { get; }
    public int Months { get; }
    public int Weeks { get; }
    public int Days { get; }
    public int Hours { get; }
    public int Minutes { get; }
    public decimal Seconds { get; }
    public bool IsNegative { get; }

    public string OriginalLexicalForm { get; }

    public static IsoDuration Parse(ReadOnlySpan<char> text)
    {
        if (!TryParse(text, out IsoDuration? value))
        {
            throw new FormatException($"'{text.ToString()}' is not a valid ISO 8601 duration.");
        }
        return value;
    }

    public static bool TryParse(ReadOnlySpan<char> text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IsoDuration? value)
    {
        value = null;
        if (text.IsEmpty) return false;

        bool negative = false;
        int index = 0;
        if (text[0] == '-')
        {
            negative = true;
            index = 1;
        }
        else if (text[0] == '+')
        {
            index = 1;
        }

        if (index >= text.Length || text[index] != 'P') return false;
        index++;

        int years = 0, months = 0, weeks = 0, days = 0, hours = 0, minutes = 0;
        decimal seconds = 0m;
        bool inTime = false;
        bool anyComponent = false;

        while (index < text.Length)
        {
            char c = text[index];
            if (c == 'T')
            {
                if (inTime) return false;
                inTime = true;
                index++;
                continue;
            }

            int numStart = index;
            while (index < text.Length && (char.IsDigit(text[index]) || text[index] == '.' || text[index] == ','))
            {
                index++;
            }
            if (numStart == index || index >= text.Length) return false;

            ReadOnlySpan<char> numSpan = text.Slice(numStart, index - numStart);
            char unit = text[index];
            index++;

            string numText = numSpan.ToString().Replace(',', '.');
            if (!decimal.TryParse(numText, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out decimal num))
            {
                return false;
            }
            if (num < 0m) return false;

            if (!inTime)
            {
                switch (unit)
                {
                    case 'Y': if (years != 0) return false; if (!TryToInt(num, out years)) return false; break;
                    case 'M': if (months != 0) return false; if (!TryToInt(num, out months)) return false; break;
                    case 'W': if (weeks != 0) return false; if (!TryToInt(num, out weeks)) return false; break;
                    case 'D': if (days != 0) return false; if (!TryToInt(num, out days)) return false; break;
                    default: return false;
                }
            }
            else
            {
                switch (unit)
                {
                    case 'H': if (hours != 0) return false; if (!TryToInt(num, out hours)) return false; break;
                    case 'M': if (minutes != 0) return false; if (!TryToInt(num, out minutes)) return false; break;
                    case 'S': if (seconds != 0m) return false; seconds = num; break;
                    default: return false;
                }
            }

            anyComponent = true;
        }

        if (!anyComponent) return false;
        if (inTime && hours == 0 && minutes == 0 && seconds == 0m) return false;

        value = new IsoDuration(years, months, weeks, days, hours, minutes, seconds, negative, text.ToString());
        return true;
    }

    private static bool TryToInt(decimal d, out int result)
    {
        if (d != decimal.Truncate(d) || d > int.MaxValue)
        {
            result = 0;
            return false;
        }
        result = (int)d;
        return true;
    }

    /// <summary>
    /// Approximate this duration as a <see cref="TimeSpan"/>, treating
    /// 1 year = 365 days and 1 month = 30 days. Calendar-correct
    /// arithmetic requires an anchor date and is intentionally not
    /// performed here.
    /// </summary>
    public TimeSpan ToApproximateTimeSpan()
    {
        double totalDays =
            (double)Years * 365.0
            + (double)Months * 30.0
            + (double)Weeks * 7.0
            + (double)Days;
        double totalSeconds =
            totalDays * 86400.0
            + (double)Hours * 3600.0
            + (double)Minutes * 60.0
            + (double)Seconds;
        TimeSpan span = TimeSpan.FromSeconds(totalSeconds);
        return IsNegative ? -span : span;
    }

    public int CompareTo(IsoDuration? other)
    {
        if (other is null) return 1;
        return ToApproximateTimeSpan().CompareTo(other.ToApproximateTimeSpan());
    }

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is IsoDuration d) return CompareTo(d);
        throw new ArgumentException($"Object must be of type {nameof(IsoDuration)}.", nameof(obj));
    }

    public bool Equals(IsoDuration? other)
        => other is not null
        && Years == other.Years
        && Months == other.Months
        && Weeks == other.Weeks
        && Days == other.Days
        && Hours == other.Hours
        && Minutes == other.Minutes
        && Seconds == other.Seconds
        && IsNegative == other.IsNegative;

    public override bool Equals(object? obj) => Equals(obj as IsoDuration);

    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(Years);
        hash.Add(Months);
        hash.Add(Weeks);
        hash.Add(Days);
        hash.Add(Hours);
        hash.Add(Minutes);
        hash.Add(Seconds);
        hash.Add(IsNegative);
        return hash.ToHashCode();
    }

    public override string ToString() => OriginalLexicalForm;

    private static string FormatCanonical(
        int years, int months, int weeks, int days,
        int hours, int minutes, decimal seconds, bool isNegative)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(24);
        if (isNegative) sb.Append('-');
        sb.Append('P');
        if (years > 0) { sb.Append(years); sb.Append('Y'); }
        if (months > 0) { sb.Append(months); sb.Append('M'); }
        if (weeks > 0) { sb.Append(weeks); sb.Append('W'); }
        if (days > 0) { sb.Append(days); sb.Append('D'); }

        bool hasTime = hours > 0 || minutes > 0 || seconds > 0m;
        if (hasTime)
        {
            sb.Append('T');
            if (hours > 0) { sb.Append(hours); sb.Append('H'); }
            if (minutes > 0) { sb.Append(minutes); sb.Append('M'); }
            if (seconds > 0m)
            {
                sb.Append(seconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append('S');
            }
        }

        if (sb.Length == (isNegative ? 2 : 1))
        {
            sb.Append("T0S");
        }

        return sb.ToString();
    }
}
