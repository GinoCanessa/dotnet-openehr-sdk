using DotnetOpenEhr.Foundation;

namespace DotnetOpenEhr.Archetypes.Aom2.Constraint;

// SPEC: AOM2.html — primitive constraint classes (CString, CInteger,
// CReal, CDuration, CBoolean, CDate, CTime, CDateTime,
// CTerminologyCode). Each carries the relevant constraint shape on top
// of CPrimitiveObject<T>.

/// <summary>Constrains a <c>String</c>-typed RM property.</summary>
public sealed class CString : CPrimitiveObject<string>
{
    /// <summary>Optional regex pattern the value must match.</summary>
    public string? Pattern { get; set; }
}

/// <summary>Constrains an <c>Integer</c>-typed RM property.</summary>
public sealed class CInteger : CPrimitiveObject<int>
{
    public Interval<int>? Range { get; set; }
}

/// <summary>Constrains a <c>Real</c>-typed RM property.</summary>
public sealed class CReal : CPrimitiveObject<double>
{
    public Interval<double>? Range { get; set; }
}

/// <summary>Constrains an ISO 8601 duration value.</summary>
public sealed class CDuration : CPrimitiveObject<string>
{
    public string? Pattern { get; set; }
    public Interval<string>? Range { get; set; }
}

/// <summary>Constrains a <c>Boolean</c>-typed RM property.</summary>
public sealed class CBoolean : CPrimitiveObject<bool>
{
    public bool TrueValid { get; set; } = true;
    public bool FalseValid { get; set; } = true;
}

/// <summary>Constrains an ISO 8601 date value.</summary>
public sealed class CDate : CPrimitiveObject<string>
{
    public string? Pattern { get; set; }
    public Interval<string>? Range { get; set; }
}

/// <summary>Constrains an ISO 8601 time value.</summary>
public sealed class CTime : CPrimitiveObject<string>
{
    public string? Pattern { get; set; }
    public Interval<string>? Range { get; set; }
}

/// <summary>Constrains an ISO 8601 date-time value.</summary>
public sealed class CDateTime : CPrimitiveObject<string>
{
    public string? Pattern { get; set; }
    public Interval<string>? Range { get; set; }
}

/// <summary>Constrains a terminology-coded value.</summary>
public sealed class CTerminologyCode : CPrimitiveObject<string>
{
    /// <summary>
    /// Terminology identifier (e.g. <c>"local"</c>, <c>"SNOMED-CT"</c>).
    /// </summary>
    public string TerminologyId { get; set; } = string.Empty;

    /// <summary>
    /// AC-code reference to a value-set defined in the terminology
    /// container, when constraint is by value-set rather than by a
    /// fixed code list. Mutually exclusive with
    /// <see cref="CPrimitiveObject{T}.EnumeratedValues"/>.
    /// </summary>
    public string? ValueSetRef { get; set; }
}
