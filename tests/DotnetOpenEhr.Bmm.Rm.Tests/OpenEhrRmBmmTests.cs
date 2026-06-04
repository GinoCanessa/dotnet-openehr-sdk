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
    private static readonly string[] s_documentedBmmToRmMisses =
    [
        "ACCESS_GROUP_REF",
        "ADDRESSED_MESSAGE",
        "CONTRIBUTION",
        "DV_GENERAL_TIME_SPECIFICATION",
        "DV_INTERVAL",
        "DV_PARAGRAPH",
        "DV_PERIODIC_TIME_SPECIFICATION",
        "EXTRACT",
        "EXTRACT_ACTION_REQUEST",
        "EXTRACT_CHAPTER",
        "EXTRACT_ENTITY_CHAPTER",
        "EXTRACT_ENTITY_MANIFEST",
        "EXTRACT_FOLDER",
        "EXTRACT_MANIFEST",
        "EXTRACT_PARTICIPATION",
        "EXTRACT_REQUEST",
        "EXTRACT_SPEC",
        "EXTRACT_UPDATE_SPEC",
        "EXTRACT_VERSION_SPEC",
        "FOLDER",
        "GENERIC_CONTENT_ITEM",
        "GENERIC_ENTRY",
        "IMPORTED_VERSION",
        "INTERNET_ID",
        "ISO_OID",
        "LOCATABLE_REF",
        "MESSAGE",
        "OPENEHR_CONTENT_ITEM",
        "PARTY_RELATIONSHIP",
        "PROPORTION_KIND",
        "RESOURCE_ANNOTATIONS",
        "RESOURCE_DESCRIPTION",
        "RESOURCE_DESCRIPTION_ITEM",
        "REVISION_HISTORY",
        "REVISION_HISTORY_ITEM",
        "SYNC_EXTRACT",
        "SYNC_EXTRACT_REQUEST",
        "SYNC_EXTRACT_SPEC",
        "TRANSLATION_DETAILS",
        "UUID",
        "VALIDITY_KIND",
        "VERSIONED_OBJECT",
        "VERSION_STATUS",
        "VERSION_TREE_ID",
        "X_CONTRIBUTION",
        "X_VERSIONED_COMPOSITION",
        "X_VERSIONED_EHR_ACCESS",
        "X_VERSIONED_EHR_STATUS",
        "X_VERSIONED_FOLDER",
        "X_VERSIONED_OBJECT",
        "X_VERSIONED_PARTY",
    ];

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
            if (!RmTypeName.TryGet(kvp.Key, out _))
            {
                missing.Add(kvp.Key);
            }
        }
        missing.Sort(StringComparer.Ordinal);
        Assert.Equal(s_documentedBmmToRmMisses, missing);
    }

    // -- M17 — deep-shape characterization --------------------------------

    private static BmmProperty PropertyOf(string className, string propertyName)
    {
        BmmModel model = OpenEhrRmBmm.LoadDefault();
        BmmClass? cls = model.GetClass(className);
        Assert.NotNull(cls);
        BmmClass walker = cls!;
        while (true)
        {
            if (walker.Properties.TryGetValue(propertyName, out BmmProperty? hit))
            {
                return hit;
            }
            if (walker.Ancestors.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Property '{propertyName}' not found on '{className}' or any ancestor.");
            }
            BmmClass? parent = model.GetClass(walker.Ancestors[0]);
            Assert.NotNull(parent);
            walker = parent!;
        }
    }

    [Fact]
    public void Composition_context_property_resolves_to_expected_type()
    {
        BmmProperty p = PropertyOf("COMPOSITION", "context");
        Assert.Equal("EVENT_CONTEXT", p.Type.TypeName);
    }

    [Fact]
    public void Observation_data_property_is_history_container()
    {
        BmmProperty p = PropertyOf("OBSERVATION", "data");
        // OBSERVATION.data is HISTORY<ITEM_STRUCTURE> — a generic
        // shape whose outer name is HISTORY.
        Assert.Equal("HISTORY", p.Type.TypeName);
    }

    [Fact]
    public void Locatable_archetype_node_id_is_mandatory_string()
    {
        BmmProperty p = PropertyOf("LOCATABLE", "archetype_node_id");
        Assert.Equal("String", p.Type.TypeName);
        Assert.True(p.IsMandatory, "archetype_node_id must be mandatory on LOCATABLE.");
    }

    [Fact]
    public void Cluster_items_is_p_list_with_minimum_one()
    {
        BmmProperty p = PropertyOf("CLUSTER", "items");
        // CLUSTER.items is a P_List/List of ITEM with at least one entry.
        BmmContainerType container = Assert.IsType<BmmContainerType>(p.Type);
        Assert.NotEmpty(container.TypeArguments);
        // Cardinality lower-bound is 1.
        Assert.NotNull(p.Cardinality);
        Assert.True(
            p.Cardinality!.Interval.HasLower && p.Cardinality.Interval.Lower >= 1,
            $"Expected cardinality lower-bound ≥1; got {p.Cardinality.Interval}");
    }

    // -- M28 — embedded file enumeration pinning --------------------------

    [Fact]
    public void EmbeddedFileNames_enumerates_in_declaration_order()
    {
        // The set's documented intent is "ten canonical files spanning
        // base + RM"; enumeration ordering is implementation-defined on
        // FrozenSet, so the pin is on set membership + count rather than
        // index-by-index sequence.
        string[] expected =
        [
            "openehr_base_120.bmm",
            "openehr_base_base_types_120.bmm",
            "openehr_base_foundation_types_120.bmm",
            "openehr_base_resource_120.bmm",
            "openehr_rm_110.bmm",
            "openehr_rm_data_types_110.bmm",
            "openehr_rm_demographic_110.bmm",
            "openehr_rm_ehr_110.bmm",
            "openehr_rm_ehr_extract_110.bmm",
            "openehr_rm_structures_110.bmm",
        ];
        Assert.Equal(expected.Length, OpenEhrRmBmm.EmbeddedFileNames.Count);
        Assert.Equal(
            expected.OrderBy(s => s, StringComparer.Ordinal),
            OpenEhrRmBmm.EmbeddedFileNames.OrderBy(s => s, StringComparer.Ordinal));
    }
}
