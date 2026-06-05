using System.Linq;
using DotnetOpenEhr.Archetypes.Adl2;
using DotnetOpenEhr.Archetypes.Aom2;
using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using Xunit;

namespace DotnetOpenEhr.Archetypes.Tests.Adl2;

/// <summary>
/// M16 (0604-04) — characterisation tests for DV_QUANTITY / DV_ORDINAL
/// constraints in <see cref="Adl2Parser"/>.
///
/// The openEHR ADL 2 spec (Release 2.3.0, § "Tuple constraint examples")
/// explicitly removed the legacy ADL 1.4 <c>C_DV_QUANTITY</c> and
/// <c>C_DV_ORDINAL</c> constraint forms. The replacement is generic
/// <c>CAttributeTuple</c> constraints: a co-varying <c>[units, magnitude]</c>
/// tuple for DV_QUANTITY and a <c>[value, symbol]</c> tuple for DV_ORDINAL,
/// which the existing <see cref="Adl2Parser"/> already materialises as a
/// <see cref="CComplexObject"/> with one or more
/// <see cref="CAttributeTuple"/> children.
///
/// Pin both:
///   - The basic (non-tuple) form: a CComplexObject with attribute children.
///   - The tuple form: a CComplexObject with AttributeTuples populated.
///   - Round-tripping both via <see cref="Adl2Writer"/>.
///
/// The AOM2 model classes <c>CDvQuantity</c> / <c>CDvOrdinal</c> remain in
/// the codebase as orphans — no ADL2 syntax produces them today. The
/// validator dispatches on them defensively but they will never appear in
/// a parsed archetype until/unless ADL2 reintroduces the second-order
/// syntax.
/// </summary>
public sealed class Adl2ParserSecondOrderTests
{
    private const string MinimalHeader = "archetype (adl_version=2.0.6; rm_release=1.1.0)\n\topenEHR-EHR-OBSERVATION.so.v1.0.0\n";
    private const string MinimalLanguage = "\nlanguage\n\toriginal_language = <[ISO_639-1::en]>\n";
    private const string MinimalDescription = "\ndescription\n\tlifecycle_state = <\"unmanaged\">\n";
    private const string MinimalTerminology = "\nterminology\n\tterm_definitions = <[\"en\"] = <[\"id1\"] = <text = <\"x\"> description = <\"y\">>>>\n";

    private static string Wrap(string definition)
        => MinimalHeader + MinimalLanguage + MinimalDescription + definition + MinimalTerminology;

    private static CObject FirstLeaf(Archetype a)
    {
        // Same shape as Adl2ParserTests.FirstLeaf: walk down attribute
        // chains until a leaf primitive-like object surfaces.
        CObject cur = a.Definition;
        while (true)
        {
            if (cur is CComplexObject ccx && ccx.Attributes.Count > 0
                && ccx.Attributes[0].Children.Count > 0)
            {
                cur = ccx.Attributes[0].Children[0];
                continue;
            }
            return cur;
        }
    }

    // ---- DV_QUANTITY — basic (non-tuple) ---------------------------------

    [Fact]
    public void DvQuantity_basic_form_parses_as_CComplexObject_with_magnitude_attribute()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tELEMENT[id1] matches {\n"
            + "\t\tvalue matches { DV_QUANTITY[id2] matches {\n"
            + "\t\t\tmagnitude matches { |0.0..300.0| }\n"
            + "\t\t\tunits matches { \"mm[Hg]\" }\n"
            + "\t\t} }\n"
            + "\t}\n"));

        // Walk: ELEMENT/value/DV_QUANTITY
        CComplexObject element = a.Definition;
        CObject valueObj = element.Attributes.Single(x => x.RmAttributeName == "value").Children.Single();
        CComplexObject dvQuantity = Assert.IsType<CComplexObject>(valueObj);
        Assert.Equal("DV_QUANTITY", dvQuantity.RmTypeName);
        Assert.Equal(2, dvQuantity.Attributes.Count);
        Assert.Contains(dvQuantity.Attributes, a => a.RmAttributeName == "magnitude");
        Assert.Contains(dvQuantity.Attributes, a => a.RmAttributeName == "units");
        Assert.Empty(dvQuantity.AttributeTuples);
    }

    // ---- DV_QUANTITY — tuple form (spec § 4.3.1) -------------------------

    [Fact]
    public void DvQuantity_tuple_form_parses_as_CComplexObject_with_AttributeTuples()
    {
        // Spec-correct ADL2 tuple form: co-varying [units, magnitude]
        // with two alternative rows. Each leaf is wrapped in { }.
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tELEMENT[id1] matches {\n"
            + "\t\tvalue matches { DV_QUANTITY[id2] matches {\n"
            + "\t\t\t[units, magnitude] matches {\n"
            + "\t\t\t\t[{\"deg F\"}, {|32.0..212.0|}],\n"
            + "\t\t\t\t[{\"deg C\"}, {|0.0..100.0|}]\n"
            + "\t\t\t}\n"
            + "\t\t} }\n"
            + "\t}\n"));

        CComplexObject element = a.Definition;
        CObject valueObj = element.Attributes.Single(x => x.RmAttributeName == "value").Children.Single();
        CComplexObject dvQuantity = Assert.IsType<CComplexObject>(valueObj);
        Assert.Equal("DV_QUANTITY", dvQuantity.RmTypeName);
        CAttributeTuple tuple = Assert.Single(dvQuantity.AttributeTuples);
        Assert.Equal(2, tuple.Members.Count);
        Assert.Equal("units", tuple.Members[0].RmAttributeName);
        Assert.Equal("magnitude", tuple.Members[1].RmAttributeName);
        Assert.Equal(2, tuple.Children.Count); // two alternative rows
    }

    // ---- DV_ORDINAL — basic and tuple forms ------------------------------

    [Fact]
    public void DvOrdinal_basic_form_parses_as_CComplexObject_with_value_and_symbol()
    {
        Archetype a = Adl2Parser.Parse(Wrap(
            "\ndefinition\n\tELEMENT[id1] matches {\n"
            + "\t\tvalue matches { DV_ORDINAL[id2] matches {\n"
            + "\t\t\tvalue matches { 0 }\n"
            + "\t\t\tsymbol matches { DV_CODED_TEXT[id3] matches { defining_code matches {[at1]} } }\n"
            + "\t\t} }\n"
            + "\t}\n"));

        CComplexObject element = a.Definition;
        CObject valueObj = element.Attributes.Single(x => x.RmAttributeName == "value").Children.Single();
        CComplexObject dvOrdinal = Assert.IsType<CComplexObject>(valueObj);
        Assert.Equal("DV_ORDINAL", dvOrdinal.RmTypeName);
        Assert.Contains(dvOrdinal.Attributes, a => a.RmAttributeName == "value");
        Assert.Contains(dvOrdinal.Attributes, a => a.RmAttributeName == "symbol");
        Assert.Empty(dvOrdinal.AttributeTuples);
    }

    [Fact]
    public void DvOrdinal_tuple_form_with_terminology_codes_is_a_known_parser_limitation()
    {
        // Spec § 4.3.1 tuple form for DV_ORDINAL pairs an integer value
        // with a symbol that is itself a terminology code:
        //     [value, symbol] matches {
        //         [{1}, {[at1]}],
        //         [{2}, {[at2]}]
        //     }
        //
        // The current Adl2Parser tuple-member parser
        // (ParsePrimitiveBody) handles intervals + string / integer /
        // real literal lists, but does not accept terminology codes
        // (AtCode / AcCode / LBracket) inside a tuple-member { }
        // block. Characterise this so a future change to
        // ParsePrimitiveBody surfaces here.
        string source = Wrap(
            "\ndefinition\n\tELEMENT[id1] matches {\n"
            + "\t\tvalue matches { DV_ORDINAL[id2] matches {\n"
            + "\t\t\t[value, symbol] matches {\n"
            + "\t\t\t\t[{1}, {[at1]}],\n"
            + "\t\t\t\t[{2}, {[at2]}]\n"
            + "\t\t\t}\n"
            + "\t\t} }\n"
            + "\t}\n");

        Adl2ParseException ex = Assert.Throws<Adl2ParseException>(
            () => Adl2Parser.Parse(source));
        Assert.Contains("Expected '}'", ex.Message, StringComparison.Ordinal);
    }

    // ---- Round-trip via Adl2Writer ---------------------------------------

    [Fact]
    public void DvQuantity_tuple_form_round_trips_through_writer()
    {
        string source = Wrap(
            "\ndefinition\n\tELEMENT[id1] matches {\n"
            + "\t\tvalue matches { DV_QUANTITY[id2] matches {\n"
            + "\t\t\t[units, magnitude] matches {\n"
            + "\t\t\t\t[{\"deg F\"}, {|32.0..212.0|}],\n"
            + "\t\t\t\t[{\"deg C\"}, {|0.0..100.0|}]\n"
            + "\t\t\t}\n"
            + "\t\t} }\n"
            + "\t}\n");

        Archetype first = Adl2Parser.Parse(source);
        string written = Adl2Writer.Write(first);
        Archetype second = Adl2Parser.Parse(written);

        CComplexObject element1 = first.Definition;
        CComplexObject element2 = second.Definition;
        CComplexObject dvq1 = (CComplexObject)element1.Attributes.Single(a => a.RmAttributeName == "value").Children.Single();
        CComplexObject dvq2 = (CComplexObject)element2.Attributes.Single(a => a.RmAttributeName == "value").Children.Single();
        Assert.Equal(dvq1.AttributeTuples.Count, dvq2.AttributeTuples.Count);
        Assert.Equal(dvq1.AttributeTuples[0].Members.Count, dvq2.AttributeTuples[0].Members.Count);
        Assert.Equal(dvq1.AttributeTuples[0].Children.Count, dvq2.AttributeTuples[0].Children.Count);
    }

    [Fact]
    public void DvOrdinal_basic_form_round_trips_through_writer()
    {
        // Use the basic (non-tuple) form for round-trip; the tuple
        // form's terminology-code members are a parser limitation
        // (see Adl2ParserSecondOrderTests above).
        string source = Wrap(
            "\ndefinition\n\tELEMENT[id1] matches {\n"
            + "\t\tvalue matches { DV_ORDINAL[id2] matches {\n"
            + "\t\t\tvalue matches { 0, 1, 2 }\n"
            + "\t\t} }\n"
            + "\t}\n");

        Archetype first = Adl2Parser.Parse(source);
        string written = Adl2Writer.Write(first);
        Archetype second = Adl2Parser.Parse(written);

        CComplexObject element1 = first.Definition;
        CComplexObject element2 = second.Definition;
        CComplexObject dvo1 = (CComplexObject)element1.Attributes.Single(a => a.RmAttributeName == "value").Children.Single();
        CComplexObject dvo2 = (CComplexObject)element2.Attributes.Single(a => a.RmAttributeName == "value").Children.Single();
        Assert.Equal("DV_ORDINAL", dvo1.RmTypeName);
        Assert.Equal(dvo1.RmTypeName, dvo2.RmTypeName);
        Assert.Equal(dvo1.Attributes.Count, dvo2.Attributes.Count);
    }
}
