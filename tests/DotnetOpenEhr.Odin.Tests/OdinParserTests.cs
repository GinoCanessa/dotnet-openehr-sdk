using System.Globalization;
using DotnetOpenEhr.Foundation;
using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Odin;
using DotnetOpenEhr.Odin.Values;
using Xunit;

namespace DotnetOpenEhr.Odin.Tests;

/// <summary>
/// Parser and round-trip tests covering the ODIN literal grammar, the
/// container forms (hash / list / object), interval syntaxes, and a
/// curated selection of examples lifted from the ODIN specification.
/// </summary>
public class OdinParserTests
{
    private static OdinValue RoundTrip(string source)
    {
        OdinValue parsed = OdinParser.Parse(source);
        string rendered = OdinWriter.Write(parsed);
        OdinValue reparsed = OdinParser.Parse(rendered);
        Assert.True(
            OdinValue.StructurallyEqual(parsed, reparsed),
            $"Round-trip mismatch.\n--- original ---\n{source}\n--- rendered ---\n{rendered}");
        return parsed;
    }

    [Fact]
    public void Parses_integer_leaf()
    {
        OdinValue v = OdinParser.Parse("<42>");
        Assert.Equal(42L, v.AsInteger().Value);
        RoundTrip("<42>");
    }

    [Fact]
    public void Parses_negative_integer_leaf()
    {
        OdinValue v = OdinParser.Parse("<-7>");
        Assert.Equal(-7L, v.AsInteger().Value);
    }

    [Fact]
    public void Parses_integer_with_exponent()
    {
        OdinValue v = OdinParser.Parse("<29e6>");
        Assert.Equal(29_000_000L, v.AsInteger().Value);
    }

    [Fact]
    public void Parses_real_leaf()
    {
        OdinValue v = OdinParser.Parse("<3.14>");
        Assert.Equal(3.14, v.AsReal().Value, 10);
        RoundTrip("<3.14>");
    }

    [Fact]
    public void Parses_string_leaf()
    {
        OdinValue v = OdinParser.Parse("<\"hello\">");
        Assert.Equal("hello", v.AsString().Value);
        RoundTrip("<\"hello\">");
    }

    [Fact]
    public void Parses_boolean_leaf()
    {
        OdinValue t = OdinParser.Parse("<True>");
        Assert.True(t.AsBoolean().Value);
        OdinValue f = OdinParser.Parse("<false>");
        Assert.False(f.AsBoolean().Value);
        RoundTrip("<True>");
    }

    [Fact]
    public void Parses_date_leaf()
    {
        OdinValue v = OdinParser.Parse("<1919-01-23>");
        Assert.Equal(new IsoDate(1919, 1, 23), v.AsDate().Value);
        RoundTrip("<1919-01-23>");
    }

    [Fact]
    public void Parses_partial_date_falls_back_to_string()
    {
        // SPEC: ODIN reduced-accuracy '??' partial dates aren't IsoDate.
        OdinValue v = OdinParser.Parse("<2024-??-??>");
        Assert.Equal("2024-??-??", v.AsString().Value);
    }

    [Fact]
    public void Parses_time_leaf()
    {
        OdinValue v = OdinParser.Parse("<08:30:00>");
        Assert.Equal("08:30:00", v.AsTime().Value.OriginalLexicalForm);
        RoundTrip("<08:30:00>");
    }

    [Fact]
    public void Parses_datetime_leaf()
    {
        OdinValue v = OdinParser.Parse("<2001-05-12T07:35:20+10:00>");
        Assert.Equal("2001-05-12T07:35:20+10:00", v.AsDateTime().Value.OriginalLexicalForm);
        RoundTrip("<2001-05-12T07:35:20+10:00>");
    }

    [Fact]
    public void Parses_duration_leaf()
    {
        OdinValue v = OdinParser.Parse("<P22DT4H15M0S>");
        Assert.Equal("P22DT4H15M0S", v.AsDuration().Value.OriginalLexicalForm);
        RoundTrip("<P22DT4H15M0S>");
    }

    [Fact]
    public void Parses_void_object()
    {
        OdinValue v = OdinParser.Parse("<...>");
        Assert.True(v.IsNull);
        // The default writer emits '<>' for null/empty; both are accepted.
        OdinValue empty = OdinParser.Parse("<>");
        Assert.True(empty.IsObject);
    }

    [Fact]
    public void Parses_terminology_code_full_form()
    {
        OdinValue v = OdinParser.Parse("<[ISO_639-1::en]>");
        OdinTerminologyCode tc = v.AsTerminologyCode();
        Assert.Equal("ISO_639-1", tc.Value.TerminologyId);
        Assert.Equal("en", tc.Value.CodeString);
        RoundTrip("<[ISO_639-1::en]>");
    }

    [Fact]
    public void Parses_terminology_code_local_form()
    {
        OdinValue v = OdinParser.Parse("<[at0001]>");
        OdinTerminologyCode tc = v.AsTerminologyCode();
        Assert.Equal("local", tc.Value.TerminologyId);
        Assert.Equal("at0001", tc.Value.CodeString);
        Assert.True(tc.IsLocalForm);
        RoundTrip("<[at0001]>");
    }

    [Fact]
    public void Parses_inline_list_of_strings()
    {
        OdinValue v = OdinParser.Parse("<\"pear\", \"cumquat\", \"peach\">");
        OdinList list = v.AsList();
        Assert.Equal(3, list.Items.Count);
        Assert.Equal("pear", list.Items[0].AsString().Value);
        Assert.Equal("peach", list.Items[2].AsString().Value);
        RoundTrip("<\"pear\", \"cumquat\", \"peach\">");
    }

    [Fact]
    public void Parses_inline_list_of_integers()
    {
        OdinValue v = OdinParser.Parse("<1, 1, 2, 3, 5>");
        OdinList list = v.AsList();
        Assert.Equal(5, list.Items.Count);
        Assert.Equal(2L, list.Items[2].AsInteger().Value);
    }

    [Fact]
    public void Parses_list_continuation_marker()
    {
        OdinValue v = OdinParser.Parse("<\"en\", ...>");
        OdinList list = v.AsList();
        Assert.Single(list.Items);
        Assert.True(list.HasContinuationMarker);
        RoundTrip("<\"en\", ...>");
    }

    [Fact]
    public void Parses_attribute_object()
    {
        const string src = "<name = <\"plato\"> age = <50>>";
        OdinValue v = OdinParser.Parse(src);
        OdinObject obj = v.AsObject();
        Assert.Equal(2, obj.Attributes.Count);
        Assert.Equal("plato", obj.Attributes["name"].AsString().Value);
        Assert.Equal(50L, obj.Attributes["age"].AsInteger().Value);
        RoundTrip(src);
    }

    [Fact]
    public void Parses_implicit_object_document()
    {
        const string src = "language = <[ISO_639-1::en]> author = <\"plato\">";
        OdinObject obj = OdinParser.Parse(src).AsObject();
        Assert.Equal(2, obj.Attributes.Count);
        Assert.Equal("en", obj.Attributes["language"].AsTerminologyCode().Value.CodeString);
        Assert.Equal("plato", obj.Attributes["author"].AsString().Value);
        RoundTrip(src);
    }

    [Fact]
    public void Parses_identified_object_document_top_level()
    {
        const string src = "[\"en\"] = <name = <\"english\">> [\"de\"] = <name = <\"german\">>";
        OdinHash hash = OdinParser.Parse(src).AsHash();
        Assert.Equal(2, hash.Entries.Count);
        Assert.Equal("english", hash.Entries["en"].AsObject().Attributes["name"].AsString().Value);
        Assert.Equal("german", hash.Entries["de"].AsObject().Attributes["name"].AsString().Value);
        RoundTrip(src);
    }

    [Fact]
    public void Parses_hash_with_integer_keys()
    {
        const string src = "locations = <[1] = <\"north\"> [2] = <\"south\">>";
        OdinObject obj = OdinParser.Parse(src).AsObject();
        OdinHash hash = obj.Attributes["locations"].AsHash();
        Assert.Equal(OdinKind.Integer, hash.KeyKind);
        Assert.Equal("north", hash.Entries["1"].AsString().Value);
        RoundTrip(src);
    }

    [Fact]
    public void Parses_type_marker_on_object()
    {
        const string src = "destinations = <[\"seville\"] = (TOURIST_DESTINATION) <profile = <\"good\">>>";
        OdinObject obj = OdinParser.Parse(src).AsObject();
        OdinHash hash = obj.Attributes["destinations"].AsHash();
        OdinObject seville = hash.Entries["seville"].AsObject();
        Assert.Equal("TOURIST_DESTINATION", seville.TypeMarker);
        RoundTrip(src);
    }

    [Fact]
    public void Parses_generic_type_marker()
    {
        const string src = "lesson_times = (List<Time>) <08:30:00, 09:30:00>";
        OdinObject obj = OdinParser.Parse(src).AsObject();
        OdinList list = obj.Attributes["lesson_times"].AsList();
        Assert.Equal("List<Time>", list.TypeMarker);
        Assert.Equal(2, list.Items.Count);
        RoundTrip(src);
    }

    [Fact]
    public void Parses_nested_containers()
    {
        const string src = "lol = <[1] = <[1] = <\"a\"> [2] = <\"b\">> [2] = <[1] = <\"c\">>>";
        OdinObject obj = OdinParser.Parse(src).AsObject();
        OdinHash outer = obj.Attributes["lol"].AsHash();
        Assert.Equal(2, outer.Entries.Count);
        OdinHash inner = outer.Entries["1"].AsHash();
        Assert.Equal("a", inner.Entries["1"].AsString().Value);
        Assert.Equal("b", inner.Entries["2"].AsString().Value);
        RoundTrip(src);
    }

    [Theory]
    [InlineData("<|0..5|>", true, 0L, true, 5L)]
    [InlineData("<|>0..5|>", false, 0L, true, 5L)]
    [InlineData("<|0..<5|>", true, 0L, false, 5L)]
    [InlineData("<|>0..<5|>", false, 0L, false, 5L)]
    public void Parses_two_sided_integer_intervals(string src, bool loIncl, long lo, bool hiIncl, long hi)
    {
        OdinValue v = OdinParser.Parse(src);
        OdinInterval iv = v.AsInterval();
        Assert.Equal(lo, iv.Lower!.AsInteger().Value);
        Assert.Equal(hi, iv.Upper!.AsInteger().Value);
        Assert.Equal(loIncl, iv.LowerIncluded);
        Assert.Equal(hiIncl, iv.UpperIncluded);
        RoundTrip(src);
    }

    [Theory]
    [InlineData("<|>=3|>", true, false, true)]
    [InlineData("<|>3|>", true, false, false)]
    [InlineData("<|<=3|>", false, true, true)]
    [InlineData("<|<3|>", false, true, false)]
    public void Parses_one_sided_intervals(string src, bool hasLower, bool hasUpper, bool included)
    {
        OdinValue v = OdinParser.Parse(src);
        OdinInterval iv = v.AsInterval();
        Assert.Equal(hasLower, iv.Lower is not null);
        Assert.Equal(hasUpper, iv.Upper is not null);
        if (hasLower) Assert.Equal(included, iv.LowerIncluded);
        if (hasUpper) Assert.Equal(included, iv.UpperIncluded);
        RoundTrip(src);
    }

    [Fact]
    public void Parses_plus_minus_interval()
    {
        OdinValue v = OdinParser.Parse("<|5.0 +/-0.5|>");
        OdinInterval iv = v.AsInterval();
        Assert.Equal(4.5, iv.Lower!.AsReal().Value, 10);
        Assert.Equal(5.5, iv.Upper!.AsReal().Value, 10);
    }

    [Fact]
    public void Parses_real_interval()
    {
        const string src = "<|0.0..<1000.0|>";
        OdinValue v = OdinParser.Parse(src);
        OdinInterval iv = v.AsInterval();
        Assert.Equal(0.0, iv.Lower!.AsReal().Value, 10);
        Assert.Equal(1000.0, iv.Upper!.AsReal().Value, 10);
        Assert.False(iv.UpperIncluded);
        RoundTrip(src);
    }

    [Fact]
    public void Parses_date_interval()
    {
        const string src = "<|>=1939-02-01|>";
        OdinValue v = OdinParser.Parse(src);
        OdinInterval iv = v.AsInterval();
        Assert.NotNull(iv.Lower);
        Assert.True(iv.LowerIncluded);
        Assert.Null(iv.Upper);
        RoundTrip(src);
    }

    // --- Examples lifted verbatim from the ODIN spec --------------------
    //
    // These exercise the most-used productions: void, leaf primitives,
    // hash containers, type markers, nested containers, lists, intervals,
    // and ADL2-style local terminology codes.

    public static IEnumerable<object[]> SpecExamples()
    {
        yield return new object[]
        {
            // spec 5.1 - simple attribute object
            "attr_1 = < attr_2 = <\"leaf\"> attr_3 = <42>>",
        };
        yield return new object[]
        {
            // spec 5.4 - list of strings
            "fruits = <\"pear\", \"cumquat\", \"peach\">",
        };
        yield return new object[]
        {
            // spec 5.4 - integer-keyed container
            "people = <[1] = <name = <\"alice\">> [2] = <name = <\"bob\">>>",
        };
        yield return new object[]
        {
            // spec 5.4 - string-keyed container with construction-style keys
            "subjects = <[\"philosophy:plato\"] = <teacher = <\"plato\">> [\"art\"] = <teacher = <\"goya\">>>",
        };
        yield return new object[]
        {
            // spec 5.5 - nested containers
            "list_of_string_lists = <[1] = <[1] = <\"first\">> [2] = <[1] = <\"second\"> [2] = <\"third\">>>",
        };
        yield return new object[]
        {
            // spec 5.6 - type-marked hash entries (ADL2-shaped)
            "destinations = <[\"seville\"] = (TOURIST_DESTINATION) <hotels = <[\"sofitel\"] = (LUXURY_HOTEL) <name = <\"sofitel\">>>>>",
        };
        yield return new object[]
        {
            // spec 7.2 - interval grab-bag
            "ranges = <[1] = <|0..5|> [2] = <|0.0..<1000.0|> [3] = <|>=1939-02-01|> [4] = <|5.0 +/-0.5|>>",
        };
        yield return new object[]
        {
            // ADL2 shape: local terminology codes inside a description block
            "description = (RESOURCE_DESCRIPTION) <details = <[\"en\"] = (RESOURCE_DESCRIPTION_ITEM) <language = <[ISO_639-1::en]>>>>",
        };
    }

    [Theory]
    [MemberData(nameof(SpecExamples))]
    public void Spec_example_round_trips(string source)
    {
        RoundTrip(source);
    }

    [Fact]
    public void Compact_write_round_trips()
    {
        const string src = "<name = <\"plato\"> age = <50>>";
        OdinValue parsed = OdinParser.Parse(src);
        string compact = OdinWriter.Write(parsed, OdinWriteOptions.Compact);
        OdinValue reparsed = OdinParser.Parse(compact);
        Assert.True(OdinValue.StructurallyEqual(parsed, reparsed));
        Assert.DoesNotContain("\n", compact, StringComparison.Ordinal);
    }

    [Fact]
    public void TryGet_on_object_and_hash()
    {
        OdinObject obj = (OdinObject)OdinParser.Parse("a = <\"x\"> b = <3>");
        Assert.True(obj.TryGet("a", out OdinValue? va));
        Assert.Equal("x", va!.AsString().Value);
        Assert.False(obj.TryGet("missing", out _));

        OdinHash hash = (OdinHash)OdinParser.Parse("[\"k\"] = <1>");
        Assert.True(hash.TryGet("k", out OdinValue? vk));
        Assert.Equal(1L, vk!.AsInteger().Value);
    }

    [Fact]
    public void Comments_in_source_are_ignored()
    {
        const string src = "-- top comment\na = <1>\n-- middle\nb = <2>";
        OdinObject obj = OdinParser.Parse(src).AsObject();
        Assert.Equal(2, obj.Attributes.Count);
        Assert.Equal(1L, obj.Attributes["a"].AsInteger().Value);
        Assert.Equal(2L, obj.Attributes["b"].AsInteger().Value);
    }

    [Fact]
    public void Semicolon_between_attributes_is_allowed()
    {
        const string src = "a = <1>; b = <2>";
        OdinObject obj = OdinParser.Parse(src).AsObject();
        Assert.Equal(2, obj.Attributes.Count);
    }

    [Fact]
    public void Manual_construction_writes_canonical_form()
    {
        OdinObject inner = new();
        inner.Attributes["k"] = new OdinInteger(7);
        OdinObject outer = new();
        outer.Attributes["thing"] = inner;
        string text = OdinWriter.Write(outer);
        OdinValue reparsed = OdinParser.Parse(text);
        Assert.True(OdinValue.StructurallyEqual(outer, reparsed));
        Assert.Contains("thing", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Real_with_invariant_culture()
    {
        // Sanity check: writer formats reals in invariant culture even
        // when the test process has a comma-decimal culture.
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            OdinObject obj = new();
            obj.Attributes["x"] = new OdinReal(3.14);
            string text = OdinWriter.Write(obj);
            Assert.Contains("3.14", text, StringComparison.Ordinal);
            Assert.DoesNotContain("3,14", text, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void StructurallyEqual_distinguishes_kinds()
    {
        OdinValue a = OdinParser.Parse("<3>");
        OdinValue b = OdinParser.Parse("<3.0>");
        Assert.False(OdinValue.StructurallyEqual(a, b));
    }

    [Fact]
    public void List_with_terminology_codes()
    {
        const string src = "<[at0001], [at0002], [at0003]>";
        OdinValue v = OdinParser.Parse(src);
        OdinList list = v.AsList();
        Assert.Equal(3, list.Items.Count);
        Assert.Equal("at0001", list.Items[0].AsTerminologyCode().Value.CodeString);
        RoundTrip(src);
    }

    [Fact]
    public void Hash_with_local_code_bracket_key()
    {
        // ADL2 sometimes uses bare [at0001] as a key (no '::') instead of
        // a string-quoted key. The parser accepts this and treats it as a
        // string-keyed hash with the code as the literal key text.
        const string src = "[at0001] = <\"first\"> [at0002] = <\"second\">";
        OdinHash hash = OdinParser.Parse(src).AsHash();
        // SPEC: keys may arrive as TerminologyCode-class identifiers; we
        // store the textual key. Either form is acceptable here.
        Assert.Equal(2, hash.Entries.Count);
    }
}
