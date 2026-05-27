using System.Text;
using System.Text.Json;
using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Support;
using Xunit;

namespace DotnetOpenEhr.Serialization.Json.Tests;

/// <summary>
/// Programmatically builds a small in-memory Composition graph and
/// asserts the canonical JSON serializer preserves polymorphism and
/// property naming through a full round-trip.
/// </summary>
public sealed class PolymorphismRoundTripTests
{
    private static Composition BuildSample()
    {
        DvQuantity sbp = new(120, "mm[Hg]");
        Element systolic = new()
        {
            Name = new DvText("Systolic"),
            ArchetypeNodeId = "at0004",
            Value = sbp,
        };
        Element temperatureNote = new()
        {
            Name = new DvText("Note"),
            ArchetypeNodeId = "at0005",
            Value = new DvCodedText("febrile",
                new CodePhrase(new TerminologyId { Value = "SNOMED-CT" }, "386661006")),
        };
        Element observedAt = new()
        {
            Name = new DvText("Observed at"),
            ArchetypeNodeId = "at0007",
            Value = new DvDateTime(IsoDateTime.Parse("2024-05-27T10:25:03")),
        };

        ItemTree tree = new()
        {
            Name = new DvText("blood_pressure_data"),
            ArchetypeNodeId = "at0003",
            Items = [systolic, temperatureNote, observedAt],
        };
        PointEvent pt = new()
        {
            Name = new DvText("Any event"),
            ArchetypeNodeId = "at0006",
            Time = new DvDateTime(IsoDateTime.Parse("2024-05-27T10:25:03")),
            Data = tree,
        };
        History history = new()
        {
            Name = new DvText("history"),
            ArchetypeNodeId = "at0002",
            Origin = new DvDateTime(IsoDateTime.Parse("2024-05-27T10:25:03")),
            Events = [pt],
        };
        Observation obs = new()
        {
            Name = new DvText("Blood pressure"),
            ArchetypeNodeId = "openEHR-EHR-OBSERVATION.blood_pressure.v2",
            Language = new CodePhrase(new TerminologyId { Value = "ISO_639-1" }, "en"),
            Encoding = new CodePhrase(new TerminologyId { Value = "IANA_character-sets" }, "UTF-8"),
            Subject = new PartySelf(),
            Data = history,
        };
        Composition comp = new()
        {
            Name = new DvText("Vitals"),
            ArchetypeNodeId = "openEHR-EHR-COMPOSITION.encounter.v1",
            Language = new CodePhrase(new TerminologyId { Value = "ISO_639-1" }, "en"),
            Territory = new CodePhrase(new TerminologyId { Value = "ISO_3166-1" }, "US"),
            Category = new DvCodedText("event",
                new CodePhrase(new TerminologyId { Value = "openehr" }, "433")),
            Composer = new PartyIdentified { Name = "Dr. Alice Example" },
            Content = [obs],
        };
        return comp;
    }

    [Fact]
    public void Composition_RoundTrips_AndCarriesConcreteRmTypes()
    {
        Composition original = BuildSample();
        byte[] bytes = OpenEhrJson.Serialize(original);
        Composition? back = OpenEhrJson.ParseComposition(bytes);

        Assert.NotNull(back);
        Assert.Equal(original.ArchetypeNodeId, back!.ArchetypeNodeId);
        Assert.NotNull(back.Content);
        Assert.Single(back.Content!);

        Observation obs = Assert.IsType<Observation>(back.Content![0]);
        Assert.IsType<History>(obs.Data);
        PointEvent pt = Assert.IsType<PointEvent>(obs.Data.Events![0]);
        ItemTree tree = Assert.IsType<ItemTree>(pt.Data);
        Element systolic = Assert.IsType<Element>(tree.Items![0]);
        DvQuantity q = Assert.IsType<DvQuantity>(systolic.Value);
        Assert.Equal(120d, q.Magnitude);
        Assert.Equal("mm[Hg]", q.Units);

        Element coded = Assert.IsType<Element>(tree.Items![1]);
        DvCodedText ct = Assert.IsType<DvCodedText>(coded.Value);
        Assert.Equal("febrile", ct.Value);
        Assert.Equal("386661006", ct.DefiningCode.CodeString);

        Element dtElem = Assert.IsType<Element>(tree.Items![2]);
        DvDateTime dt = Assert.IsType<DvDateTime>(dtElem.Value);
        Assert.Equal("2024-05-27T10:25:03", dt.Value.OriginalLexicalForm);

        Assert.IsType<PartySelf>(obs.Subject);
        Assert.IsType<PartyIdentified>(back.Composer);
    }

    [Fact]
    public void Serialized_Json_Includes_TypeDiscriminators_Where_Polymorphic()
    {
        Composition original = BuildSample();
        byte[] bytes = OpenEhrJson.Serialize(original);

        using JsonDocument doc = JsonDocument.Parse(bytes);
        // Composition._type is COMPOSITION (declared on Locatable).
        Assert.Equal("COMPOSITION", doc.RootElement.GetProperty("_type").GetString());

        JsonElement obs = doc.RootElement.GetProperty("content")[0];
        Assert.Equal("OBSERVATION", obs.GetProperty("_type").GetString());

        JsonElement value = obs.GetProperty("data").GetProperty("events")[0]
            .GetProperty("data").GetProperty("items")[0].GetProperty("value");
        Assert.Equal("DV_QUANTITY", value.GetProperty("_type").GetString());

        JsonElement codedValue = obs.GetProperty("data").GetProperty("events")[0]
            .GetProperty("data").GetProperty("items")[1].GetProperty("value");
        Assert.Equal("DV_CODED_TEXT", codedValue.GetProperty("_type").GetString());

        JsonElement dvDateTime = obs.GetProperty("data").GetProperty("events")[0]
            .GetProperty("data").GetProperty("items")[2].GetProperty("value");
        Assert.Equal("DV_DATE_TIME", dvDateTime.GetProperty("_type").GetString());

        Assert.Equal("PARTY_SELF", obs.GetProperty("subject").GetProperty("_type").GetString());
        Assert.Equal("PARTY_IDENTIFIED", doc.RootElement.GetProperty("composer").GetProperty("_type").GetString());
    }

    [Fact]
    public void All_Property_Keys_Are_Snake_Case_Lower()
    {
        Composition original = BuildSample();
        byte[] bytes = OpenEhrJson.Serialize(original);
        using JsonDocument doc = JsonDocument.Parse(bytes);

        List<string> offenders = [];
        WalkKeys(doc.RootElement, offenders);

        Assert.True(offenders.Count == 0,
            "Found JSON property keys that are not snake_case_lower (or '_type'):" + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Null_Optional_Properties_Are_Omitted_From_Output()
    {
        Composition original = BuildSample();
        byte[] bytes = OpenEhrJson.Serialize(original);
        string text = Encoding.UTF8.GetString(bytes);

        // No literal "null" values should appear: we have no nulled-out properties on this sample.
        Assert.DoesNotContain(":null", text, StringComparison.Ordinal);
        // Optional Context not set on the composition — must not appear as a key.
        using JsonDocument doc = JsonDocument.Parse(bytes);
        Assert.False(doc.RootElement.TryGetProperty("context", out _),
            "Optional 'context' must be omitted when null.");
    }

    private static void WalkKeys(JsonElement element, List<string> offenders, string path = "$")
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty p in element.EnumerateObject())
                {
                    if (!IsAcceptableKey(p.Name))
                    {
                        offenders.Add($"{path}.{p.Name}");
                    }
                    WalkKeys(p.Value, offenders, $"{path}.{p.Name}");
                }
                break;
            case JsonValueKind.Array:
                int i = 0;
                foreach (JsonElement child in element.EnumerateArray())
                {
                    WalkKeys(child, offenders, $"{path}[{i++}]");
                }
                break;
            default:
                break;
        }
    }

    private static bool IsAcceptableKey(string key)
    {
        if (key.Length == 0) return false;
        // openEHR canonical reserves the leading-underscore discriminator '_type'.
        if (key == "_type") return true;
        foreach (char c in key)
        {
            // snake_case_lower: lower letters, digits, underscores only.
            if (!(c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
            {
                return false;
            }
        }
        return true;
    }
}
