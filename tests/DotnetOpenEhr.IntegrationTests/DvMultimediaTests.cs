using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Encapsulated;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.DataTypes.Uri;
using DotnetOpenEhr.Rm.Support;
using DotnetOpenEhr.Serialization.Json;
using Xunit;

namespace DotnetOpenEhr.IntegrationTests;

/// <summary>
/// Pins M5 — DvMultimedia.Size widened from <c>int</c> to <c>long</c>
/// so payloads above 2 GiB do not silently truncate on the wire.
/// </summary>
public sealed class DvMultimediaTests
{
    [Fact]
    public void Size_RoundTripsBeyondInt32_Max()
    {
        const long FiveGib = 5L * 1024 * 1024 * 1024;

        DvMultimedia source = new()
        {
            MediaType = new CodePhrase(new TerminologyId { Value = "IANA_media-types" }, "image/jpeg"),
            Size = FiveGib,
            Uri = new DvUri { Value = "http://example.org/large.jpg" },
        };

        Composition original = BuildCompositionWithElement(source);
        byte[] bytes = OpenEhrJson.Serialize(original);
        Composition? back = OpenEhrJson.ParseComposition(bytes);
        Assert.NotNull(back);

        DvMultimedia rt = Assert.IsType<DvMultimedia>(((Element)((ItemTree)((PointEvent)((Observation)back!.Content![0]).Data.Events![0]).Data).Items![0]).Value);
        Assert.Equal(FiveGib, rt.Size);
    }

    private static Composition BuildCompositionWithElement(DvMultimedia mm)
    {
        Element el = new()
        {
            Name = new DvText("Image"),
            ArchetypeNodeId = "at0004",
            Value = mm,
        };
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
            ArchetypeNodeId = "openEHR-EHR-OBSERVATION.image.v1",
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
}
