using System.Text.Json.Serialization;

namespace DotnetOpenEhr.Rm.DataTypes.Encapsulated;

// SPEC: Data Types Information Model.html#_dv_encapsulated_class (Section 9.2.1).
/// <summary>
/// Abstract base of types representing encapsulated data, i.e. data not
/// directly modelled by openEHR. Carries character set and language.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(DvMultimedia), "DV_MULTIMEDIA")]
[JsonDerivedType(typeof(DvParsable),   "DV_PARSABLE")]
public abstract class DvEncapsulated : DataValue
{
    [JsonPropertyName("charset")]
    public Text.CodePhrase? Charset { get; set; }

    [JsonPropertyName("language")]
    public Text.CodePhrase? Language { get; set; }
}

// SPEC: Data Types Information Model.html#_dv_multimedia_class (Section 9.2.2).
/// <summary>Multimedia encapsulated data, e.g. an image, an audio clip.</summary>
public sealed class DvMultimedia : DvEncapsulated
{
    [JsonPropertyName("alternate_text")]
    public string? AlternateText { get; set; }

    [JsonPropertyName("uri")]
    public Uri.DvUri? Uri { get; set; }

    [JsonPropertyName("data")]
    public byte[]? Data { get; set; }

    [JsonPropertyName("media_type")]
    public Text.CodePhrase MediaType { get; set; } = new();

    [JsonPropertyName("compression_algorithm")]
    public Text.CodePhrase? CompressionAlgorithm { get; set; }

    [JsonPropertyName("integrity_check")]
    public byte[]? IntegrityCheck { get; set; }

    [JsonPropertyName("integrity_check_algorithm")]
    public Text.CodePhrase? IntegrityCheckAlgorithm { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("thumbnail")]
    public DvMultimedia? Thumbnail { get; set; }
}

// SPEC: Data Types Information Model.html#_dv_parsable_class (Section 9.2.3).
/// <summary>Encapsulated value whose representation is a string in a known formalism.</summary>
public sealed class DvParsable : DvEncapsulated
{
    public DvParsable() { }

    public DvParsable(string value, string formalism)
    {
        Value = value;
        Formalism = formalism;
    }

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("formalism")]
    public string Formalism { get; set; } = string.Empty;
}
