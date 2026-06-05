using System.Text.Json;
using DotnetOpenEhr.Rm.DataTypes;
using DotnetOpenEhr.Rm.DataTypes.Basic;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Serialization.Json.Flat;
using Xunit;

namespace DotnetOpenEhr.Serialization.Json.Flat.Tests.Flat;

/// <summary>
/// M1 (0604-04): when two FLAT entries land on the same DV path and
/// their attribute hints disagree about the concrete DV type,
/// <see cref="FlatJsonContentParser.MergeDataValueInternal"/> must
/// preserve scalars written by the first entry rather than silently
/// zero them out when re-instantiating the target type. These tests
/// drive the merge directly via <c>InternalsVisibleTo</c>.
/// </summary>
public sealed class MergeDataValueScalarPreservationTests
{
    private static JsonElement Json(string raw)
    {
        using JsonDocument doc = JsonDocument.Parse(raw);
        return doc.RootElement.Clone();
    }

    /// <summary>Convenience: run two attribute merges in order and
    /// return the resulting DataValue.</summary>
    private static DataValue Merge2(
        string rmType1, string attr1, string raw1,
        string rmType2, string attr2, string raw2)
    {
        DataValue step1 = FlatJsonContentParser.MergeDataValueInternal(
            current: null,
            rmType: rmType1 == "" ? null : rmType1,
            attribute: attr1,
            value: Json(raw1),
            path: "test|" + attr1);
        DataValue step2 = FlatJsonContentParser.MergeDataValueInternal(
            current: step1,
            rmType: rmType2 == "" ? null : rmType2,
            attribute: attr2,
            value: Json(raw2),
            path: "test|" + attr2);
        return step2;
    }

    // ---- DvCount -> DvQuantity transition --------------------------------

    [Fact]
    public void DvCount_to_DvQuantity_when_units_arrives_after_magnitude_preserves_magnitude()
    {
        // |magnitude (integral 120) lands as DvCount; |units forces
        // re-instantiation as DvQuantity. Magnitude must survive.
        DataValue merged = Merge2(
            rmType1: "", attr1: "|magnitude", raw1: "120",
            rmType2: "", attr2: "|units",     raw2: "\"mm[Hg]\"");

        DvQuantity q = Assert.IsType<DvQuantity>(merged);
        Assert.Equal(120.0, q.Magnitude);
        Assert.Equal("mm[Hg]", q.Units);
    }

    [Fact]
    public void DvQuantity_magnitude_then_units_with_schema_hint_preserves_magnitude()
    {
        // When the schema hint pins DV_QUANTITY up-front, |magnitude
        // (integral) lands directly as DvQuantity; |units does not
        // trigger a transition, but magnitude must still survive.
        DataValue merged = Merge2(
            rmType1: "DV_QUANTITY", attr1: "|magnitude", raw1: "120",
            rmType2: "DV_QUANTITY", attr2: "|units",     raw2: "\"mm[Hg]\"");

        DvQuantity q = Assert.IsType<DvQuantity>(merged);
        Assert.Equal(120.0, q.Magnitude);
        Assert.Equal("mm[Hg]", q.Units);
    }

    // ---- DvText -> DvCodedText transition --------------------------------

    [Fact]
    public void DvText_to_DvCodedText_when_code_arrives_after_value_preserves_text()
    {
        DataValue merged = Merge2(
            rmType1: "", attr1: "|value", raw1: "\"home\"",
            rmType2: "", attr2: "|code",  raw2: "\"225\"");

        DvCodedText ct = Assert.IsType<DvCodedText>(merged);
        Assert.Equal("home", ct.Value);
        Assert.Equal("225", ct.DefiningCode.CodeString);
    }

    [Fact]
    public void DvText_to_DvCodedText_followed_by_terminology_preserves_text()
    {
        // Three-step: |value -> |code -> |terminology. Final shape is
        // DvCodedText with all three populated.
        DataValue step1 = FlatJsonContentParser.MergeDataValueInternal(
            null, null, "|value", Json("\"home\""), "test|value");
        DataValue step2 = FlatJsonContentParser.MergeDataValueInternal(
            step1, null, "|code", Json("\"225\""), "test|code");
        DataValue step3 = FlatJsonContentParser.MergeDataValueInternal(
            step2, null, "|terminology", Json("\"openehr\""), "test|terminology");

        DvCodedText ct = Assert.IsType<DvCodedText>(step3);
        Assert.Equal("home", ct.Value);
        Assert.Equal("225", ct.DefiningCode.CodeString);
        Assert.Equal("openehr", ct.DefiningCode.TerminologyId.Value);
    }

    // ---- DvCodedText not downcast to DvText -----------------------------

    [Fact]
    public void DvCodedText_is_not_downcast_when_value_arrives_after_code_pair()
    {
        // |code+|terminology first, then |value. The schema hint here
        // is DV_TEXT to maximise the downcast pressure. The merger
        // must keep the DvCodedText and update Value in place.
        DataValue step1 = FlatJsonContentParser.MergeDataValueInternal(
            null, null, "|code", Json("\"225\""), "test|code");
        DataValue step2 = FlatJsonContentParser.MergeDataValueInternal(
            step1, null, "|terminology", Json("\"openehr\""), "test|terminology");
        DataValue step3 = FlatJsonContentParser.MergeDataValueInternal(
            step2, "DV_TEXT", "|value", Json("\"home\""), "test|value");

        DvCodedText ct = Assert.IsType<DvCodedText>(step3);
        Assert.Equal("home", ct.Value);
        Assert.Equal("225", ct.DefiningCode.CodeString);
        Assert.Equal("openehr", ct.DefiningCode.TerminologyId.Value);
    }

    [Fact]
    public void DvCodedText_does_not_leave_DvText_only_instance_after_value_update()
    {
        // Negative pin: after the above sequence, the result must not
        // be a bare DvText.
        DataValue step1 = FlatJsonContentParser.MergeDataValueInternal(
            null, null, "|code", Json("\"225\""), "test|code");
        DataValue step2 = FlatJsonContentParser.MergeDataValueInternal(
            step1, "DV_TEXT", "|value", Json("\"home\""), "test|value");

        // It is a DvText (DvCodedText extends DvText) but it must be
        // specifically a DvCodedText - never re-instantiated as a
        // plain DvText (which would lose the code).
        Assert.IsType<DvCodedText>(step2);
        Assert.False(step2.GetType() == typeof(DvText),
            "DvCodedText must not be downcast to plain DvText.");
    }

    // ---- DvText -> DvBoolean transition ----------------------------------

    [Fact]
    public void DvText_to_DvBoolean_when_value_arrives_as_boolean_does_not_throw_and_drops_text()
    {
        // First |value is a string -> DvText. Second |value is a JSON
        // boolean -> InferRmTypeFromAttribute returns DV_BOOLEAN. The
        // transition has no shared scalars; the prior text is
        // explicitly destroyed. Must not throw.
        DataValue merged = Merge2(
            rmType1: "", attr1: "|value", raw1: "\"home\"",
            rmType2: "", attr2: "|value", raw2: "true");

        DvBoolean b = Assert.IsType<DvBoolean>(merged);
        Assert.True(b.Value);
    }

    // ---- Single-merge attribute arms (sanity) ---------------------------

    [Fact]
    public void Single_magnitude_integral_lands_as_DvCount_schemaless()
    {
        DataValue merged = FlatJsonContentParser.MergeDataValueInternal(
            null, null, "|magnitude", Json("42"), "test|magnitude");
        DvCount c = Assert.IsType<DvCount>(merged);
        Assert.Equal(42L, c.Magnitude);
    }

    [Fact]
    public void Single_magnitude_floating_lands_as_DvQuantity_schemaless()
    {
        DataValue merged = FlatJsonContentParser.MergeDataValueInternal(
            null, null, "|magnitude", Json("42.5"), "test|magnitude");
        DvQuantity q = Assert.IsType<DvQuantity>(merged);
        Assert.Equal(42.5, q.Magnitude);
    }
}
