namespace DotnetOpenEhr.Archetypes.Aom2;

// SPEC: AOM2.html#_rules_section_class — the archetype rules sub-grammar
// is captured as raw text in v1; a structured rules AST is deferred.

/// <summary>
/// The <c>rules</c> section of an archetype, captured as raw text in
/// v1. A structured rules AST is deferred to a follow-up phase.
/// </summary>
public sealed class RulesSection
{
    public string RawText { get; set; } = string.Empty;
}
