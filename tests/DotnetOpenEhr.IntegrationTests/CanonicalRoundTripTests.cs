using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Serialization.Json;
using Xunit;

namespace DotnetOpenEhr.IntegrationTests;

/// <summary>
/// Integration tests for the canonical openEHR JSON round-trip. The
/// gating bar is structural equivalence (parse → re-serialize → parse →
/// deep-equal). Byte equivalence after pragmatic normalisation is a
/// best-effort signal; see <c>docs/canonical-json-ordering.md</c> for
/// rationale.
/// </summary>
public sealed class CanonicalRoundTripTests
{
    public static IEnumerable<TheoryDataRow<string>> FixtureNames()
        => FixtureLoader.AllFixtureNames.Select(n => new TheoryDataRow<string>(n));

    [Theory]
    [MemberData(nameof(FixtureNames))]
    public void Structural_Equivalence_RoundTrips(string fixtureName)
    {
        byte[] input = FixtureLoader.Load(fixtureName);

        Composition? first = OpenEhrJson.ParseComposition(input);
        Assert.NotNull(first);

        byte[] reSerialized = OpenEhrJson.Serialize(first!);
        Composition? second = OpenEhrJson.ParseComposition(reSerialized);
        Assert.NotNull(second);

        bool equal = RmEquality.AreEqual(first, second, out string diffPath);
        Assert.True(equal,
            $"Structural equivalence broke for '{fixtureName}'. First difference at: {diffPath}");
    }

    [Theory]
    [MemberData(nameof(FixtureNames))]
    public void ByteEquivalence_AfterNormalisation_BestEffort(string fixtureName)
    {
        byte[] input = FixtureLoader.Load(fixtureName);

        Composition? composition = OpenEhrJson.ParseComposition(input);
        Assert.NotNull(composition);

        byte[] reSerialized = OpenEhrJson.Serialize(composition!);
        byte[] normalisedInput = CanonicalJsonNormaliser.Normalise(input);
        byte[] normalisedOutput = CanonicalJsonNormaliser.Normalise(reSerialized);

        if (!normalisedInput.AsSpan().SequenceEqual(normalisedOutput))
        {
            // Best-effort: log diff size, do not fail. Structural equivalence is the gate.
            int diffBytes = Math.Abs(normalisedInput.Length - normalisedOutput.Length);
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"[byte-diff] {fixtureName}: normalised input={normalisedInput.Length}B "
                + $"output={normalisedOutput.Length}B "
                + $"abs-length-diff={diffBytes}B (best-effort; see docs/canonical-json-ordering.md).");
        }
        else
        {
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"[byte-equal] {fixtureName}: {normalisedInput.Length}B normalised.");
        }
    }
}
