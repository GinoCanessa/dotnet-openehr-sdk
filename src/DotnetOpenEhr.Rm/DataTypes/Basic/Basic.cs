using System.Text.Json.Serialization;

namespace DotnetOpenEhr.Rm.DataTypes.Basic;

// SPEC: Data Types Information Model.html#_dv_boolean_class (Section 4.2.2).
/// <summary>Item with two values, <c>true</c> and <c>false</c>.</summary>
public sealed class DvBoolean : DataValue
{
    public DvBoolean() { }

    public DvBoolean(bool value)
    {
        Value = value;
    }

    [JsonPropertyName("value")]
    public bool Value { get; set; }

    public override string ToString() => Value ? "true" : "false";
}

// SPEC: Data Types Information Model.html#_dv_state_class (Section 4.2.3).
/// <summary>
/// Carries a state value coded by an external state machine. Models a
/// process state, e.g. "active" / "inactive".
/// </summary>
public sealed class DvState : DataValue
{
    [JsonPropertyName("value")]
    public Text.DvCodedText Value { get; set; } = new();

    [JsonPropertyName("is_terminal")]
    public bool IsTerminal { get; set; }

    public override string ToString() => Value.ToString();
}

// SPEC: Data Types Information Model.html#_dv_identifier_class (Section 4.2.4).
/// <summary>
/// Type for representing identifiers of real-world entities such as
/// drivers' license numbers, prescriptions, etc.
/// </summary>
public sealed class DvIdentifier : DataValue
{
    public DvIdentifier() { }

    public DvIdentifier(string id, string? issuer = null, string? assigner = null, string? type = null)
    {
        Id = id;
        Issuer = issuer;
        Assigner = assigner;
        Type = type;
    }

    [JsonPropertyName("issuer")]
    public string? Issuer { get; set; }

    [JsonPropertyName("assigner")]
    public string? Assigner { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    public override string ToString() => Id;
}
