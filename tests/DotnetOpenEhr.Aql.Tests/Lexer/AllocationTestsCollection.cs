using Xunit;

namespace DotnetOpenEhr.Aql.Tests.Lexer;

/// <summary>
/// Test collection that disables parallelization for the small set of
/// allocation-budget assertions in this project. Per-thread allocation
/// counters are unreliable when another test runs on the same scheduled
/// thread, so we pin these tests to serial execution.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AllocationTestsCollection
{
    public const string Name = "AllocationTests";
}
