using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using DotnetOpenEhr.Templates.Abstractions;
using Xunit;

namespace DotnetOpenEhr.Templates.Tests;

/// <summary>
/// Phase-3 coverage: <see cref="Opt14XmlParser"/> walks the OPT1.4
/// <c>&lt;definition&gt;</c> tree, materialises the AOM2 constraint
/// graph, and populates the <see cref="OperationalTemplate.Nodes"/>
/// index via <see cref="OperationalTemplate.Initialize"/>.
/// </summary>
public sealed partial class Opt14XmlParserTests
{
    // (fixture, expected root RM type, sample FLAT path, expected RM type at that path)
    public static readonly System.Collections.Generic.IEnumerable<object[]> FixtureFlatRows =
    [
        ["KDS_Vitalstatus.opt", "COMPOSITION", "report/category",      "DV_CODED_TEXT"],
        ["KDS_Diagnose.opt",    "COMPOSITION", "report/category",      "DV_CODED_TEXT"],
        ["KDS_Person.opt",      "COMPOSITION", "person/category",      "DV_CODED_TEXT"],
        ["Blood Pressure.opt",  "COMPOSITION", "blood_pressure/category", "DV_CODED_TEXT"],
    ];

    [Theory]
    [MemberData(nameof(FixtureFlatRows))]
    public void Fixture_TryResolveType_finds_root(
        string fixture, string expectedRootRm, string flatPath, string expectedFlatRm)
    {
        _ = flatPath;
        _ = expectedFlatRm;
        OperationalTemplate opt = Opt14XmlParser.Parse(ReadFixture(fixture));
        Assert.Equal(expectedRootRm, opt.Definition.RmTypeName);
        bool ok = opt.TryResolveType(opt.TemplateId.AsSpan(), out TemplateRmTypeResolution res);
        Assert.True(ok, $"{fixture}: TryResolveType('{opt.TemplateId}') should resolve to the root RM type.");
        Assert.Equal(expectedRootRm, res.RmTypeName);
    }

    [Theory]
    [MemberData(nameof(FixtureFlatRows))]
    public void Fixture_definition_recurses_to_known_FLAT_path(
        string fixture, string expectedRootRm, string flatPath, string expectedFlatRm)
    {
        _ = expectedRootRm;
        OperationalTemplate opt = Opt14XmlParser.Parse(ReadFixture(fixture));
        bool ok = opt.TryResolveType(flatPath.AsSpan(), out TemplateRmTypeResolution res);
        Assert.True(ok, $"{fixture}: TryResolveType('{flatPath}') should succeed once the definition is recursed.");
        Assert.Equal(expectedFlatRm, res.RmTypeName);
    }

    [Fact]
    public void Each_fixture_nodes_count_exceeds_recursion_floor()
    {
        // A bare top-level Initialize without recursion yields 1
        // (just the root). Anything well above that proves the
        // <attributes>/<children> walk fired.
        foreach (string name in s_fixtureNames)
        {
            OperationalTemplate opt = Opt14XmlParser.Parse(ReadFixture(name));
            Assert.True(opt.Nodes.Count >= 5,
                $"{name}: expected at least 5 nodes after definition recursion, got {opt.Nodes.Count}.");
        }
    }

    [Fact]
    public void KDS_Diagnose_primitive_object_wrapper_is_unwrapped()
    {
        // C_PRIMITIVE_OBJECT wraps <item xsi:type="C_STRING">; without
        // unwrap the inner CString never surfaces in the tree. We walk
        // every CObject in the resulting Definition and assert at least
        // one CString instance exists (KDS_Diagnose uses 14 such wrappers).
        OperationalTemplate opt = Opt14XmlParser.Parse(ReadFixture("KDS_Diagnose.opt"));
        int cstringCount = CountInTree<CString>(opt.Definition);
        Assert.True(cstringCount > 0,
            $"Expected at least one CString in KDS_Diagnose definition tree (C_PRIMITIVE_OBJECT wrapper unwrap), got {cstringCount}.");
    }

    [Fact]
    public void KDS_Vitalstatus_archetype_root_children_recognised()
    {
        // The vitalstatus fixture has two C_ARCHETYPE_ROOT nodes
        // (composed CLUSTER + composed EVALUATION). Both should
        // materialise as CArchetypeRoot instances with ArchetypeRef
        // populated.
        OperationalTemplate opt = Opt14XmlParser.Parse(ReadFixture("KDS_Vitalstatus.opt"));
        System.Collections.Generic.List<CArchetypeRoot> roots = [];
        CollectFromTree<CArchetypeRoot>(opt.Definition, roots);
        Assert.True(roots.Count >= 2,
            $"Expected ≥2 CArchetypeRoot nodes in KDS_Vitalstatus, got {roots.Count}.");
        Assert.Contains(roots, r => r.ArchetypeRef == "openEHR-EHR-EVALUATION.vital_status.v1");
        Assert.Contains(roots, r => r.ArchetypeRef == "openEHR-EHR-CLUSTER.case_identification.v0");
    }

    [Fact]
    public void Blood_Pressure_includes_archetype_slot()
    {
        // Blood Pressure.opt has 5 ARCHETYPE_SLOT children; verify the
        // ArchetypeSlot type round-trips with at least one includes
        // assertion captured as raw text.
        OperationalTemplate opt = Opt14XmlParser.Parse(ReadFixture("Blood Pressure.opt"));
        System.Collections.Generic.List<ArchetypeSlot> slots = [];
        CollectFromTree<ArchetypeSlot>(opt.Definition, slots);
        Assert.True(slots.Count >= 1,
            $"Expected ≥1 ArchetypeSlot in Blood Pressure, got {slots.Count}.");
        Assert.Contains(slots, s => s.Includes.Count > 0 && !string.IsNullOrEmpty(s.Includes[0].RawText));
    }

    // ----- tree walker helpers ---------------------------------------

    private static int CountInTree<T>(CObject node) where T : CObject
    {
        int count = node is T ? 1 : 0;
        if (node is CComplexObject cco)
        {
            foreach (CAttribute attr in cco.Attributes)
            {
                foreach (CObject child in attr.Children)
                {
                    count += CountInTree<T>(child);
                }
            }
        }
        return count;
    }

    private static void CollectFromTree<T>(CObject node, System.Collections.Generic.List<T> sink) where T : CObject
    {
        if (node is T t)
        {
            sink.Add(t);
        }
        if (node is CComplexObject cco)
        {
            foreach (CAttribute attr in cco.Attributes)
            {
                foreach (CObject child in attr.Children)
                {
                    CollectFromTree<T>(child, sink);
                }
            }
        }
    }
}
