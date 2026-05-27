namespace DotnetOpenEhr.Archetypes.Aom2.Constraint;

// SPEC: AOM2.html#_c_complex_object_class — constrains an RM object by
// listing constraints on its attributes plus optional cross-attribute
// tuple constraints.

/// <summary>
/// Constrains a complex RM object by enumerating attribute and tuple
/// constraints on it.
/// </summary>
public class CComplexObject : CDefinedObject
{
    public List<CAttribute> Attributes { get; set; } = [];
    public List<CAttributeTuple> AttributeTuples { get; set; } = [];
}
