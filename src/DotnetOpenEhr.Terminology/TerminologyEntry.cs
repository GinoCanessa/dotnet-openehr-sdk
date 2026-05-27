namespace DotnetOpenEhr.Terminology;

/// <summary>
/// A single concept entry in an openEHR Support Terminology group:
/// a code (e.g. <c>"253"</c>) and its English rubric (e.g. <c>"unknown"</c>),
/// plus an optional human-readable description when the spec provides one.
/// </summary>
public sealed record TerminologyEntry(string Code, string Rubric, string? Description = null);
