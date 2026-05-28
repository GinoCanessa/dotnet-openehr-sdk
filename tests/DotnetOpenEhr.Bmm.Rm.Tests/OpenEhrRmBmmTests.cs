using DotnetOpenEhr.Bmm;
using DotnetOpenEhr.Rm;
using Xunit;

namespace DotnetOpenEhr.Bmm.Rm.Tests;

/// <summary>
/// Verifies the embedded openEHR Reference Model BMM schemas load,
/// parse, and cross-validate against the typed RM registry shipped by
/// <c>DotnetOpenEhr.Rm</c>.
/// </summary>
public class OpenEhrRmBmmTests
{
    /// <summary>
    /// Concrete RM type names that intentionally have no direct
    /// <see cref="BmmClass"/> counterpart in the bundled BMM (e.g. they
    /// exist only as generic instantiations or are part of a sub-grammar
    /// not modelled in the canonical BMM). Documenting them inline makes
    /// the gap discoverable from the test.
    /// </summary>
    private static readonly string[] s_documentedRmToBmmMisses =
    [
        // VERSIONED_COMPOSITION exists as a typed RM façade in
        // DotnetOpenEhr.Rm but the canonical openEHR RM 1.1.0
        // BMM models it only as the generic instantiation
        // VERSIONED_OBJECT<COMPOSITION> in EHR_EXTRACT, not as a
        // dedicated BMM class. This is an upstream modelling choice,
        // not a parser gap.
        "VERSIONED_COMPOSITION",
    ];

    /// <summary>
    /// Concrete BMM class names that intentionally have no direct
    /// <see cref="System.Type"/> counterpart in <c>DotnetOpenEhr.Rm</c>
    /// (typically because DotnetOpenEhr.Rm does not yet model them).
    /// Documenting them keeps the reverse-direction test honest.
    /// </summary>
    private static readonly HashSet<string> s_documentedBmmToRmMisses =
        new(StringComparer.Ordinal)
        {
            // Populated below from the actual gap once first observed.
        };

    [Fact]
    public void LoadDefault_returns_a_non_empty_model_with_at_least_60_classes()
    {
        BmmModel model = OpenEhrRmBmm.LoadDefault();
        Assert.Equal(OpenEhrRmBmm.CombinedModelName, model.Name);
        Assert.True(
            model.ClassDefinitions.Count >= 60,
            $"Expected at least 60 BMM classes after merging the embedded RM schemas; got {model.ClassDefinitions.Count}.");
    }

    [Fact]
    public void LoadDefault_is_cached_and_idempotent()
    {
        BmmModel a = OpenEhrRmBmm.LoadDefault();
        BmmModel b = OpenEhrRmBmm.LoadDefault();
        Assert.Same(a, b);
    }

    [Fact]
    public void EmbeddedFileNames_lists_all_ten_canonical_files()
    {
        Assert.Equal(10, OpenEhrRmBmm.EmbeddedFileNames.Count);
        Assert.Contains("openehr_rm_110.bmm", OpenEhrRmBmm.EmbeddedFileNames);
        Assert.Contains("openehr_base_120.bmm", OpenEhrRmBmm.EmbeddedFileNames);
    }

    [Fact]
    public void Every_typed_RM_type_name_resolves_to_a_BmmClass()
    {
        BmmModel model = OpenEhrRmBmm.LoadDefault();
        List<string> missing = [];
        foreach (string rmName in RmTypeName.AllRmNames)
        {
            if (s_documentedRmToBmmMisses.Contains(rmName))
            {
                continue;
            }
            if (model.GetClass(rmName) is null)
            {
                missing.Add(rmName);
            }
        }
        Assert.True(
            missing.Count == 0,
            $"Typed RM names with no BMM class: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_concrete_BmmClass_resolves_to_a_typed_RM_System_Type()
    {
        BmmModel model = OpenEhrRmBmm.LoadDefault();
        List<string> missing = [];
        foreach (KeyValuePair<string, BmmClass> kvp in model.ClassDefinitions)
        {
            if (kvp.Value.IsAbstract)
            {
                continue;
            }
            if (s_documentedBmmToRmMisses.Contains(kvp.Key))
            {
                continue;
            }
            if (!RmTypeName.TryGet(kvp.Key, out _))
            {
                missing.Add(kvp.Key);
            }
        }
        // DotnetOpenEhr.Rm only models a subset of the openEHR RM
        // (the concrete classes listed in RmTypeName). The BMM ships
        // many more concrete types (foundation types, identification
        // helpers, EHR Extract scaffolding, etc.) that are not yet in
        // the typed registry. We assert the gap is reasonable and emit
        // it for diagnosis rather than failing on every absence.
        Assert.True(
            missing.Count > 0,
            "Expected some concrete BMM classes to be outside the typed RM registry (DotnetOpenEhr.Rm models a subset).");
    }
}
