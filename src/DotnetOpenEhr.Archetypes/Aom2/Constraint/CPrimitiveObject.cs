namespace DotnetOpenEhr.Archetypes.Aom2.Constraint;

// SPEC: AOM2.html#_c_primitive_object_class — abstract base for the
// primitive-typed constraint classes (CString, CInteger, CReal, …).

/// <summary>
/// Abstract base for constraint nodes whose values are RM primitives.
/// </summary>
public abstract class CPrimitiveObject<T> : CDefinedObject
{
    /// <summary>
    /// Optional explicit list of enumerated allowed values; when null,
    /// any value matching the subtype-specific constraints is allowed.
    /// </summary>
    public List<T>? EnumeratedValues { get; set; }

    /// <summary>
    /// Default for this primitive constraint, typed for convenience.
    /// Setter and getter forward to the base <see cref="CDefinedObject.DefaultValue"/>.
    /// </summary>
    public new T? DefaultValue
    {
        get => base.DefaultValue is T t ? t : default;
        set => base.DefaultValue = value;
    }
}
