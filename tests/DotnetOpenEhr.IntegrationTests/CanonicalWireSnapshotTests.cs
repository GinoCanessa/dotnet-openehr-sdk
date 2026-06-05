using System.IO;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Serialization.Json;
using Xunit;

namespace DotnetOpenEhr.IntegrationTests;

/// <summary>
/// H13 — canonical-JSON byte snapshot. For every fixture under
/// <c>Fixtures/Canonical/</c>, parse → re-serialize must produce
/// bytes that match the checked-in <c>&lt;fixture&gt;.expected.json</c>
/// file. This pins the wire-format contract so a regression in
/// property naming, ordering, or null-omission is caught the moment
/// a developer runs the suite.
/// </summary>
/// <remarks>
/// To regenerate the expected snapshot files (e.g. after an
/// intentional wire-format change), set
/// <c>OPENEHR_REGENERATE_CANONICAL_SNAPSHOTS=1</c> and rerun the
/// suite. The test will overwrite each <c>.expected.json</c> file
/// and then fail with a "regenerated" message so a follow-up clean
/// run is required to confirm. See
/// <c>docs/canonical-json-ordering.md</c> for the workflow.
/// </remarks>
public sealed class CanonicalWireSnapshotTests
{
    private const string RegenEnvVar = "OPENEHR_REGENERATE_CANONICAL_SNAPSHOTS";

    public static IEnumerable<TheoryDataRow<string>> FixtureNames()
        => FixtureLoader.AllFixtureNames.Select(n => new TheoryDataRow<string>(n));

    [Theory]
    [MemberData(nameof(FixtureNames))]
    public void Serialize_TheoryOverFixtures_MatchesCheckedInSnapshot(string fixtureName)
    {
        byte[] input = FixtureLoader.Load(fixtureName);

        Composition? parsed = OpenEhrJson.ParseComposition(input);
        Assert.NotNull(parsed);

        byte[] reSerialized = OpenEhrJson.Serialize(parsed!);

        string expectedPath = ResolveExpectedPath(fixtureName);

        if (string.Equals(Environment.GetEnvironmentVariable(RegenEnvVar), "1", StringComparison.Ordinal))
        {
            File.WriteAllBytes(expectedPath, reSerialized);
            Assert.Fail($"Regenerated canonical snapshot at '{expectedPath}'. Unset {RegenEnvVar} and rerun to verify.");
        }

        Assert.True(File.Exists(expectedPath),
            $"Expected snapshot missing: {expectedPath}. Run with {RegenEnvVar}=1 to bootstrap.");

        byte[] expected = File.ReadAllBytes(expectedPath);
        Assert.Equal(expected, reSerialized);
    }

    private static string ResolveExpectedPath(string fixtureName)
    {
        string baseName = Path.GetFileNameWithoutExtension(fixtureName);
        string expectedName = $"{baseName}.expected.json";

        string assemblyDir = Path.GetDirectoryName(typeof(CanonicalWireSnapshotTests).Assembly.Location)
            ?? throw new InvalidOperationException("Could not locate test assembly directory.");
        DirectoryInfo? dir = new(assemblyDir);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            string candidateDir = Path.Combine(dir.FullName, "Fixtures", "Canonical");
            if (Directory.Exists(candidateDir))
            {
                return Path.Combine(candidateDir, expectedName);
            }
        }
        throw new InvalidOperationException(
            "Could not locate the Fixtures/Canonical directory under the test project root.");
    }
}
