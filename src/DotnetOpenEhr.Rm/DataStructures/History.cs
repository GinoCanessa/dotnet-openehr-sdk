using System.Text.Json.Serialization;
using DotnetOpenEhr.Rm.DataTypes.DateTime;

namespace DotnetOpenEhr.Rm.DataStructures;

// SPEC: Data Structures Information Model.html#_history_class (Section 5.2.1).
/// <summary>Time series of <see cref="Event"/>s for an observation.</summary>
public sealed class History : DataStructure
{
    [JsonPropertyName("origin")]
    public DvDateTime Origin { get; set; } = new();

    [JsonPropertyName("period")]
    public DvDuration? Period { get; set; }

    [JsonPropertyName("duration")]
    public DvDuration? Duration { get; set; }

    [JsonPropertyName("summary")]
    public ItemStructure? Summary { get; set; }

    [JsonPropertyName("events")]
    public IList<Event>? Events { get; set; }
}

// SPEC: Data Structures Information Model.html#_event_class (Section 5.2.2).
/// <summary>Abstract base for a point or interval event in a <see cref="History"/>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(PointEvent),    "POINT_EVENT")]
[JsonDerivedType(typeof(IntervalEvent), "INTERVAL_EVENT")]
public abstract class Event : Common.Locatable
{
    [JsonPropertyName("time")]
    public DvDateTime Time { get; set; } = new();

    [JsonPropertyName("data")]
    public ItemStructure Data { get; set; } = new ItemTree();

    [JsonPropertyName("state")]
    public ItemStructure? State { get; set; }
}

// SPEC: Data Structures Information Model.html#_point_event_class.
/// <summary>Instantaneous-in-time event sample.</summary>
public sealed class PointEvent : Event
{
}

// SPEC: Data Structures Information Model.html#_interval_event_class.
/// <summary>Event whose value summarises a non-zero interval.</summary>
public sealed class IntervalEvent : Event
{
    [JsonPropertyName("width")]
    public DvDuration Width { get; set; } = new();

    [JsonPropertyName("sample_count")]
    public int? SampleCount { get; set; }

    [JsonPropertyName("math_function")]
    public DataTypes.Text.DvCodedText MathFunction { get; set; } = new();
}
