using System.Globalization;
using System.Text.Json.Serialization;

namespace DotnetOpenEhr.Rm.DataTypes.Quantity;

// SPEC: Data Types Information Model.html#_dv_ordered_class (Section 6.2.1).
/// <summary>
/// Abstract base of ordered data values. Adds normal-status / range
/// reference range metadata to a <see cref="DataValue"/>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(DvOrdinal),    "DV_ORDINAL")]
[JsonDerivedType(typeof(DvScale),      "DV_SCALE")]
[JsonDerivedType(typeof(DvQuantity),   "DV_QUANTITY")]
[JsonDerivedType(typeof(DvCount),      "DV_COUNT")]
[JsonDerivedType(typeof(DvProportion), "DV_PROPORTION")]
[JsonDerivedType(typeof(DateTime.DvDate),     "DV_DATE")]
[JsonDerivedType(typeof(DateTime.DvTime),     "DV_TIME")]
[JsonDerivedType(typeof(DateTime.DvDateTime), "DV_DATE_TIME")]
[JsonDerivedType(typeof(DateTime.DvDuration), "DV_DURATION")]
public abstract class DvOrdered : DataValue, IComparable<DvOrdered>
{
    [JsonPropertyName("normal_status")]
    public Text.CodePhrase? NormalStatus { get; set; }

    [JsonPropertyName("normal_range")]
    public ReferenceRange? NormalRange { get; set; }

    [JsonPropertyName("other_reference_ranges")]
    public IReadOnlyList<ReferenceRange>? OtherReferenceRanges { get; set; }

    /// <summary>
    /// Per spec, DvOrdered defines a partial order over comparable
    /// instances of the same concrete subtype. Comparing across subtypes
    /// is undefined and throws <see cref="ArgumentException"/>.
    /// </summary>
    public abstract int CompareTo(DvOrdered? other);
}

// SPEC: Data Types Information Model.html#_dv_quantified_class (Section 6.2.6).
/// <summary>Abstract intermediate base for truly quantified values.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(DvQuantity),   "DV_QUANTITY")]
[JsonDerivedType(typeof(DvCount),      "DV_COUNT")]
[JsonDerivedType(typeof(DvProportion), "DV_PROPORTION")]
[JsonDerivedType(typeof(DateTime.DvDate),     "DV_DATE")]
[JsonDerivedType(typeof(DateTime.DvTime),     "DV_TIME")]
[JsonDerivedType(typeof(DateTime.DvDateTime), "DV_DATE_TIME")]
[JsonDerivedType(typeof(DateTime.DvDuration), "DV_DURATION")]
public abstract class DvQuantified : DvOrdered
{
    [JsonPropertyName("magnitude_status")]
    public string? MagnitudeStatus { get; set; }
}

// SPEC: Data Types Information Model.html#_dv_amount_class (Section 6.2.7).
/// <summary>
/// Abstract base for relative quantified amounts (the operands of
/// <c>+</c> and <c>-</c>).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(DvQuantity),   "DV_QUANTITY")]
[JsonDerivedType(typeof(DvCount),      "DV_COUNT")]
[JsonDerivedType(typeof(DvProportion), "DV_PROPORTION")]
[JsonDerivedType(typeof(DateTime.DvDuration), "DV_DURATION")]
public abstract class DvAmount : DvQuantified
{
    [JsonPropertyName("accuracy")]
    public double Accuracy { get; set; }

    [JsonPropertyName("accuracy_is_percent")]
    public bool AccuracyIsPercent { get; set; }
}

// SPEC: Data Types Information Model.html#_dv_absolute_quantity_class (Section 6.2.12).
/// <summary>Abstract base for absolute-quantity types whose diff is a <c>DV_AMOUNT</c>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(DateTime.DvDate),     "DV_DATE")]
[JsonDerivedType(typeof(DateTime.DvTime),     "DV_TIME")]
[JsonDerivedType(typeof(DateTime.DvDateTime), "DV_DATE_TIME")]
public abstract class DvAbsoluteQuantity : DvQuantified
{
    [JsonPropertyName("accuracy")]
    public DvAmount? Accuracy { get; set; }
}

// SPEC: Data Types Information Model.html#_dv_quantity_class (Section 6.2.8).
/// <summary>Quantified amount expressed as magnitude + units (UCUM by default).</summary>
public sealed class DvQuantity : DvAmount
{
    public DvQuantity() { }

    public DvQuantity(double magnitude, string units, int? precision = null)
    {
        Magnitude = magnitude;
        Units = units;
        Precision = precision;
    }

    [JsonPropertyName("magnitude")]
    public double Magnitude { get; set; }

    [JsonPropertyName("precision")]
    public int? Precision { get; set; }

    [JsonPropertyName("units")]
    public string Units { get; set; } = string.Empty;

    [JsonPropertyName("units_system")]
    public string? UnitsSystem { get; set; }

    [JsonPropertyName("units_display_name")]
    public string? UnitsDisplayName { get; set; }

    public override string ToString()
        => FormattableString.Invariant($"{Magnitude} {Units}");

    public override int CompareTo(DvOrdered? other)
    {
        if (other is null) return 1;
        if (other is not DvQuantity q) throw new ArgumentException("Incompatible DvOrdered.", nameof(other));
        return Magnitude.CompareTo(q.Magnitude);
    }
}

// SPEC: Data Types Information Model.html#_dv_count_class (Section 6.2.9).
/// <summary>Countable quantity expressed as an integer magnitude.</summary>
public sealed class DvCount : DvAmount
{
    public DvCount() { }

    public DvCount(long magnitude)
    {
        Magnitude = magnitude;
    }

    [JsonPropertyName("magnitude")]
    public long Magnitude { get; set; }

    public override string ToString()
        => Magnitude.ToString(CultureInfo.InvariantCulture);

    public override int CompareTo(DvOrdered? other)
    {
        if (other is null) return 1;
        if (other is not DvCount c) throw new ArgumentException("Incompatible DvOrdered.", nameof(other));
        return Magnitude.CompareTo(c.Magnitude);
    }
}

// SPEC: Data Types Information Model.html#_dv_proportion_class (Section 6.2.10).
/// <summary>Ratio of two pure numbers (e.g. percent, titre, unitary).</summary>
public sealed class DvProportion : DvAmount
{
    [JsonPropertyName("numerator")]
    public double Numerator { get; set; }

    [JsonPropertyName("denominator")]
    public double Denominator { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("precision")]
    public int? Precision { get; set; }

    public override string ToString()
        => FormattableString.Invariant($"{Numerator}/{Denominator}");

    public override int CompareTo(DvOrdered? other)
    {
        if (other is null) return 1;
        if (other is not DvProportion p) throw new ArgumentException("Incompatible DvOrdered.", nameof(other));
        double left = Denominator == 0 ? 0 : Numerator / Denominator;
        double right = p.Denominator == 0 ? 0 : p.Numerator / p.Denominator;
        return left.CompareTo(right);
    }
}

// SPEC: Data Types Information Model.html#_dv_ordinal_class (Section 6.2.4).
/// <summary>Integral score with an attached symbol carrying meaning.</summary>
public sealed class DvOrdinal : DvOrdered
{
    [JsonPropertyName("value")]
    public int Value { get; set; }

    [JsonPropertyName("symbol")]
    public Text.DvCodedText Symbol { get; set; } = new();

    public override string ToString()
        => FormattableString.Invariant($"{Value}|{Symbol}");

    public override int CompareTo(DvOrdered? other)
    {
        if (other is null) return 1;
        if (other is not DvOrdinal o) throw new ArgumentException("Incompatible DvOrdered.", nameof(other));
        return Value.CompareTo(o.Value);
    }
}

// SPEC: Data Types Information Model.html#_dv_scale_class (Section 6.2.5).
/// <summary>Real-valued score with an attached symbol (e.g. APGAR sub-score).</summary>
public sealed class DvScale : DvOrdered
{
    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("symbol")]
    public Text.DvCodedText Symbol { get; set; } = new();

    public override string ToString()
        => FormattableString.Invariant($"{Value}|{Symbol}");

    public override int CompareTo(DvOrdered? other)
    {
        if (other is null) return 1;
        if (other is not DvScale s) throw new ArgumentException("Incompatible DvOrdered.", nameof(other));
        return Value.CompareTo(s.Value);
    }
}

// SPEC: Data Types Information Model.html#_reference_range_class (Section 6.2.3).
/// <summary>Named reference range associated with an ordered value.</summary>
public sealed class ReferenceRange
{
    [JsonPropertyName("meaning")]
    public Text.DvText Meaning { get; set; } = new();

    [JsonPropertyName("range")]
    public Foundation.Interval<DvOrdered>? Range { get; set; }
}
