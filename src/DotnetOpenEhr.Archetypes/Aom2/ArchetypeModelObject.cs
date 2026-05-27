namespace DotnetOpenEhr.Archetypes.Aom2;

// SPEC: AOM2.html#_archetype_model_object_class — abstract root of every
// node in the AOM2 tree. Carries optional source-location metadata
// populated by the Phase-7d ADL2 parser; programmatically-constructed
// trees leave SourceLine/SourceColumn at 0.

/// <summary>
/// Abstract root of every node in the openEHR Archetype Object Model
/// (AOM2).
/// </summary>
public abstract class ArchetypeModelObject
{
    /// <summary>
    /// Source line in the originating ADL2 text, when applicable.
    /// <c>0</c> on programmatically-constructed trees.
    /// </summary>
    public int SourceLine { get; set; }

    /// <summary>
    /// Source column in the originating ADL2 text, when applicable.
    /// <c>0</c> on programmatically-constructed trees.
    /// </summary>
    public int SourceColumn { get; set; }
}
