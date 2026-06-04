using System.IO;
using System.Linq;
using System.Reflection;
using DotnetOpenEhr.Templates.Abstractions;
using Xunit;

namespace DotnetOpenEhr.Templates.Tests;

/// <summary>
/// H3 — verifies <see cref="OperationalTemplate.TryResolveType"/> uses
/// the frozen-dictionary span-keyed alternate lookup and does not
/// allocate per call. Marked with <c>[Collection("AllocationTests")]</c>
/// so per-thread allocation counters are stable.
/// </summary>
[Collection(AllocationTestsCollection.Name)]
public sealed class OperationalTemplateAllocationTests
{
    private static string LoadFixture(string name)
    {
        Assembly asm = typeof(OperationalTemplateAllocationTests).Assembly;
        string? match = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(name, System.StringComparison.Ordinal));
        if (match is null)
        {
            throw new FileNotFoundException($"Embedded fixture '{name}' not found.");
        }
        using Stream s = asm.GetManifestResourceStream(match)!;
        using StreamReader r = new(s);
        return r.ReadToEnd();
    }

    [Fact]
    public void TryResolveType_DoesNotAllocate()
    {
        OperationalTemplate opt = Opt2Parser.Parse(LoadFixture("minimal_vitals.opt2"));

        // Probe paths: at least one known hit and one miss. The exact
        // FLAT path strings depend on the fixture's template id; we
        // pluck the first known node from the materialised index so the
        // test does not bake in a brittle string.
        string knownPath = opt.Nodes.First().FlatPath;
        string missPath = "does/not/exist/" + System.Guid.NewGuid().ToString("N");

        // Warm-up — JIT, FrozenDictionary cold paths, any static-init.
        for (int i = 0; i < 1000; i++)
        {
            _ = opt.TryResolveType(knownPath.AsSpan(), out _);
            _ = opt.TryResolveType(missPath.AsSpan(), out _);
        }

        // Measure.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        const int N = 1000;
        for (int i = 0; i < N; i++)
        {
            _ = opt.TryResolveType(knownPath.AsSpan(), out _);
            _ = opt.TryResolveType(missPath.AsSpan(), out _);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();
        long delta = after - before;

        Assert.True(
            delta == 0,
            $"TryResolveType allocated {delta} bytes over {N * 2} calls.");
    }
}
