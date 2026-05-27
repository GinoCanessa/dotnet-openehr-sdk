using System.Reflection;

namespace DotnetOpenEhr.IntegrationTests;

/// <summary>
/// Helpers for loading embedded canonical openEHR JSON Composition
/// fixtures that ship with this test project under
/// <c>Fixtures/Canonical/</c>.
/// </summary>
internal static class FixtureLoader
{
    private const string FixturePrefix = "DotnetOpenEhr.IntegrationTests.Fixtures.Canonical.";

    /// <summary>
    /// All canonical fixture filenames (e.g. <c>growth_chart.json</c>).
    /// Order is stable across runs to make test output reproducible.
    /// </summary>
    public static IReadOnlyList<string> AllFixtureNames { get; } = LoadNames();

    /// <summary>
    /// Returns the raw UTF-8 bytes of the named fixture, preserving
    /// whitespace and ordering exactly as shipped upstream.
    /// </summary>
    public static byte[] Load(string fixtureName)
    {
        using Stream stream = typeof(FixtureLoader).Assembly
            .GetManifestResourceStream(FixturePrefix + fixtureName)
            ?? throw new InvalidOperationException(
                $"Embedded fixture '{fixtureName}' is missing from the test assembly.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string[] LoadNames()
    {
        Assembly asm = typeof(FixtureLoader).Assembly;
        return [.. asm.GetManifestResourceNames()
            .Where(n => n.StartsWith(FixturePrefix, StringComparison.Ordinal)
                     && n.EndsWith(".json", StringComparison.Ordinal))
            .Select(n => n.Substring(FixturePrefix.Length))
            .OrderBy(n => n, StringComparer.Ordinal)];
    }
}
