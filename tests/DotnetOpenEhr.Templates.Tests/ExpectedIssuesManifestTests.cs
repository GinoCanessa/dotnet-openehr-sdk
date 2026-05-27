using System.Reflection;
using System.Text;
using System.Text.Json;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Serialization.Json;
using DotnetOpenEhr.Templates;
using DotnetOpenEhr.Templates.Tests.ExpectedIssues;
using DotnetOpenEhr.Templates.Validation;
using Xunit;

namespace DotnetOpenEhr.Templates.Tests;

/// <summary>
/// Pins <see cref="OperationalTemplateValidator"/> behaviour against
/// hand-curated <c>(template.opt2, composition.json, expected-issues.json)</c>
/// triples. Each scenario asserts set-equality between the issues the
/// validator emits and the manifest, projected to
/// <c>(Path, RuleId, Severity)</c>. The assertion message reports the
/// symmetric difference so regressions (both missed issues and
/// unexpected new ones) are immediately localised.
/// </summary>
public sealed class ExpectedIssuesManifestTests
{
    private static readonly OperationalTemplateValidator s_validator = new();

    public static TheoryData<string> Scenarios { get; } =
    [
        "blood_pressure_valid",
        "blood_pressure_missing_mandatory",
        "blood_pressure_magnitude_out_of_range",
        "blood_pressure_wrong_units",
        "blood_pressure_extra_occurrence",
        "blood_pressure_value_set_violation",
    ];

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void ManifestMatchesActualIssues(string scenarioName)
    {
        string templateBody = LoadResourceText($"{scenarioName}.template.opt2");
        byte[] compositionJson = LoadResourceBytes($"{scenarioName}.composition.json");
        byte[] manifestJson = LoadResourceBytes($"{scenarioName}.expected-issues.json");

        OperationalTemplate template = Opt2Parser.Parse(templateBody);
        Composition composition = OpenEhrJson.ParseComposition(compositionJson)
            ?? throw new InvalidOperationException(
                $"composition.json for '{scenarioName}' parsed as null.");

        IReadOnlyList<ValidationIssue> actualIssues = s_validator.Validate(
            composition, template, TestContext.Current.CancellationToken);

        ExpectedIssueEntry[] expectedEntries = JsonSerializer.Deserialize(
            manifestJson, ExpectedIssuesContext.Default.ExpectedIssueEntryArray)
            ?? throw new InvalidOperationException(
                $"expected-issues.json for '{scenarioName}' parsed as null.");

        HashSet<(string Path, string RuleId, string Severity)> expected = [];
        foreach (ExpectedIssueEntry e in expectedEntries)
        {
            expected.Add((e.Path, e.RuleId, e.Severity));
        }

        HashSet<(string Path, string RuleId, string Severity)> actual = [];
        foreach (ValidationIssue i in actualIssues)
        {
            actual.Add((i.Path, i.RuleId, i.Severity.ToString()));
        }

        if (expected.SetEquals(actual))
        {
            return;
        }

        List<(string Path, string RuleId, string Severity)> missing = [];
        foreach ((string Path, string RuleId, string Severity) e in expected)
        {
            if (!actual.Contains(e))
            {
                missing.Add(e);
            }
        }

        List<(string Path, string RuleId, string Severity)> unexpected = [];
        foreach ((string Path, string RuleId, string Severity) a in actual)
        {
            if (!expected.Contains(a))
            {
                unexpected.Add(a);
            }
        }

        StringBuilder sb = new();
        sb.AppendLine($"Scenario '{scenarioName}': validator issue set does not match manifest.");
        sb.AppendLine($"  Expected: {expected.Count}");
        sb.AppendLine($"  Actual:   {actual.Count}");

        if (missing.Count > 0)
        {
            sb.AppendLine($"  Missing (expected but not produced) [{missing.Count}]:");
            foreach ((string Path, string RuleId, string Severity) m in missing)
            {
                sb.AppendLine($"    - {m.Severity} {m.RuleId} @ {m.Path}");
            }
        }

        if (unexpected.Count > 0)
        {
            sb.AppendLine($"  Unexpected (produced but not in manifest) [{unexpected.Count}]:");
            foreach ((string Path, string RuleId, string Severity) u in unexpected)
            {
                sb.AppendLine($"    + {u.Severity} {u.RuleId} @ {u.Path}");
            }
        }

        Assert.Fail(sb.ToString());
    }

    private static string LoadResourceText(string relative)
    {
        using Stream stream = OpenResource(relative);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private static byte[] LoadResourceBytes(string relative)
    {
        using Stream stream = OpenResource(relative);
        using MemoryStream memory = new();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static Stream OpenResource(string relative)
    {
        string resourceName =
            $"DotnetOpenEhr.Templates.Tests.Fixtures.ExpectedIssuesManifest.{relative}";
        Assembly assembly = typeof(ExpectedIssuesManifestTests).Assembly;
        return assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource not found: {resourceName}");
    }
}
