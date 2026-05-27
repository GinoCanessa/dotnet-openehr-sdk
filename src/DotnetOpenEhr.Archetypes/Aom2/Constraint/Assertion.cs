namespace DotnetOpenEhr.Archetypes.Aom2.Constraint;

// SPEC: AOM2.html — Assertion class. The rule sub-grammar is deferred to
// a follow-up phase; we capture each assertion as raw ADL text with an
// optional tag.

/// <summary>
/// An archetype assertion (rule / invariant / include / exclude
/// expression). The expression sub-grammar is captured as raw text in
/// v1.
/// </summary>
public sealed class Assertion
{
    public string? Tag { get; set; }
    public string RawText { get; set; } = string.Empty;
}
