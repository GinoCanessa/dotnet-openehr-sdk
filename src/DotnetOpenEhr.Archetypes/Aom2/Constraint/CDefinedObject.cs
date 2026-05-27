namespace DotnetOpenEhr.Archetypes.Aom2.Constraint;

// SPEC: AOM2.html#_c_defined_object_class — adds an optional default
// value to a CObject. The default is carried as object?, because the
// concrete shape depends on the subtype (an RM value object for
// CComplexObject, a primitive instance for CPrimitiveObject<T>, etc.).

/// <summary>
/// A <see cref="CObject"/> that defines its constraint inline (as
/// opposed to <see cref="CReferenceObject"/>, which refers to another
/// node).
/// </summary>
public abstract class CDefinedObject : CObject
{
    public object? DefaultValue { get; set; }
}
