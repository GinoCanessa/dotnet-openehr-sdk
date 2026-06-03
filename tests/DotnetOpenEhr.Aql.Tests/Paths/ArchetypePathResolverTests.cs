using System.Collections.Generic;
using DotnetOpenEhr.Aql;
using DotnetOpenEhr.Aql.Tests.Evaluation;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Support;
using Xunit;

namespace DotnetOpenEhr.Aql.Tests.Paths;

/// <summary>
/// Behavioral pins for <see cref="ArchetypePathResolver"/>. Covers the
/// FR's contract matrix: string / span overloads, path shapes,
/// not-found collapse, multi-match, generic overloads, root / empty
/// paths, and argument validation.
/// </summary>
public class ArchetypePathResolverTests
{
    private const string BpArchetype = "openEHR-EHR-OBSERVATION.blood_pressure.v2";
    private const string MagnitudePath = "/data/events/data/items[at0004]/value/magnitude";

    private static Observation NewBp(double systolic = 120, double diastolic = 80)
        => CompositionBuilder.NewBloodPressure(
            BpArchetype, systolic, "mm[Hg]", diastolic, "mm[Hg]");

    private static Observation NewTwoEventBp()
        => CompositionBuilder.NewBloodPressureWithTwoEvents(
            BpArchetype,
            firstSystolic: 120, firstDiastolic: 80,
            secondSystolic: 122, secondDiastolic: 82);

    private static Composition NewCompositionContainingBp()
    {
        Composition comp = CompositionBuilder.NewComposition(
            "BP Composition", "uid-bp",
            context: CompositionBuilder.NewContext(),
            content: [NewBp()]);
        return comp;
    }

    /// <summary>
    /// Build a single-event BP-shaped Observation whose Systolic
    /// Element carries an explicit openEHR null (NullFlavour set,
    /// Value == null) so the resolver tests can pin the null collapse.
    /// </summary>
    private static Observation NewBpWithOpenEhrNullSystolic()
    {
        Observation bp = NewBp();
        ItemTree tree = (ItemTree)((PointEvent)bp.Data!.Events![0]).Data!;
        Element systolic = (Element)tree.Items![0];
        systolic.Value = null;
        systolic.NullFlavour = new DvCodedText(
            "no information",
            new CodePhrase(new TerminologyId { Value = "openehr" }, "271"));
        return bp;
    }

    // ----------------------------------------------------------------
    // String / span entry points.
    // ----------------------------------------------------------------

    [Fact]
    public void Resolve_with_string_overload_matches_span_overload()
    {
        Observation bp = NewBp();
        object? viaString = ArchetypePathResolver.Resolve(bp, "/data/origin/value");
        object? viaSpan = ArchetypePathResolver.Resolve(bp, "/data/origin/value".AsSpan());
        Assert.Equal(viaString, viaSpan);
    }

    [Fact]
    public void Resolve_leading_slash_is_inert()
    {
        Observation bp = NewBp();
        object? leading = ArchetypePathResolver.Resolve(bp, "/data/origin/value");
        object? noLeading = ArchetypePathResolver.Resolve(bp, "data/origin/value");
        Assert.Equal(leading, noLeading);
    }

    // ----------------------------------------------------------------
    // Path shapes.
    // ----------------------------------------------------------------

    [Fact]
    public void Resolve_simple_attribute_chain_returns_terminal_value()
    {
        Observation bp = NewBp();
        object? value = ArchetypePathResolver.Resolve(bp, "/data/origin/value");
        Assert.NotNull(value);
    }

    [Fact]
    public void Resolve_node_id_predicate_disambiguates_sibling()
    {
        Observation bp = NewBp();
        object? value = ArchetypePathResolver.Resolve(
            bp,
            "/data[at0001]/events[at0006]/data[at0003]/items[at0004]/value/magnitude");
        Assert.Equal(120d, value);
    }

    [Fact]
    public void Resolve_archetype_hrid_predicate_matches_root()
    {
        Composition comp = NewCompositionContainingBp();
        object? value = ArchetypePathResolver.Resolve(
            comp,
            "/content[openEHR-EHR-OBSERVATION.blood_pressure.v2]");
        Assert.IsType<Observation>(value);
    }

    [Fact]
    public void Resolve_name_predicate_picks_systolic_over_diastolic()
    {
        Observation bp = NewBp();
        object? value = ArchetypePathResolver.Resolve(
            bp,
            "/data/events/data/items['Systolic']/value/magnitude");
        Assert.Equal(120d, value);
    }

    [Fact]
    public void Resolve_combined_node_id_and_name_predicate()
    {
        Observation bp = NewBp();
        object? match = ArchetypePathResolver.Resolve(
            bp,
            "/data/events/data/items[at0004, 'Systolic']/value/magnitude");
        Assert.Equal(120d, match);

        object? mismatch = ArchetypePathResolver.Resolve(
            bp,
            "/data/events/data/items[at0004, 'Diastolic']/value/magnitude");
        Assert.Null(mismatch);
    }

    // ----------------------------------------------------------------
    // Not-found cases (collapse to null / empty).
    // ----------------------------------------------------------------

    [Fact]
    public void Resolve_returns_null_when_intermediate_step_is_null()
    {
        Observation bp = NewBp();
        // BP fixture has no Protocol set.
        object? value = ArchetypePathResolver.Resolve(bp, "/protocol/items");
        Assert.Null(value);
    }

    [Fact]
    public void Resolve_returns_null_when_terminal_step_is_missing()
    {
        Observation bp = NewBp();
        object? value = ArchetypePathResolver.Resolve(bp, "/data/origin/no_such_attribute");
        Assert.Null(value);
    }

    [Fact]
    public void Resolve_returns_null_when_terminal_value_is_openehr_null()
    {
        Observation bp = NewBpWithOpenEhrNullSystolic();
        object? value = ArchetypePathResolver.Resolve(
            bp,
            "/data/events/data/items[at0004]/value/magnitude");
        Assert.Null(value);
    }

    [Fact]
    public void ResolveAll_returns_empty_enumerable_when_path_does_not_resolve()
    {
        Observation bp = NewBp();
        List<object?> items =
        [
            .. ArchetypePathResolver.ResolveAll(bp, "/protocol/items"),
        ];
        Assert.Empty(items);
    }

    [Fact]
    public void ResolveAll_returns_empty_enumerable_when_terminal_value_is_openehr_null()
    {
        Observation bp = NewBpWithOpenEhrNullSystolic();
        List<object?> items =
        [
            .. ArchetypePathResolver.ResolveAll(
                bp,
                "/data/events/data/items[at0004]/value/magnitude"),
        ];
        Assert.Empty(items);
    }

    [Fact]
    public void ResolveT_returns_default_when_terminal_value_is_openehr_null()
    {
        Observation bp = NewBpWithOpenEhrNullSystolic();
        double magnitude = ArchetypePathResolver.Resolve<double>(
            bp,
            "/data/events/data/items[at0004]/value/magnitude");
        Assert.Equal(0d, magnitude);
    }

    // ----------------------------------------------------------------
    // Multi-match contract.
    // ----------------------------------------------------------------

    [Fact]
    public void Resolve_throws_invalidoperation_when_path_matches_two_nodes()
    {
        Observation bp = NewTwoEventBp();
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ArchetypePathResolver.Resolve(bp, MagnitudePath));
        Assert.Contains("2", ex.Message);
    }

    [Fact]
    public void ResolveAll_returns_two_results_in_RM_collection_order()
    {
        Observation bp = NewTwoEventBp();
        List<object?> magnitudes =
        [
            .. ArchetypePathResolver.ResolveAll(bp, MagnitudePath),
        ];
        Assert.Equal(2, magnitudes.Count);
        Assert.Equal(120d, magnitudes[0]);
        Assert.Equal(122d, magnitudes[1]);
    }

    [Fact]
    public void ResolveAll_against_typed_rm_list_terminal_yields_each_element()
    {
        Observation bp = NewTwoEventBp();
        List<object?> events =
        [
            .. ArchetypePathResolver.ResolveAll(bp, "/data/events"),
        ];
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.IsType<PointEvent>(e));
    }

    // ----------------------------------------------------------------
    // Generic overloads.
    // ----------------------------------------------------------------

    [Fact]
    public void ResolveT_returns_typed_value()
    {
        Observation bp = NewBp();
        double magnitude = ArchetypePathResolver.Resolve<double>(bp, MagnitudePath);
        Assert.Equal(120d, magnitude);
    }

    [Fact]
    public void ResolveT_returns_default_when_path_does_not_resolve()
    {
        Observation bp = NewBp();
        double doubleDefault = ArchetypePathResolver.Resolve<double>(bp, "/protocol/items");
        Assert.Equal(0d, doubleDefault);
        string? stringDefault = ArchetypePathResolver.Resolve<string>(bp, "/protocol/items");
        Assert.Null(stringDefault);
    }

    [Fact]
    public void ResolveT_throws_invalidcast_on_type_mismatch()
    {
        Observation bp = NewBp();
        // /data/events/data/items[at0004]/value lands on a DvQuantity;
        // casting a DvQuantity reference to int boxes-then-fails.
        Assert.Throws<InvalidCastException>(
            () => ArchetypePathResolver.Resolve<int>(
                bp,
                "/data/events/data/items[at0004]/value"));
    }

    [Fact]
    public void ResolveAllT_throws_invalidcast_on_first_offending_element()
    {
        Observation bp = NewTwoEventBp();
        IEnumerator<int> enumerator =
            ArchetypePathResolver.ResolveAll<int>(
                bp,
                "/data/events/data/items[at0004]/value").GetEnumerator();
        Assert.Throws<InvalidCastException>(() => enumerator.MoveNext());
    }

    // ----------------------------------------------------------------
    // Empty / root path.
    // ----------------------------------------------------------------

    [Fact]
    public void Resolve_slash_only_returns_root()
    {
        Observation bp = NewBp();
        object? value = ArchetypePathResolver.Resolve(bp, "/");
        Assert.Same(bp, value);
    }

    [Fact]
    public void ResolveAll_slash_only_yields_root_once()
    {
        Observation bp = NewBp();
        List<object?> items =
        [
            .. ArchetypePathResolver.ResolveAll(bp, "/"),
        ];
        Assert.Single(items);
        Assert.Same(bp, items[0]);
    }

    [Fact]
    public void Resolve_empty_string_throws_archetypepathparse_exception()
    {
        Observation bp = NewBp();
        Assert.Throws<ArchetypePathParseException>(
            () => ArchetypePathResolver.Resolve(bp, string.Empty));
    }

    // ----------------------------------------------------------------
    // Argument validation.
    // ----------------------------------------------------------------

    [Fact]
    public void Resolve_throws_argumentnull_for_null_root()
    {
        Assert.Throws<ArgumentNullException>(
            () => ArchetypePathResolver.Resolve(null!, "/data/origin/value"));
    }

    [Fact]
    public void Resolve_throws_argumentnull_for_null_string_path()
    {
        Observation bp = NewBp();
        Assert.Throws<ArgumentNullException>(
            () => ArchetypePathResolver.Resolve(bp, (string)null!));
    }

    [Fact]
    public void Parse_throws_archetypepathparse_exception_on_invalid_input()
    {
        Assert.Throws<ArchetypePathParseException>(() => ArchetypePath.Parse("data//items"));
    }
}
