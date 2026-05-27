namespace DotnetOpenEhr.Serialization.Json.Flat;

/// <summary>
/// Thrown by schemaless FLAT parsing when one or more paths cannot be
/// resolved to a concrete RM type without a template (OPT) schema.
/// Carries the offending paths in <see cref="UnresolvedPaths"/> so the
/// caller can surface them in diagnostics or feed them to a schema
/// lookup.
/// </summary>
public sealed class FlatSchemaRequiredException : Exception
{
    /// <summary>
    /// Initialises the exception with the unresolved paths and the
    /// template id of the offending document.
    /// </summary>
    public FlatSchemaRequiredException(string templateId, IReadOnlyList<string> unresolvedPaths)
        : base(BuildMessage(templateId, unresolvedPaths))
    {
        TemplateId = templateId;
        UnresolvedPaths = unresolvedPaths;
    }

    /// <summary>The template id discovered from the FLAT document, if any.</summary>
    public string TemplateId { get; }

    /// <summary>Distinct FLAT paths the schemaless parser refused to resolve.</summary>
    public IReadOnlyList<string> UnresolvedPaths { get; }

    private static string BuildMessage(string templateId, IReadOnlyList<string> unresolvedPaths)
    {
        ArgumentNullException.ThrowIfNull(unresolvedPaths);
        int shown = Math.Min(unresolvedPaths.Count, 5);
        string sample = string.Join(", ", unresolvedPaths.Take(shown));
        string suffix = unresolvedPaths.Count > shown ? $" (+{unresolvedPaths.Count - shown} more)" : string.Empty;
        return $"Schemaless FLAT parse for template '{templateId}' cannot resolve {unresolvedPaths.Count} path(s): {sample}{suffix}.";
    }
}
