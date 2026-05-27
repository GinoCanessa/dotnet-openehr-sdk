using System.Text.Json.Serialization;

namespace DotnetOpenEhr.Templates.Tests.ExpectedIssues;

/// <summary>
/// Single row of an <c>expected-issues.json</c> manifest:
/// the AQL path of the offending Composition node, the rule id, and
/// the rule severity (as a string — <c>"Error"</c>, <c>"Warning"</c>,
/// or <c>"NotValidated"</c>).
/// </summary>
internal sealed record ExpectedIssueEntry(string Path, string RuleId, string Severity);

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> used by
/// <see cref="ExpectedIssuesManifestTests"/> to deserialize the
/// per-scenario <c>expected-issues.json</c> manifests with no
/// reflection — keeps the test project trim/AOT-clean.
/// </summary>
[JsonSerializable(typeof(ExpectedIssueEntry[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class ExpectedIssuesContext : JsonSerializerContext;
