using System.IO;
using System.Reflection;
using DotnetOpenEhr.Serialization.Json.Flat;

namespace DotnetOpenEhr.Serialization.Json.Flat.Tests;

internal static class FixtureLoader
{
    private const string Prefix = "DotnetOpenEhr.Serialization.Json.Flat.Tests.Fixtures.Flat.";

    public static IReadOnlyList<string> AllFixtureNames { get; } = LoadNames();

    public static byte[] Load(string fixtureName)
    {
        using Stream s = typeof(FixtureLoader).Assembly.GetManifestResourceStream(Prefix + fixtureName)
            ?? throw new InvalidOperationException($"Embedded fixture '{fixtureName}' missing.");
        using MemoryStream ms = new();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static string[] LoadNames()
    {
        Assembly asm = typeof(FixtureLoader).Assembly;
        return [.. asm.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)
                     && n.EndsWith(".json", StringComparison.Ordinal))
            .Select(n => n.Substring(Prefix.Length))
            .OrderBy(n => n, StringComparer.Ordinal)];
    }
}
