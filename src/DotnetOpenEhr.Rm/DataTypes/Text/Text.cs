using System.Text.Json.Serialization;

namespace DotnetOpenEhr.Rm.DataTypes.Text;

// SPEC: Data Types Information Model.html#_code_phrase_class (Section 5.2.3).
/// <summary>
/// Fully co-ordinated reference to a coded term within a terminology.
/// Carries terminology id, code string and an optional preferred term.
/// </summary>
public sealed class CodePhrase
{
    public CodePhrase() { }

    public CodePhrase(Support.TerminologyId terminologyId, string codeString, string? preferredTerm = null)
    {
        TerminologyId = terminologyId;
        CodeString = codeString;
        PreferredTerm = preferredTerm;
    }

    [JsonPropertyName("terminology_id")]
    public Support.TerminologyId TerminologyId { get; set; } = new();

    [JsonPropertyName("code_string")]
    public string CodeString { get; set; } = string.Empty;

    [JsonPropertyName("preferred_term")]
    public string? PreferredTerm { get; set; }

    public override string ToString() => $"{TerminologyId.Value}::{CodeString}";
}

// SPEC: Data Types Information Model.html#_term_mapping_class (Section 5.2.2).
/// <summary>
/// Coded mapping attached to a <see cref="DvText"/> via its <c>mappings</c>
/// collection.
/// </summary>
public sealed class TermMapping
{
    [JsonPropertyName("match")]
    public string Match { get; set; } = "?";

    [JsonPropertyName("purpose")]
    public DvCodedText? Purpose { get; set; }

    [JsonPropertyName("target")]
    public CodePhrase Target { get; set; } = new();
}

// SPEC: Data Types Information Model.html#_dv_text_class (Section 5.2.1).
/// <summary>Plain or markdown text data value, optionally coded via mappings.</summary>
public class DvText : DataValue
{
    public DvText() { }

    public DvText(string value)
    {
        Value = value;
    }

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("hyperlink")]
    public Uri.DvUri? Hyperlink { get; set; }

    [JsonPropertyName("formatting")]
    public string? Formatting { get; set; }

    [JsonPropertyName("mappings")]
    public IReadOnlyList<TermMapping>? Mappings { get; set; }

    [JsonPropertyName("language")]
    public CodePhrase? Language { get; set; }

    [JsonPropertyName("encoding")]
    public CodePhrase? Encoding { get; set; }

    public override string ToString() => Value;
}

// SPEC: Data Types Information Model.html#_dv_coded_text_class (Section 5.2.4).
/// <summary>Text whose value is the rubric of a coded term identified by defining_code.</summary>
public sealed class DvCodedText : DvText
{
    public DvCodedText() { }

    public DvCodedText(string value, CodePhrase definingCode)
        : base(value)
    {
        DefiningCode = definingCode;
    }

    [JsonPropertyName("defining_code")]
    public CodePhrase DefiningCode { get; set; } = new();

    public override string ToString()
        => $"{Value} [{DefiningCode.TerminologyId.Value}::{DefiningCode.CodeString}]";
}
