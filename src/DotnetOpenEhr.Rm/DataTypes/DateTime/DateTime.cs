using System.Text.Json.Serialization;
using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm.DataTypes.Quantity;

namespace DotnetOpenEhr.Rm.DataTypes.DateTime;

// SPEC: Data Types Information Model.html#_dv_temporal_class (Section 7.2.1).
/// <summary>Temporal specialisation of <see cref="DvAbsoluteQuantity"/>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(DvDate),     "DV_DATE")]
[JsonDerivedType(typeof(DvTime),     "DV_TIME")]
[JsonDerivedType(typeof(DvDateTime), "DV_DATE_TIME")]
public abstract class DvTemporal : DvAbsoluteQuantity
{
}

// SPEC: Data Types Information Model.html#_dv_date_class (Section 7.2.2).
/// <summary>Partial-precision openEHR/ISO 8601 date.</summary>
public sealed class DvDate : DvTemporal
{
    public DvDate() { }

    public DvDate(IsoDate value)
    {
        Value = value;
    }

    [JsonPropertyName("value")]
    [JsonConverter(typeof(IsoLexicalConverter<IsoDate>))]
    public IsoDate Value { get; set; } = new(1, 1, 1);

    public override string ToString() => Value.OriginalLexicalForm;

    public override int CompareTo(Quantity.DvOrdered? other)
    {
        if (other is null) return 1;
        if (other is not DvDate d) throw new ArgumentException("Incompatible DvOrdered.", nameof(other));
        return Value.CompareTo(d.Value);
    }
}

// SPEC: Data Types Information Model.html#_dv_time_class (Section 7.2.3).
/// <summary>Partial-precision openEHR/ISO 8601 time-of-day.</summary>
public sealed class DvTime : DvTemporal
{
    public DvTime() { }

    public DvTime(IsoTime value)
    {
        Value = value;
    }

    [JsonPropertyName("value")]
    [JsonConverter(typeof(IsoLexicalConverter<IsoTime>))]
    public IsoTime Value { get; set; } = new(0);

    public override string ToString() => Value.OriginalLexicalForm;

    public override int CompareTo(Quantity.DvOrdered? other)
    {
        if (other is null) return 1;
        if (other is not DvTime t) throw new ArgumentException("Incompatible DvOrdered.", nameof(other));
        return Value.CompareTo(t.Value);
    }
}

// SPEC: Data Types Information Model.html#_dv_date_time_class (Section 7.2.4).
/// <summary>Partial-precision openEHR/ISO 8601 date-time.</summary>
public sealed class DvDateTime : DvTemporal
{
    public DvDateTime() { }

    public DvDateTime(IsoDateTime value)
    {
        Value = value;
    }

    [JsonPropertyName("value")]
    [JsonConverter(typeof(IsoLexicalConverter<IsoDateTime>))]
    public IsoDateTime Value { get; set; } = new(new IsoDate(1, 1, 1), new IsoTime(0));

    public override string ToString() => Value.OriginalLexicalForm;

    public override int CompareTo(Quantity.DvOrdered? other)
    {
        if (other is null) return 1;
        if (other is not DvDateTime dt) throw new ArgumentException("Incompatible DvOrdered.", nameof(other));
        return Value.CompareTo(dt.Value);
    }
}

// SPEC: Data Types Information Model.html#_dv_duration_class (Section 7.2.5).
/// <summary>openEHR/ISO 8601 duration with negative sign and 'W' mixing allowed.</summary>
public sealed class DvDuration : DvAmount
{
    public DvDuration() { }

    public DvDuration(IsoDuration value)
    {
        Value = value;
    }

    [JsonPropertyName("value")]
    [JsonConverter(typeof(IsoLexicalConverter<IsoDuration>))]
    public IsoDuration Value { get; set; } = new();

    public override string ToString() => Value.OriginalLexicalForm;

    public override int CompareTo(Quantity.DvOrdered? other)
    {
        if (other is null) return 1;
        if (other is not DvDuration d) throw new ArgumentException("Incompatible DvOrdered.", nameof(other));
        return Value.CompareTo(d.Value);
    }
}
