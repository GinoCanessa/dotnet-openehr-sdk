using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Support;

namespace DotnetOpenEhr.Aql.Tests.Evaluation;

/// <summary>
/// Test-only helpers that build minimal but well-formed RM
/// <see cref="Composition"/> instances. Each builder method documents
/// the archetype node ids it stamps onto the produced tree so the
/// evaluator tests can use stable path predicates.
/// </summary>
internal static class CompositionBuilder
{
    public static Composition NewComposition(
        string nameValue,
        string uidValue,
        string archetypeNodeId = "openEHR-EHR-COMPOSITION.encounter.v1",
        EventContext? context = null,
        IList<ContentItem>? content = null)
    {
        return new Composition
        {
            ArchetypeNodeId = archetypeNodeId,
            Name = new DvText(nameValue),
            Uid = new HierObjectId { Value = uidValue },
            Language = new CodePhrase(new TerminologyId { Value = "ISO_639-1" }, "en"),
            Territory = new CodePhrase(new TerminologyId { Value = "ISO_3166-1" }, "GB"),
            Category = new DvCodedText(
                "event",
                new CodePhrase(new TerminologyId { Value = "openehr" }, "433")),
            Composer = new PartyIdentified { Name = "Test Author" },
            Context = context,
            Content = content,
        };
    }

    public static EventContext NewContext()
        => new EventContext
        {
            StartTime = new DvDateTime(new DotnetOpenEhr.Foundation.Iso.IsoDateTime(
                new DotnetOpenEhr.Foundation.Iso.IsoDate(2024, 1, 15),
                new DotnetOpenEhr.Foundation.Iso.IsoTime(10, 0, 0))),
            Setting = new DvCodedText(
                "primary medical care",
                new CodePhrase(new TerminologyId { Value = "openehr" }, "228")),
        };

    /// <summary>
    /// Build a Blood Pressure Observation with a single PointEvent
    /// whose data tree contains systolic and diastolic Elements.
    /// Node ids match the openEHR BP archetype's published structure:
    /// data=at0001, events=at0002 (any_event), event_data=at0003,
    /// systolic=at0004, diastolic=at0005.
    /// </summary>
    public static Observation NewBloodPressure(
        string archetypeNodeId,
        double systolicValue,
        string systolicUnits,
        double diastolicValue,
        string diastolicUnits)
    {
        return new Observation
        {
            ArchetypeNodeId = archetypeNodeId,
            Name = new DvText("Blood pressure"),
            Language = new CodePhrase(new TerminologyId { Value = "ISO_639-1" }, "en"),
            Encoding = new CodePhrase(new TerminologyId { Value = "IANA_character-sets" }, "UTF-8"),
            Subject = new PartySelf(),
            Data = new History
            {
                ArchetypeNodeId = "at0001",
                Name = new DvText("History"),
                Origin = new DvDateTime(new DotnetOpenEhr.Foundation.Iso.IsoDateTime(
                    new DotnetOpenEhr.Foundation.Iso.IsoDate(2024, 1, 15),
                    new DotnetOpenEhr.Foundation.Iso.IsoTime(10, 0, 0))),
                Events =
                [
                    new PointEvent
                    {
                        ArchetypeNodeId = "at0006",
                        Name = new DvText("Any event"),
                        Time = new DvDateTime(new DotnetOpenEhr.Foundation.Iso.IsoDateTime(
                            new DotnetOpenEhr.Foundation.Iso.IsoDate(2024, 1, 15),
                            new DotnetOpenEhr.Foundation.Iso.IsoTime(10, 0, 0))),
                        Data = new ItemTree
                        {
                            ArchetypeNodeId = "at0003",
                            Name = new DvText("Tree"),
                            Items =
                            [
                                new Element
                                {
                                    ArchetypeNodeId = "at0004",
                                    Name = new DvText("Systolic"),
                                    Value = new DvQuantity(systolicValue, systolicUnits),
                                },
                                new Element
                                {
                                    ArchetypeNodeId = "at0005",
                                    Name = new DvText("Diastolic"),
                                    Value = new DvQuantity(diastolicValue, diastolicUnits),
                                },
                            ],
                        },
                    },
                ],
            },
        };
    }

    /// <summary>
    /// Build a Blood Pressure Observation with two PointEvents,
    /// each carrying the same systolic / diastolic at-code structure
    /// as <see cref="NewBloodPressure"/>. Used by the resolver tests
    /// to exercise multi-match and document-order assertions where
    /// the events themselves carry no node-id predicate.
    /// </summary>
    public static Observation NewBloodPressureWithTwoEvents(
        string archetypeNodeId,
        double firstSystolic,
        double firstDiastolic,
        double secondSystolic,
        double secondDiastolic,
        string units = "mm[Hg]")
    {
        PointEvent BuildEvent(string archetypeId, int hour, double sys, double dia)
            => new()
            {
                ArchetypeNodeId = archetypeId,
                Name = new DvText("Any event"),
                Time = new DvDateTime(new DotnetOpenEhr.Foundation.Iso.IsoDateTime(
                    new DotnetOpenEhr.Foundation.Iso.IsoDate(2024, 1, 15),
                    new DotnetOpenEhr.Foundation.Iso.IsoTime(hour, 0, 0))),
                Data = new ItemTree
                {
                    ArchetypeNodeId = "at0003",
                    Name = new DvText("Tree"),
                    Items =
                    [
                        new Element
                        {
                            ArchetypeNodeId = "at0004",
                            Name = new DvText("Systolic"),
                            Value = new DvQuantity(sys, units),
                        },
                        new Element
                        {
                            ArchetypeNodeId = "at0005",
                            Name = new DvText("Diastolic"),
                            Value = new DvQuantity(dia, units),
                        },
                    ],
                },
            };

        return new Observation
        {
            ArchetypeNodeId = archetypeNodeId,
            Name = new DvText("Blood pressure"),
            Language = new CodePhrase(new TerminologyId { Value = "ISO_639-1" }, "en"),
            Encoding = new CodePhrase(new TerminologyId { Value = "IANA_character-sets" }, "UTF-8"),
            Subject = new PartySelf(),
            Data = new History
            {
                ArchetypeNodeId = "at0001",
                Name = new DvText("History"),
                Origin = new DvDateTime(new DotnetOpenEhr.Foundation.Iso.IsoDateTime(
                    new DotnetOpenEhr.Foundation.Iso.IsoDate(2024, 1, 15),
                    new DotnetOpenEhr.Foundation.Iso.IsoTime(10, 0, 0))),
                Events =
                [
                    BuildEvent("at0006", 10, firstSystolic, firstDiastolic),
                    BuildEvent("at0006", 11, secondSystolic, secondDiastolic),
                ],
            },
        };
    }
}
