using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Support;
using DotnetOpenEhr.Serialization.Json;
using Xunit;

namespace DotnetOpenEhr.IntegrationTests;

/// <summary>
/// M25 — partial-precision DV_DATE / DV_TIME / DV_DATE_TIME values
/// must round-trip through canonical JSON without precision drift.
/// The IsoLexicalConverter preserves the original lexical form
/// rather than padding to a canonical (e.g.) full year-month-day.
/// </summary>
public sealed class PartialPrecisionTemporalTests
{
    [Theory]
    [InlineData("2024")]
    [InlineData("2024-05")]
    [InlineData("2024-05-27")]
    [InlineData("2020-02-29")]
    public void RoundTrip_DvDate_PreservesOriginalPrecision(string lexical)
    {
        DvDate original = new(IsoDate.Parse(lexical));
        Composition wrapper = BuildCompositionWithDvDateElement(original);

        byte[] bytes = OpenEhrJson.Serialize(wrapper);
        Composition? back = OpenEhrJson.ParseComposition(bytes);

        DvDate rt = ExtractFirstDvDate(back!);
        Assert.Equal(lexical, rt.Value.OriginalLexicalForm);
    }

    [Theory]
    [InlineData("2024-05-27T10")]
    [InlineData("2024-05-27T10:25:03Z")]
    [InlineData("2024-05-27T10:25:03+02:00")]
    [InlineData("2024-05-27T10:25:03")]
    public void RoundTrip_DvDateTime_PreservesOriginalLexicalForm(string lexical)
    {
        DvDateTime original = new(IsoDateTime.Parse(lexical));
        Composition wrapper = BuildCompositionWithDvDateTimeElement(original);

        byte[] bytes = OpenEhrJson.Serialize(wrapper);
        Composition? back = OpenEhrJson.ParseComposition(bytes);

        DvDateTime rt = ExtractFirstDvDateTime(back!);
        Assert.Equal(lexical, rt.Value.OriginalLexicalForm);
    }

    [Theory]
    [InlineData("10")]
    [InlineData("10:25")]
    [InlineData("10:25:03")]
    [InlineData("10:25:03Z")]
    public void RoundTrip_DvTime_PreservesOriginalPrecision(string lexical)
    {
        DvTime original = new(IsoTime.Parse(lexical));
        Composition wrapper = BuildCompositionWithDvTimeElement(original);

        byte[] bytes = OpenEhrJson.Serialize(wrapper);
        Composition? back = OpenEhrJson.ParseComposition(bytes);

        DvTime rt = ExtractFirstDvTime(back!);
        Assert.Equal(lexical, rt.Value.OriginalLexicalForm);
    }

    private static Composition BuildCompositionWithDvDateElement(DvDate value)
        => BuildCompositionWithElementValue(new Element
        {
            Name = new DvText("Date"),
            ArchetypeNodeId = "at0004",
            Value = value,
        });

    private static Composition BuildCompositionWithDvDateTimeElement(DvDateTime value)
        => BuildCompositionWithElementValue(new Element
        {
            Name = new DvText("DateTime"),
            ArchetypeNodeId = "at0004",
            Value = value,
        });

    private static Composition BuildCompositionWithDvTimeElement(DvTime value)
        => BuildCompositionWithElementValue(new Element
        {
            Name = new DvText("Time"),
            ArchetypeNodeId = "at0004",
            Value = value,
        });

    private static Composition BuildCompositionWithElementValue(Element el)
    {
        ItemTree tree = new()
        {
            Name = new DvText("data"),
            ArchetypeNodeId = "at0003",
            Items = [el],
        };
        PointEvent pt = new()
        {
            Name = new DvText("Any event"),
            ArchetypeNodeId = "at0006",
            Time = new DvDateTime(new IsoDateTime(new IsoDate(2024, 1, 1), new IsoTime(0, 0, 0))),
            Data = tree,
        };
        History history = new()
        {
            Name = new DvText("history"),
            ArchetypeNodeId = "at0002",
            Origin = new DvDateTime(new IsoDateTime(new IsoDate(2024, 1, 1), new IsoTime(0, 0, 0))),
            Events = [pt],
        };
        Observation obs = new()
        {
            Name = new DvText("obs"),
            ArchetypeNodeId = "openEHR-EHR-OBSERVATION.partial.v1",
            Language = new CodePhrase(new TerminologyId { Value = "ISO_639-1" }, "en"),
            Encoding = new CodePhrase(new TerminologyId { Value = "IANA_character-sets" }, "UTF-8"),
            Subject = new PartySelf(),
            Data = history,
        };
        return new Composition
        {
            Name = new DvText("comp"),
            ArchetypeNodeId = "openEHR-EHR-COMPOSITION.encounter.v1",
            Language = new CodePhrase(new TerminologyId { Value = "ISO_639-1" }, "en"),
            Territory = new CodePhrase(new TerminologyId { Value = "ISO_3166-1" }, "US"),
            Category = new DvCodedText("event",
                new CodePhrase(new TerminologyId { Value = "openehr" }, "433")),
            Composer = new PartyIdentified { Name = "Dr. Alice" },
            Content = [obs],
        };
    }

    private static T ExtractFirstElementValue<T>(Composition comp) where T : class
    {
        Observation obs = (Observation)comp.Content![0];
        History history = obs.Data;
        PointEvent ev = (PointEvent)history.Events![0];
        ItemTree tree = (ItemTree)ev.Data;
        Element el = (Element)tree.Items![0];
        return (T)(object)el.Value!;
    }

    private static DvDate ExtractFirstDvDate(Composition c) => ExtractFirstElementValue<DvDate>(c);
    private static DvDateTime ExtractFirstDvDateTime(Composition c) => ExtractFirstElementValue<DvDateTime>(c);
    private static DvTime ExtractFirstDvTime(Composition c) => ExtractFirstElementValue<DvTime>(c);
}
