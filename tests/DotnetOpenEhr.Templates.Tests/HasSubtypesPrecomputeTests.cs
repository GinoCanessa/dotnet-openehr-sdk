using System.Collections.Frozen;
using System.Collections.Generic;
using DotnetOpenEhr.Bmm;
using DotnetOpenEhr.Bmm.Rm;
using Xunit;

namespace DotnetOpenEhr.Templates.Tests;

/// <summary>
/// M3 — verifies the per-BmmModel <c>HasSubtypes</c> set is computed
/// once and reused on every subsequent call against the same model,
/// and that <c>ExtractElementTypeName</c> returns the generic element
/// for container-shaped properties like <c>INTERVAL&lt;DV_QUANTITY&gt;</c>.
/// </summary>
public sealed class HasSubtypesPrecomputeTests
{
    [Fact]
    public void HasSubtypes_isComputedOncePerBmm()
    {
        BmmModel bmm = OpenEhrRmBmm.LoadDefault();

        FrozenSet<string> first = OperationalTemplate.GetHasSubtypesSet(bmm);
        FrozenSet<string> second = OperationalTemplate.GetHasSubtypesSet(bmm);
        FrozenSet<string> third = OperationalTemplate.GetHasSubtypesSet(bmm);

        // Reference equality — the cache returned the same FrozenSet
        // instance for every call, proving the BMM walk happens once.
        Assert.Same(first, second);
        Assert.Same(first, third);

        // Sanity: well-known polymorphic RM bases are present.
        Assert.Contains("DATA_VALUE", first, System.StringComparer.OrdinalIgnoreCase);
        Assert.Contains("ITEM_STRUCTURE", first, System.StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractElementTypeName_returnsGenericElement()
    {
        BmmGenericType interval = new(
            "INTERVAL",
            [new BmmSimpleType("DV_QUANTITY")]);

        string element = OperationalTemplate.ExtractElementTypeName(interval);

        Assert.Equal("DV_QUANTITY", element);
    }

    [Fact]
    public void ExtractElementTypeName_returnsContainerElement()
    {
        BmmContainerType list = new(
            "List",
            [new BmmSimpleType("CLUSTER")]);

        string element = OperationalTemplate.ExtractElementTypeName(list);

        Assert.Equal("CLUSTER", element);
    }

    [Fact]
    public void ExtractElementTypeName_returnsSimpleName()
    {
        BmmSimpleType simple = new("DV_TEXT");

        string element = OperationalTemplate.ExtractElementTypeName(simple);

        Assert.Equal("DV_TEXT", element);
    }
}
