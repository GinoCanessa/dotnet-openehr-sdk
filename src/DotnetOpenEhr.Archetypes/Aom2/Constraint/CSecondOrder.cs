using DotnetOpenEhr.Foundation;

namespace DotnetOpenEhr.Archetypes.Aom2.Constraint;

// SPEC: AOM2.html — second-order constraint classes: CCodePhrase,
// CDvQuantity (with CQuantityItem), CDvOrdinal (with CDvOrdinalItem).

/// <summary>
/// Constraint on a <c>DV_CODED_TEXT</c>-style value: a terminology id
/// plus an enumerated code list.
/// </summary>
public sealed class CCodePhrase : CDefinedObject
{
    public string TerminologyId { get; set; } = string.Empty;
    public List<string> CodeList { get; set; } = [];
}

/// <summary>
/// One row of a <see cref="CDvQuantity"/> constraint: a units string
/// plus optional magnitude and precision intervals.
/// </summary>
public sealed class CQuantityItem
{
    public string Units { get; set; } = string.Empty;
    public Interval<double>? Magnitude { get; set; }
    public Interval<int>? Precision { get; set; }
}

/// <summary>
/// Constraint on a <c>DV_QUANTITY</c> value: a set of permitted
/// (units, magnitude-range, precision-range) rows, with an optional
/// shared property reference.
/// </summary>
public sealed class CDvQuantity : CDefinedObject
{
    public string? Property { get; set; }
    public List<CQuantityItem> Items { get; set; } = [];
}

/// <summary>
/// One row of a <see cref="CDvOrdinal"/> constraint: an integer value
/// plus the symbol (terminology code) it stands for.
/// </summary>
public sealed class CDvOrdinalItem
{
    public int Value { get; set; }
    public string Symbol { get; set; } = string.Empty;
}

/// <summary>
/// Constraint on a <c>DV_ORDINAL</c> value: an enumerated list of
/// (value, symbol) pairs.
/// </summary>
public sealed class CDvOrdinal : CDefinedObject
{
    public List<CDvOrdinalItem> Items { get; set; } = [];
}
