namespace DotnetOpenEhr.Archetypes.Aom2.Terminology;

// SPEC: AOM2.html — Archetype Terminology container.

/// <summary>
/// A single localised term entry: text (always), description and comment
/// (optional).
/// </summary>
public sealed class ArchetypeTerm
{
    public string Text { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Comment { get; set; }
}

/// <summary>
/// Named value-set (a set of terminology codes) referenced from a
/// <c>CTerminologyCode</c> binding.
/// </summary>
public sealed class ValueSet
{
    public string Id { get; set; } = string.Empty;
    public List<string> Members { get; set; } = [];
}

/// <summary>
/// Per-archetype terminology container: term definitions, constraint
/// definitions, value-sets and external terminology / constraint
/// bindings, plus the original-language and translation list.
/// </summary>
public sealed class ArchetypeTerminology
{
    public string OriginalLanguage { get; set; } = string.Empty;
    public List<string>? Translations { get; set; }

    /// <summary>
    /// <c>language → at-code → ArchetypeTerm</c>.
    /// </summary>
    public Dictionary<string, Dictionary<string, ArchetypeTerm>> TermDefinitions { get; set; } = [];

    /// <summary>
    /// <c>language → ac-code → ArchetypeTerm</c>.
    /// </summary>
    public Dictionary<string, Dictionary<string, ArchetypeTerm>> ConstraintDefinitions { get; set; } = [];

    /// <summary>
    /// <c>value-set id → ValueSet</c>.
    /// </summary>
    public Dictionary<string, ValueSet> ValueSets { get; set; } = [];

    /// <summary>
    /// <c>terminology → at-code → bound external code uri</c>.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> TermBindings { get; set; } = [];

    /// <summary>
    /// <c>terminology → ac-code → bound external constraint uri</c>.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> ConstraintBindings { get; set; } = [];
}
