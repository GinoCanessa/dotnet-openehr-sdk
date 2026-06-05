using DotnetOpenEhr.Foundation;
using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Support;
using DotnetOpenEhr.Serialization.Json;
using Xunit;

namespace DotnetOpenEhr.IntegrationTests;

/// <summary>
/// Pins H2 — properties previously typed <c>Interval&lt;DvOrdered&gt;?</c>
/// must round-trip through canonical JSON when retyped to a concrete
/// <c>Interval&lt;TConcrete&gt;</c>. The closed generic carries no
/// polymorphism, so the symmetric <see cref="IntervalJsonConverter{T}"/>
/// handles serialization without an abstract-base discriminator.
/// </summary>
public sealed class IntervalPropertyTests
{
    [Fact]
    public void Constructed_InMemory_Participation_Time_IsBounded_DvDateTime_Interval()
    {
        DvDateTime lo = new(new IsoDateTime(new IsoDate(2024, 1, 1), new IsoTime(8, 0, 0)));
        DvDateTime hi = new(new IsoDateTime(new IsoDate(2024, 1, 1), new IsoTime(17, 0, 0)));
        Interval<DvDateTime> bounded = Interval<DvDateTime>.Bounded(lo, hi);

        Participation p = new()
        {
            Function = new DvText("attending"),
            Performer = new PartyIdentified { Name = "Dr. Alice Example" },
            Time = bounded,
        };

        Assert.NotNull(p.Time);
        Assert.True(p.Time!.LowerIncluded);
        Assert.True(p.Time.UpperIncluded);
        Assert.Equal("2024-01-01T08:00:00", p.Time.Lower.Value.OriginalLexicalForm);
        Assert.Equal("2024-01-01T17:00:00", p.Time.Upper.Value.OriginalLexicalForm);
    }

    [Fact]
    public void RoundTrip_CompositionWith_Participation_Time_BoundedInterval_PreservesBounds()
    {
        DvDateTime lo = new(new IsoDateTime(new IsoDate(2024, 1, 1), new IsoTime(8, 0, 0)));
        DvDateTime hi = new(new IsoDateTime(new IsoDate(2024, 1, 1), new IsoTime(17, 0, 0)));

        Composition original = BuildCompositionWithParticipationTime(Interval<DvDateTime>.Bounded(lo, hi));

        byte[] bytes = OpenEhrJson.Serialize(original);
        Composition? back = OpenEhrJson.ParseComposition(bytes);

        Assert.NotNull(back);
        Participation rt = Assert.Single(back!.Context!.Participations!);
        Assert.NotNull(rt.Time);
        Assert.True(rt.Time!.HasLower);
        Assert.True(rt.Time.HasUpper);
        Assert.True(rt.Time.LowerIncluded);
        Assert.True(rt.Time.UpperIncluded);
        Assert.Equal("2024-01-01T08:00:00", rt.Time.Lower.Value.OriginalLexicalForm);
        Assert.Equal("2024-01-01T17:00:00", rt.Time.Upper.Value.OriginalLexicalForm);
    }

    [Fact]
    public void RoundTrip_Participation_Time_OpenAndUnbounded_PreservesBoundsAndInclusion()
    {
        DvDateTime hi = new(new IsoDateTime(new IsoDate(2024, 6, 1), new IsoTime(0, 0, 0)));
        Interval<DvDateTime> upperOnlyExclusive = Interval<DvDateTime>.LessThan(hi);

        Composition original = BuildCompositionWithParticipationTime(upperOnlyExclusive);

        byte[] bytes = OpenEhrJson.Serialize(original);
        Composition? back = OpenEhrJson.ParseComposition(bytes);

        Assert.NotNull(back);
        Participation rt = Assert.Single(back!.Context!.Participations!);
        Assert.NotNull(rt.Time);
        Assert.False(rt.Time!.HasLower);
        Assert.True(rt.Time.HasUpper);
        Assert.False(rt.Time.UpperIncluded);
        Assert.Equal("2024-06-01T00:00:00", rt.Time.Upper.Value.OriginalLexicalForm);
    }

    [Fact]
    public void RoundTrip_Participation_Time_Unbounded_DoesNotEmitLowerOrUpper()
    {
        Composition original = BuildCompositionWithParticipationTime(Interval<DvDateTime>.Unbounded());

        byte[] bytes = OpenEhrJson.Serialize(original);
        string text = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.Contains("\"lower_unbounded\":true", text, StringComparison.Ordinal);
        Assert.Contains("\"upper_unbounded\":true", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"lower\":", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"upper\":", text, StringComparison.Ordinal);

        Composition? back = OpenEhrJson.ParseComposition(bytes);
        Participation rt = Assert.Single(back!.Context!.Participations!);
        Assert.NotNull(rt.Time);
        Assert.False(rt.Time!.HasLower);
        Assert.False(rt.Time.HasUpper);
    }

    private static Composition BuildCompositionWithParticipationTime(Interval<DvDateTime> time)
    {
        return new Composition
        {
            Name = new DvText("session"),
            ArchetypeNodeId = "openEHR-EHR-COMPOSITION.encounter.v1",
            Language = new CodePhrase(new TerminologyId { Value = "ISO_639-1" }, "en"),
            Territory = new CodePhrase(new TerminologyId { Value = "ISO_3166-1" }, "US"),
            Category = new DvCodedText("event",
                new CodePhrase(new TerminologyId { Value = "openehr" }, "433")),
            Composer = new PartyIdentified { Name = "Dr. Alice Example" },
            Context = new EventContext
            {
                StartTime = new DvDateTime(new IsoDateTime(new IsoDate(2024, 1, 1), new IsoTime(8, 0, 0))),
                Setting = new DvCodedText("primary",
                    new CodePhrase(new TerminologyId { Value = "openehr" }, "228")),
                Participations =
                [
                    new Participation
                    {
                        Function = new DvText("attending"),
                        Performer = new PartyIdentified { Name = "Dr. Alice" },
                        Time = time,
                    },
                ],
            },
        };
    }
}
